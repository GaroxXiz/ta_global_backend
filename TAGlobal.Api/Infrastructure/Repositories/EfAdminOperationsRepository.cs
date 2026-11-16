using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.Abstractions
{
    public sealed record BroadcastRecipient(int Id, int? SellerId);
    public sealed record CategorySplitRecord(string Label, decimal Value);
    public sealed record SellerCountRecord(DateTime Date, int Count);

    public interface IAdminOperationsRepository
    {
        Task<IReadOnlyList<MarketplaceOrder>> ListOrdersAsync(CancellationToken ct);
        Task<IReadOnlyList<ChatThread>> ListThreadsAsync(CancellationToken ct);
        Task<ChatThread?> FindThreadWithMessagesAsync(int id, CancellationToken ct);
        Task<bool> MarkThreadReadAsync(int id, CancellationToken ct);
        Task<ChatThread?> SendMessageAsync(int id, string fromUserId, string text, CancellationToken ct);
        Task<ChatThread> CreateThreadAsync(ChatThread thread, CancellationToken ct);
        Task CreateThreadsAsync(IReadOnlyList<ChatThread> threads, CancellationToken ct);
        Task<IReadOnlyList<BroadcastRecipient>> ListRecipientsAsync(CancellationToken ct);
        Task<string?> GetSettingAsync(string key, CancellationToken ct);
        Task SaveSettingAsync(string key, string json, CancellationToken ct);
        Task<(int Products, int Sellers, int Orders, decimal Gmv)> GetDashboardAsync(CancellationToken ct);
        Task<object> GetAnalyticsAsync(CancellationToken ct);
    }
}

namespace TAGlobal.Api.Infrastructure.Repositories
{
    using TAGlobal.Api.Application.Abstractions;

    public sealed class EfAdminOperationsRepository(AppDbContext db) : IAdminOperationsRepository
    {
        // Helper Raw SQL: Mengambil item rincian pesanan tanpa menggunakan EF Include
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

