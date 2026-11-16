using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.Abstractions
{
    public interface IOrderRepository
    {
        Task<IReadOnlyList<MarketplaceOrder>> ListAsync(bool isAdmin, int? sellerId, string buyerName, CancellationToken ct);
        Task<MarketplaceOrder?> FindAsync(string orderNumber, CancellationToken ct);
        Task<MarketplaceOrder?> FindForTrackingAsync(string id, CancellationToken ct);
        Task<IReadOnlyList<Product>> FindProductsAsync(int[] ids, CancellationToken ct);
        Task<MarketplaceOrder> CreateAsync(MarketplaceOrder order, CancellationToken ct);
        Task<MarketplaceOrder?> UpdateStatusAsync(string orderNumber, string status, int stage, string? awb, CancellationToken ct);
        Task<DropoffManifest> CreateManifestAsync(DropoffManifest manifest, string[] orderNumbers, CancellationToken ct);
        Task<IReadOnlyList<DropoffManifest>> ListManifestsAsync(int sellerId, CancellationToken ct);
    }
}

namespace TAGlobal.Api.Infrastructure.Repositories
{
    using TAGlobal.Api.Application.Abstractions;

    public sealed class EfOrderRepository(AppDbContext db) : IOrderRepository
    {
        // Helper Raw SQL: Mengambil daftar item untuk sekumpulan pesanan tanpa menggunakan EF Include
        private async Task PopulateOrderItemsAsync(IReadOnlyList<MarketplaceOrder> orders, CancellationToken ct)
        {
            var orderIds = orders.Select(o => o.Id).Distinct().ToArray();
            if (orderIds.Length == 0) return;

            var items = await db.OrderItems
                .FromSqlInterpolated($"""
                    SELECT 
                        oi.*
                    FROM "OrderItems" oi
                    WHERE oi."OrderId" = ANY({orderIds})
                    ORDER BY oi."Id" ASC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            var itemsByOrderId = items.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var order in orders)
            {
                if (itemsByOrderId.TryGetValue(order.Id, out var orderItems))
                {
                    order.Items = orderItems;
                }
            }
        }

        // Raw SQL: Mengambil daftar pesanan berdasarkan role pengguna (Admin / Seller / Buyer)
        public async Task<IReadOnlyList<MarketplaceOrder>> ListAsync(bool isAdmin, int? sellerId, string buyerName, CancellationToken ct)
        {
            List<MarketplaceOrder> orders;
            if (isAdmin)
            {
                orders = await db.Orders
                    .FromSqlRaw("""
                        SELECT 
                            o.*
                        FROM "Orders" o
                        ORDER BY o."PlacedAtUtc" DESC
                        """)
                    .AsNoTracking()
                    .ToListAsync(ct);
            }
            else if (sellerId.HasValue)
            {
                orders = await db.Orders
                    .FromSqlInterpolated($"""
                        SELECT 
                            o.*
                        FROM "Orders" o
                        WHERE o."SellerId" = {sellerId.Value} 
                        ORDER BY o."PlacedAtUtc" DESC
                        """)
                    .AsNoTracking()
                    .ToListAsync(ct);
            }
            else
            {
                orders = await db.Orders
                    .FromSqlInterpolated($"""
                        SELECT 
                            o.*
                        FROM "Orders" o
                        WHERE o."BuyerName" = {buyerName} 
                        ORDER BY o."PlacedAtUtc" DESC
                        """)
                    .AsNoTracking()
                    .ToListAsync(ct);
            }

            await PopulateOrderItemsAsync(orders, ct);
            return orders;
        }

        // Raw SQL: Mencari satu pesanan beserta item rinciannya berdasarkan Nomor Pesanan
        public async Task<MarketplaceOrder?> FindAsync(string orderNumber, CancellationToken ct)
        {
            var order = await db.Orders
                .FromSqlInterpolated($"""
                    SELECT 
                        o.*
                    FROM "Orders" o
                    WHERE o."OrderNumber" = {orderNumber}
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (order is not null)
            {
                order.Items = await db.OrderItems
                    .FromSqlInterpolated($"""
                        SELECT 
                            oi.*
                        FROM "OrderItems" oi
                        WHERE oi."OrderId" = {order.Id}
                        ORDER BY oi."Id" ASC
                        """)
                    .AsNoTracking()
                    .ToListAsync(ct);
            }

            return order;
        }

        // Raw SQL: Mencari pesanan untuk pelacakan pengiriman (berdasarkan nomor pesanan atau resi AWB)
        public async Task<MarketplaceOrder?> FindForTrackingAsync(string id, CancellationToken ct)
        {
            var order = await db.Orders
                .FromSqlInterpolated($"""
                    SELECT 
                        o.*
                    FROM "Orders" o
                    WHERE o."OrderNumber" = {id} 
                       OR ({id} <> '-' AND o."Awb" = {id})
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (order is not null)
            {
                order.Items = await db.OrderItems
                    .FromSqlInterpolated($"""
                        SELECT 
                            oi.*
                        FROM "OrderItems" oi
                        WHERE oi."OrderId" = {order.Id}
                        ORDER BY oi."Id" ASC
                        """)
                    .AsNoTracking()
                    .ToListAsync(ct);
            }

            return order;
        }

        // Raw SQL: Mengambil sekumpulan produk pesanan berdasarkan array Id
        public async Task<IReadOnlyList<Product>> FindProductsAsync(int[] ids, CancellationToken ct) =>
            await db.Products
                .FromSqlInterpolated($"""
                    SELECT 
                        p.*
                    FROM "Products" p
                    WHERE p."Id" = ANY({ids})
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

        // Raw SQL: Membuat pesanan baru, mengurangi stok produk, dan menyimpan rincian item
        public async Task<MarketplaceOrder> CreateAsync(MarketplaceOrder order, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Raw SQL: Kurangi stok dan tambah counter penjualan produk
            foreach (var item in order.Items)
            {
                var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Products" 
                    SET "Stock" = "Stock" - {item.Quantity}, 
                        "Sold"  = "Sold"  + {item.Quantity} 
                    WHERE "Id" = {item.ProductId} 
                      AND "Stock" >= {item.Quantity}
                    """, ct);

                if (updated != 1)
                    throw new InvalidOperationException($"Stok produk {item.ProductName} berubah. Muat ulang keranjang.");
            }

            // Raw SQL: Insert record pesanan baru
            order.Id = await db.Database.SqlQuery<int>($"""
                INSERT INTO "Orders" (
                    "OrderNumber", 
                    "BuyerName", 
                    "Country", 
                    "City", 
                    "Address", 
                    "SellerId", 
                    "ProviderId", 
                    "Total", 
                    "Status", 
                    "Stage", 
                    "Awb", 
                    "Note", 
                    "PlacedAtUtc"
                )
                VALUES (
                    {order.OrderNumber}, 
                    {order.BuyerName}, 
                    {order.Country}, 
                    {order.City}, 
                    {order.Address}, 
                    {order.SellerId}, 
                    {order.ProviderId}, 
                    {order.Total}, 
                    {order.Status}, 
                    {order.Stage}, 
                    {order.Awb}, 
                    {order.Note}, 
                    {order.PlacedAtUtc}
                )
                RETURNING "Id" AS "Value"
                """).SingleAsync(ct);

            // Raw SQL: Insert rincian item pesanan
            foreach (var item in order.Items)
            {
                item.OrderId = order.Id;
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "OrderItems" (
                        "OrderId", 
                        "ProductId", 
                        "ProductName", 
                        "Quantity", 
                        "Variant"
                    ) 
                    VALUES (
                        {item.OrderId}, 
                        {item.ProductId}, 
                        {item.ProductName}, 
                        {item.Quantity}, 
                        {item.Variant}
                    )
                    """, ct);
            }

            await transaction.CommitAsync(ct);
            return order;
        }

        // Raw SQL: Memperbarui status, tahapan pelacakan, dan nomor resi AWB pesanan
        public async Task<MarketplaceOrder?> UpdateStatusAsync(string orderNumber, string status, int stage, string? awb, CancellationToken ct)
        {
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Orders" 
                SET "Status" = {status}, 
                    "Stage"  = {stage}, 
                    "Awb"    = COALESCE({awb}, "Awb") 
                WHERE "OrderNumber" = {orderNumber}
                """, ct);

            return changed == 0 ? null : await FindAsync(orderNumber, ct);
        }

        // Raw SQL: Membuat kode manifes serah terima gerai dan mengupdate status pesanan terkait menjadi 'at_hub'
        public async Task<DropoffManifest> CreateManifestAsync(DropoffManifest manifest, string[] orderNumbers, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            manifest.Id = await db.Database.SqlQuery<int>($"""
                INSERT INTO "DropoffManifests" (
                    "ManifestCode", 
                    "SellerId", 
                    "HubId", 
                    "Slot", 
                    "OrderIdsJson", 
                    "TotalOrders", 
                    "TotalWeight", 
                    "CreatedAtUtc"
                )
                VALUES (
                    {manifest.ManifestCode}, 
                    {manifest.SellerId}, 
                    {manifest.HubId}, 
                    {manifest.Slot}, 
                    CAST({manifest.OrderIdsJson} AS jsonb), 
                    {manifest.TotalOrders}, 
                    {manifest.TotalWeight}, 
                    {manifest.CreatedAtUtc}
                )
                RETURNING "Id" AS "Value"
                """).SingleAsync(ct);

            // Raw SQL: Update status setiap pesanan yang ada dalam antaran menjadi 'at_hub'
            foreach (var orderNumber in orderNumbers)
            {
                var randSuffix = Random.Shared.Next(10000000, 99999999);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Orders" 
                    SET "Status" = 'at_hub', 
                        "Stage"  = 2, 
                        "Awb"    = CASE 
                                     WHEN "Awb" = '-' OR "Awb" IS NULL THEN 'TA' || {randSuffix.ToString()}
                                     ELSE "Awb" 
                                   END
                    WHERE "OrderNumber" = {orderNumber}
                    """, ct);
            }

            await transaction.CommitAsync(ct);
            return manifest;
        }

        // Raw SQL: Mengambil daftar manifes yang dibuat oleh seller tertentu
        public async Task<IReadOnlyList<DropoffManifest>> ListManifestsAsync(int sellerId, CancellationToken ct) =>
            await db.DropoffManifests
                .FromSqlInterpolated($"""
                    SELECT 
                        m.*
                    FROM "DropoffManifests" m
                    WHERE m."SellerId" = {sellerId}
                    ORDER BY m."CreatedAtUtc" DESC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);
    }
}
