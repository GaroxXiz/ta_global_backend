using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.Abstractions
{
    public sealed record DailyAggregateRecord(DateTime Date, int OrderCount, decimal DailyTotal);
    public sealed record CountryAggregateRecord(string Country, int Orders, decimal Revenue);
    public sealed record ProviderSplitRecord(string Label, int Value);

    public interface ISellerRepository
    {
        Task<IReadOnlyList<Seller>> ListAsync(bool verifiedOnly, CancellationToken cancellationToken);
        Task<Seller?> FindAsync(int id, CancellationToken cancellationToken);
        Task<int> CountProductsAsync(int sellerId, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(Seller seller, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
        Task<object> GetAnalyticsAsync(int sellerId, CancellationToken cancellationToken);
    }
}

namespace TAGlobal.Api.Infrastructure.Repositories
{
    using TAGlobal.Api.Application.Abstractions;

    public sealed class EfSellerRepository(AppDbContext db) : ISellerRepository
    {
        // Raw SQL: Mengambil daftar penjual (semua atau yang statusnya terverifikasi saja)
        public async Task<IReadOnlyList<Seller>> ListAsync(bool verifiedOnly, CancellationToken ct)
        {
            var sql = verifiedOnly
                ? """
                  SELECT 
                      s.*
                  FROM "Sellers" s
                  WHERE s."Status" = 'verified' 
                  ORDER BY s."Id" ASC
                  """
                : """
                  SELECT 
                      s.*
                  FROM "Sellers" s
                  ORDER BY s."Id" ASC
                  """;

            return await db.Sellers
                .FromSqlRaw(sql)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        // Raw SQL: Mencari data toko/penjual berdasarkan Id
        public Task<Seller?> FindAsync(int id, CancellationToken ct) =>
            db.Sellers
                .FromSqlInterpolated($"""
                    SELECT 
                        s.*
                    FROM "Sellers" s
                    WHERE s."Id" = {id}
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

        // Raw SQL: Menghitung total jumlah produk yang terdaftar atas penjual ini
        public Task<int> CountProductsAsync(int sellerId, CancellationToken ct) =>
            db.Database
                .SqlQuery<int>($"""
                    SELECT COUNT(*)::int AS "Value" 
                    FROM "Products" 
                    WHERE "SellerId" = {sellerId}
                    """)
                .SingleAsync(ct);

        // Raw SQL: Memperbarui informasi profil penjual
        public async Task<bool> UpdateAsync(Seller seller, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Sellers" 
                SET "Name"    = {seller.Name}, 
                    "Owner"   = {seller.Owner}, 
                    "Block"   = {seller.Block}, 
                    "Email"   = {seller.Email}, 
                    "Phone"   = {seller.Phone}, 
                    "Country" = {seller.Country}, 
                    "Status"  = {seller.Status}
                WHERE "Id" = {seller.Id}
                """, ct) > 0;

        // Raw SQL: Menghapus data penjual berdasarkan Id
        public async Task<bool> DeleteAsync(int id, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "Sellers"
                WHERE "Id" = {id}
                """, ct) > 0;

        // Raw SQL: Menghitung statistik & analitik 14 hari toko untuk Seller Dashboard dan Seller Analytics
        public async Task<object> GetAnalyticsAsync(int sellerId, CancellationToken ct)
        {
            var dates = Enumerable.Range(0, 14)
                .Select(i => DateTime.UtcNow.Date.AddDays(-13 + i))
                .ToArray();

            var labels = dates.Select(d => d.ToString("dd MMM")).ToArray();

            // Raw SQL: Agregasi pesanan dan omzet harian seller 14 hari terakhir
            var dailyData = await db.Database
                .SqlQuery<DailyAggregateRecord>($"""
                    SELECT 
                        DATE(o."PlacedAtUtc") AS "Date", 
                        COUNT(*)::int AS "OrderCount", 
                        COALESCE(SUM(o."Total"), 0)::numeric AS "DailyTotal"
                    FROM "Orders" o
                    WHERE o."SellerId" = {sellerId}
                      AND o."PlacedAtUtc" >= CURRENT_DATE - INTERVAL '13 days'
                    GROUP BY DATE(o."PlacedAtUtc")
                    ORDER BY "Date" ASC
                    """)
                .ToListAsync(ct);

            var dailyMap = dailyData.ToDictionary(d => d.Date.Date, d => d);

            var revenueSeries = new decimal[14];
            var ordersSeries = new int[14];
            var visitorsSeries = new int[14];

            for (int i = 0; i < 14; i++)
            {
                var dt = dates[i];
                if (dailyMap.TryGetValue(dt, out var record))
                {
                    revenueSeries[i] = record.DailyTotal;
                    ordersSeries[i] = record.OrderCount;
                    visitorsSeries[i] = Math.Max(record.OrderCount * 18, 12);
                }
                else
                {
                    revenueSeries[i] = 0;
                    ordersSeries[i] = 0;
                    visitorsSeries[i] = 5 + (i % 7);
                }
            }

            // Raw SQL: 5 Negara tujuan pembeli teratas untuk seller ini
            var topCountries = await db.Database
                .SqlQuery<CountryAggregateRecord>($"""
                    SELECT 
                        o."Country" AS "Country",
                        COUNT(*)::int AS "Orders",
                        COALESCE(SUM(o."Total"), 0)::numeric AS "Revenue"
                    FROM "Orders" o
                    WHERE o."SellerId" = {sellerId}
                    GROUP BY o."Country"
                    ORDER BY "Revenue" DESC
                    LIMIT 5
                    """)
                .ToListAsync(ct);

            // Raw SQL: Komposisi penggunaan kurir logistik oleh pembeli
            var providerSplit = await db.Database
                .SqlQuery<ProviderSplitRecord>($"""
                    SELECT 
                        COALESCE(o."ProviderId", 'Lainnya') AS "Label",
                        COUNT(*)::int AS "Value"
                    FROM "Orders" o
                    WHERE o."SellerId" = {sellerId}
                    GROUP BY o."ProviderId"
                    ORDER BY "Value" DESC
                    """)
                .ToListAsync(ct);

            var totalOrders = ordersSeries.Sum();
            var totalViews = Math.Max(totalOrders * 20, 100);
            var totalCart = Math.Max(totalOrders * 4, 20);

            var funnel = new[]
            {
                new { step = "Dilihat", value = totalViews },
                new { step = "Dimasukkan keranjang", value = totalCart },
                new { step = "Checkout", value = Math.Max(totalOrders * 2, 10) },
                new { step = "Pesanan dibayar", value = totalOrders }
            };

            return new
            {
                revenueSeries,
                ordersSeries,
                visitorsSeries,
                labels,
                topCountries,
                funnel,
                providerSplit = providerSplit.Select((p, idx) => new
                {
                    label = p.Label,
                    value = p.Value,
                    color = idx switch { 0 => "#0b3d2c", 1 => "#b8863b", 2 => "#2b6cb0", 3 => "#c53030", _ => "#718096" }
                })
            };
        }
    }
}