        // Helper Raw SQL: Mengambil daftar pesan untuk setiap thread chat tanpa menggunakan EF Include
        private async Task PopulateThreadMessagesAsync(IReadOnlyList<ChatThread> threads, CancellationToken ct)
        {
            var threadIds = threads.Select(t => t.Id).Distinct().ToArray();
            if (threadIds.Length == 0) return;

            var messages = await db.Messages
                .FromSqlInterpolated($"""
                    SELECT 
                        m.*
                    FROM "Messages" m
                    WHERE m."ThreadId" = ANY({threadIds}) 
                    ORDER BY m."SentAtUtc" ASC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            var messagesByThreadId = messages.GroupBy(m => m.ThreadId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var thread in threads)
            {
                if (messagesByThreadId.TryGetValue(thread.Id, out var threadMessages))
                {
                    thread.Messages = threadMessages;
                }
            }
        }

        // Raw SQL: Mengambil seluruh pesanan untuk dashboard admin
        public async Task<IReadOnlyList<MarketplaceOrder>> ListOrdersAsync(CancellationToken ct)
        {
            var orders = await db.Orders
                .FromSqlRaw("""
                    SELECT 
                        o.*
                    FROM "Orders" o
                    ORDER BY o."PlacedAtUtc" DESC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            await PopulateOrderItemsAsync(orders, ct);
            return orders;
        }

        // Raw SQL: Mengambil semua thread chat beserta pesannya
        public async Task<IReadOnlyList<ChatThread>> ListThreadsAsync(CancellationToken ct)
        {
            var threads = await db.Threads
                .FromSqlRaw("""
                    SELECT 
                        t.*
                    FROM "Threads" t
                    ORDER BY t."UpdatedAtUtc" DESC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            await PopulateThreadMessagesAsync(threads, ct);
            return threads;
        }

        // Raw SQL: Mencari satu thread chat beserta pesan-pesannya berdasarkan Id
        public async Task<ChatThread?> FindThreadWithMessagesAsync(int id, CancellationToken ct)
        {
            var thread = await db.Threads
                .FromSqlInterpolated($"""
                    SELECT 
                        t.*
                    FROM "Threads" t
                    WHERE t."Id" = {id}
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (thread is not null)
            {
                thread.Messages = await db.Messages
                    .FromSqlInterpolated($"""
                        SELECT 
                            m.*
                        FROM "Messages" m
                        WHERE m."ThreadId" = {id} 
                        ORDER BY m."SentAtUtc" ASC
                        """)
                    .AsNoTracking()
                    .ToListAsync(ct);
            }

            return thread;
        }

        // Raw SQL: Menandai pesan dalam thread sudah dibaca (unread = 0)
        public async Task<bool> MarkThreadReadAsync(int id, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Threads" 
                SET "Unread" = 0 
                WHERE "Id" = {id}
                """, ct) > 0;

        // Raw SQL: Mengirim pesan baru ke dalam thread chat dan mengupdate waktu aktivitas thread
        public async Task<ChatThread?> SendMessageAsync(int id, string fromUserId, string text, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Threads" 
                SET "UpdatedAtUtc" = {DateTime.UtcNow}, 
                    "Unread"       = 0 
                WHERE "Id" = {id}
                """, ct);

            if (changed == 0) return null;

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Messages" (
                    "ThreadId", 
                    "FromUserId", 
                    "Text", 
                    "SentAtUtc"
                ) 
                VALUES (
                    {id}, 
                    {fromUserId}, 
                    {text}, 
                    {DateTime.UtcNow}
                )
                """, ct);

            await transaction.CommitAsync(ct);
            return await FindThreadWithMessagesAsync(id, ct);
        }

        // Raw SQL: Membuat thread chat baru beserta pesan perdananya
        public async Task<ChatThread> CreateThreadAsync(ChatThread thread, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            thread.Id = await InsertThreadAsync(thread, ct);
            foreach (var message in thread.Messages)
            {
                await InsertMessageAsync(thread.Id, message, ct);
            }
            await transaction.CommitAsync(ct);
            return thread;
        }

        // Raw SQL: Membuat beberapa thread chat sekaligus (misalnya untuk fitur broadcast)
        public async Task CreateThreadsAsync(IReadOnlyList<ChatThread> threads, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            foreach (var thread in threads)
            {
                thread.Id = await InsertThreadAsync(thread, ct);
                foreach (var message in thread.Messages)
                {
                    await InsertMessageAsync(thread.Id, message, ct);
                }
            }
            await transaction.CommitAsync(ct);
        }

        // Raw SQL: Insert record thread chat baru dan mengembalikan Id
        private async Task<int> InsertThreadAsync(ChatThread thread, CancellationToken ct) =>
            await db.Database.SqlQuery<int>($"""
                INSERT INTO "Threads" (
                    "ThreadKey", 
                    "With", 
                    "Role", 
                    "Subject", 
                    "Party", 
                    "Unread", 
                    "UpdatedAtUtc"
                )
                VALUES (
                    {thread.ThreadKey}, 
                    {thread.With}, 
                    {thread.Role}, 
                    {thread.Subject}, 
                    {thread.Party}, 
                    {thread.Unread}, 
                    {thread.UpdatedAtUtc}
                )
                RETURNING "Id" AS "Value"
                """).SingleAsync(ct);

        // Raw SQL: Insert record pesan baru ke dalam thread chat
        private async Task InsertMessageAsync(int threadId, ChatMessage message, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Messages" (
                    "ThreadId", 
                    "FromUserId", 
                    "Text", 
                    "SentAtUtc"
                ) 
                VALUES (
                    {threadId}, 
                    {message.FromUserId}, 
                    {message.Text}, 
                    {message.SentAtUtc}
                )
                """, ct);

        // Raw SQL: Mengambil daftar penerima broadcast (hanya kolom Id dan SellerId)
        public async Task<IReadOnlyList<BroadcastRecipient>> ListRecipientsAsync(CancellationToken ct) =>
            await db.Database
                .SqlQueryRaw<BroadcastRecipient>("""
                    SELECT 
                        u."Id", 
                        u."SellerId" 
                    FROM "Users" u
                    """)
                .ToListAsync(ct);

        // Raw SQL: Mengambil nilai JSON setting berdasarkan Key
        public Task<string?> GetSettingAsync(string key, CancellationToken ct) =>
            db.Database
                .SqlQuery<string>($"""
                    SELECT "ValueJson" AS "Value" 
                    FROM "Settings" 
                    WHERE "Key" = {key}
                    LIMIT 1
                    """)
                .SingleOrDefaultAsync(ct);

        // Raw SQL: Menyimpan atau memperbarui setting sistem (UPSERT dengan ON CONFLICT)
        public async Task SaveSettingAsync(string key, string json, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Settings" (
                    "Key", 
                    "ValueJson"
                ) 
                VALUES (
                    {key}, 
                    CAST({json} AS jsonb)
                ) 
                ON CONFLICT ("Key") 
                DO UPDATE SET "ValueJson" = EXCLUDED."ValueJson"
                """, ct);

        // Raw SQL: Mengumpulkan ringkasan statistik (Products, Sellers, Orders, GMV) untuk dashboard admin
        public async Task<(int Products, int Sellers, int Orders, decimal Gmv)> GetDashboardAsync(CancellationToken ct) =>
            (await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" 
                FROM "Products"
                """).SingleAsync(ct),

             await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" 
                FROM "Sellers"
                """).SingleAsync(ct),

             await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" 
                FROM "Orders"
                """).SingleAsync(ct),

             await db.Database.SqlQuery<decimal>($"""
                SELECT COALESCE(SUM("Total"), 0)::numeric AS "Value" 
                FROM "Orders"
                """).SingleAsync(ct));

        // Raw SQL: Mengumpulkan analitik komprehensif 14 hari untuk Super Admin Dashboard
        public async Task<object> GetAnalyticsAsync(CancellationToken ct)
        {
            var dates = Enumerable.Range(0, 14)
                .Select(i => DateTime.UtcNow.Date.AddDays(-13 + i))
                .ToArray();

            var labels = dates.Select(d => d.ToString("dd MMM")).ToArray();

            // Raw SQL: Total GMV dan order harian 14 hari
            var dailyData = await db.Database
                .SqlQuery<DailyAggregateRecord>($"""
                    SELECT 
                        DATE(o."PlacedAtUtc") AS "Date", 
                        COUNT(*)::int AS "OrderCount", 
                        COALESCE(SUM(o."Total"), 0)::numeric AS "DailyTotal"
                    FROM "Orders" o
                    WHERE o."PlacedAtUtc" >= CURRENT_DATE - INTERVAL '13 days'
                    GROUP BY DATE(o."PlacedAtUtc")
                    ORDER BY "Date" ASC
                    """)
                .ToListAsync(ct);

            var dailyMap = dailyData.ToDictionary(d => d.Date.Date, d => d);

            // Raw SQL: Penjual baru per hari 14 hari
            var sellersData = await db.Database
                .SqlQuery<SellerCountRecord>($"""
                    SELECT 
                        DATE(s."JoinedAtUtc") AS "Date", 
                        COUNT(*)::int AS "Count"
                    FROM "Sellers" s
                    WHERE s."JoinedAtUtc" >= CURRENT_DATE - INTERVAL '13 days'
                    GROUP BY DATE(s."JoinedAtUtc")
                    ORDER BY "Date" ASC
                    """)
                .ToListAsync(ct);

            var sellerMap = sellersData.ToDictionary(s => s.Date.Date, s => s.Count);

            var gmvSeries = new decimal[14];
            var ordersSeries = new int[14];
            var newSellers = new int[14];

            for (int i = 0; i < 14; i++)
            {
                var dt = dates[i];
                if (dailyMap.TryGetValue(dt, out var dRecord))
                {
                    gmvSeries[i] = dRecord.DailyTotal;
                    ordersSeries[i] = dRecord.OrderCount;
                }
                else
                {
                    gmvSeries[i] = 0;
                    ordersSeries[i] = 0;
                }

                newSellers[i] = sellerMap.TryGetValue(dt, out var sCount) ? sCount : 0;
            }

            // Raw SQL: Distribusi GMV per Kategori
            var categoryData = await db.Database
                .SqlQuery<CategorySplitRecord>($"""
                    SELECT 
                        c."Name" AS "Label",
                        COALESCE(SUM(p."Price" * p."Sold"), 0)::numeric AS "Value"
                    FROM "Categories" c
                    LEFT JOIN "Products" p ON p."CategoryId" = c."Id"
                    GROUP BY c."Id", c."Name"
                    ORDER BY "Value" DESC
                    """)
                .ToListAsync(ct);

            var categoryColors = new[] { "#0b3d2c", "#b8863b", "#2b6cb0", "#805ad5", "#dd6b20", "#319795", "#e53e3e", "#718096" };
            var categorySplit = categoryData.Select((c, idx) => new
            {
                label = c.Label,
                value = c.Value,
                color = categoryColors[idx % categoryColors.Length]
            });

            // Raw SQL: 5 Negara tujuan dengan transaksi tertinggi
            var topCountries = await db.Database
                .SqlQuery<CountryAggregateRecord>($"""
                    SELECT 
                        o."Country" AS "Country",
                        COUNT(*)::int AS "Orders",
                        COALESCE(SUM(o."Total"), 0)::numeric AS "Revenue"
                    FROM "Orders" o
                    GROUP BY o."Country"
                    ORDER BY "Revenue" DESC
                    LIMIT 5
                    """)
                .ToListAsync(ct);

            return new
            {
                gmvSeries,
                ordersSeries,
                newSellers,
                labels,
                categorySplit,
                topCountries
            };
        }
    }
}
