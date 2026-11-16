using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.Abstractions
{
    public interface ICategoryRepository
    {
        Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);
        Task<Category?> FindAsync(int id, CancellationToken cancellationToken);
        Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);
        Task<Category> CreateAsync(Category category, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }

    public interface IProductRepository
    {
        Task<IReadOnlyList<Product>> ListActiveAsync(string? search, int? categoryId, int page, int pageSize, CancellationToken cancellationToken);
        Task<IReadOnlyList<Product>> SearchMarketplaceAsync(string? search, string? category, int? sellerId, string? status, decimal? minPrice, decimal? maxPrice, int? ownSellerId, bool isAdmin, int page, int pageSize, CancellationToken cancellationToken);
        Task<IReadOnlyList<Product>> ListAllAsync(CancellationToken cancellationToken);
        Task<Product?> FindWithCategoryAsync(int id, CancellationToken cancellationToken);
        Task<Product?> FindAsync(int id, CancellationToken cancellationToken);
        Task<bool> CategoryExistsAsync(int id, CancellationToken cancellationToken);
        Task<Category?> ResolveCategoryAsync(int id, string? name, CancellationToken cancellationToken);
        Task<Product> CreateAsync(Product product, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
        Task<IReadOnlyList<ProductReview>> ListReviewsAsync(int productId, CancellationToken cancellationToken);
        Task<ProductReview> CreateReviewAsync(ProductReview review, CancellationToken cancellationToken);
    }
}

namespace TAGlobal.Api.Infrastructure.Repositories
{
    using TAGlobal.Api.Application.Abstractions;

    public sealed class EfCategoryRepository(AppDbContext db) : ICategoryRepository
    {
        // Raw SQL: Mengambil semua daftar kategori terurut berdasarkan Id
        public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct) =>
            await db.Categories
                .FromSqlRaw("""
                    SELECT 
                        c."Id",
                        c."Name",
                        c."Description"
                    FROM "Categories" c
                    ORDER BY c."Id" ASC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

        // Raw SQL: Mencari satu kategori berdasarkan Id
        public Task<Category?> FindAsync(int id, CancellationToken ct) =>
            db.Categories
                .FromSqlInterpolated($"""
                    SELECT 
                        c."Id",
                        c."Name",
                        c."Description"
                    FROM "Categories" c
                    WHERE c."Id" = {id}
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

        // Raw SQL: Mengecek apakah kategori ada berdasarkan Id
        public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
            db.Database
                .SqlQuery<bool>($"""
                    SELECT EXISTS (
                        SELECT 1 
                        FROM "Categories" 
                        WHERE "Id" = {id}
                    ) AS "Value"
                    """)
                .SingleAsync(ct);

        // Raw SQL: Menambahkan kategori baru dan mengembalikan Id hasil insert
        public async Task<Category> CreateAsync(Category category, CancellationToken ct)
        {
            category.Id = await db.Database
                .SqlQuery<int>($"""
                    INSERT INTO "Categories" (
                        "Name", 
                        "Description"
                    ) 
                    VALUES (
                        {category.Name}, 
                        {category.Description}
                    ) 
                    RETURNING "Id" AS "Value"
                    """)
                .SingleAsync(ct);

            return category;
        }

        // Raw SQL: Menghapus kategori berdasarkan Id
        public async Task<bool> DeleteAsync(int id, CancellationToken ct) =>
            await db.Database
                .ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM "Categories"
                    WHERE "Id" = {id}
                    """, ct) > 0;
    }

    public sealed class EfProductRepository(AppDbContext db) : IProductRepository
    {
        // Helper Raw SQL: Mengambil data kategori untuk produk tanpa menggunakan EF Include
        private async Task PopulateCategoriesAsync(IReadOnlyList<Product> products, CancellationToken ct)
        {
            var categoryIds = products.Select(p => p.CategoryId).Distinct().ToArray();
            if (categoryIds.Length == 0) return;

            var categories = await db.Categories
                .FromSqlInterpolated($"""
                    SELECT 
                        c."Id",
                        c."Name",
                        c."Description"
                    FROM "Categories" c
                    WHERE c."Id" = ANY({categoryIds})
                    """)
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Id, ct);

            foreach (var product in products)
            {
                if (categories.TryGetValue(product.CategoryId, out var category))
                {
                    product.Category = category;
                }
            }
        }

        // Raw SQL: Mencari produk pada marketplace dengan berbagai filter dinamis dan pagination
        public async Task<IReadOnlyList<Product>> SearchMarketplaceAsync(
            string? search, 
            string? category, 
            int? sellerId, 
            string? status, 
            decimal? minPrice, 
            decimal? maxPrice, 
            int? ownSellerId, 
            bool isAdmin, 
            int page, 
            int pageSize, 
            CancellationToken ct)
        {
            var sql = new StringBuilder("""
                SELECT 
                    p.* 
                FROM "Products" p 
                WHERE 1 = 1
                """);

            var parameters = new List<object>();
            void Add(string clause, object value)
            {
                var name = $"@p{parameters.Count}";
                sql.Append("\n  AND ").Append(clause.Replace("?", name));
                parameters.Add(new NpgsqlParameter(name, value));
            }

            if (!isAdmin && ownSellerId is null)
                sql.Append("\n  AND p.\"Status\" = 'active'");

            if (!isAdmin && ownSellerId is int own)
                Add("p.\"SellerId\" = ?", own);

            if (!string.IsNullOrWhiteSpace(status) && (isAdmin || ownSellerId.HasValue))
                Add("p.\"Status\" = ?", status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                var name = $"@p{parameters.Count}";
                parameters.Add(new NpgsqlParameter(name, term));
                sql.Append($"\n  AND (p.\"Name\" ILIKE {name} OR p.\"Sub\" ILIKE {name})");
            }

            if (!string.IsNullOrWhiteSpace(category))
                Add("EXISTS (SELECT 1 FROM \"Categories\" c WHERE c.\"Id\" = p.\"CategoryId\" AND c.\"Name\" = ?)", category);

            if (sellerId.HasValue)
                Add("p.\"SellerId\" = ?", sellerId.Value);

            if (minPrice.HasValue)
                Add("p.\"Price\" >= ?", minPrice.Value);

            if (maxPrice.HasValue)
                Add("p.\"Price\" <= ?", maxPrice.Value);

            var limit = $"@p{parameters.Count}";
            parameters.Add(new NpgsqlParameter(limit, pageSize));

            var offset = $"@p{parameters.Count}";
            parameters.Add(new NpgsqlParameter(offset, (Math.Max(1, page) - 1) * pageSize));

            sql.AppendLine();
            sql.Append($"ORDER BY p.\"CreatedAtUtc\" DESC\nLIMIT {limit} OFFSET {offset}");

            var products = await db.Products
                .FromSqlRaw(sql.ToString(), parameters.ToArray())
                .AsNoTracking()
                .ToListAsync(ct);

            await PopulateCategoriesAsync(products, ct);
            return products;
        }

        // Raw SQL: Mengambil seluruh data produk untuk kebutuhan admin
        public async Task<IReadOnlyList<Product>> ListAllAsync(CancellationToken ct)
        {
            var products = await db.Products
                .FromSqlRaw("""
                    SELECT 
                        p.*
                    FROM "Products" p
                    ORDER BY p."CreatedAtUtc" DESC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            await PopulateCategoriesAsync(products, ct);
            return products;
        }

        // Raw SQL: Mengambil produk berstatus aktif dengan pagination dan opsional filter nama & kategori
        public async Task<IReadOnlyList<Product>> ListActiveAsync(string? search, int? categoryId, int page, int pageSize, CancellationToken ct)
        {
            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var offset = (Math.Max(1, page) - 1) * pageSize;

            var products = await db.Products
                .FromSqlInterpolated($"""
                    SELECT 
                        p.*
                    FROM "Products" p
                    WHERE p."Status" = 'active'
                      AND ({term} IS NULL OR p."Name" ILIKE '%' || {term} || '%')
                      AND ({categoryId} IS NULL OR p."CategoryId" = {categoryId})
                    ORDER BY p."CreatedAtUtc" DESC
                    LIMIT {pageSize} 
                    OFFSET {offset}
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            await PopulateCategoriesAsync(products, ct);
            return products;
        }

        // Raw SQL: Mencari produk beserta data kategorinya berdasarkan Id
        public async Task<Product?> FindWithCategoryAsync(int id, CancellationToken ct)
        {
            var product = await db.Products
                .FromSqlInterpolated($"""
                    SELECT 
                        p.*
                    FROM "Products" p
                    WHERE p."Id" = {id}
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (product is not null)
            {
                product.Category = await db.Categories
                    .FromSqlInterpolated($"""
                        SELECT 
                            c."Id",
                            c."Name",
                            c."Description"
                        FROM "Categories" c
                        WHERE c."Id" = {product.CategoryId}
                        LIMIT 1
                        """)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);
            }

            return product;
        }

        // Raw SQL: Mencari produk berdasarkan Id
        public Task<Product?> FindAsync(int id, CancellationToken ct) =>
            db.Products
                .FromSqlInterpolated($"""
                    SELECT 
                        p.*
                    FROM "Products" p
                    WHERE p."Id" = {id}
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

        // Raw SQL: Mengecek apakah kategori produk valid
        public Task<bool> CategoryExistsAsync(int id, CancellationToken ct) =>
            db.Database
                .SqlQuery<bool>($"""
                    SELECT EXISTS (
                        SELECT 1 
                        FROM "Categories" 
                        WHERE "Id" = {id}
                    ) AS "Value"
                    """)
                .SingleAsync(ct);

        // Raw SQL: Mencari kategori produk berdasarkan Id atau Nama
        public Task<Category?> ResolveCategoryAsync(int id, string? name, CancellationToken ct) =>
            id > 0
                ? db.Categories
                    .FromSqlInterpolated($"""
                        SELECT 
                            c."Id",
                            c."Name",
                            c."Description"
                        FROM "Categories" c
                        WHERE c."Id" = {id}
                        LIMIT 1
                        """)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct)
                : db.Categories
                    .FromSqlInterpolated($"""
                        SELECT 
                            c."Id",
                            c."Name",
                            c."Description"
                        FROM "Categories" c
                        WHERE c."Name" = {name}
                        LIMIT 1
                        """)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);

        // Raw SQL: Menambahkan data produk baru
        public async Task<Product> CreateAsync(Product p, CancellationToken ct)
        {
            p.Id = await db.Database.SqlQuery<int>($"""
                INSERT INTO "Products" (
                    "LegacyId", 
                    "Name", 
                    "Description", 
                    "Price", 
                    "OldPrice", 
                    "Currency", 
                    "Stock", 
                    "Moq", 
                    "Sold", 
                    "Rating", 
                    "Reviews", 
                    "Weight", 
                    "Status", 
                    "Sku", 
                    "Sub", 
                    "Tags", 
                    "ImagesJson", 
                    "VariantsJson", 
                    "SpecsJson", 
                    "SellerId", 
                    "CategoryId", 
                    "CreatedAtUtc"
                )
                VALUES (
                    {p.LegacyId}, 
                    {p.Name}, 
                    {p.Description}, 
                    {p.Price}, 
                    {p.OldPrice}, 
                    {p.Currency}, 
                    {p.Stock}, 
                    {p.Moq}, 
                    {p.Sold}, 
                    {p.Rating}, 
                    {p.Reviews}, 
                    {p.Weight}, 
                    {p.Status}, 
                    {p.Sku}, 
                    {p.Sub}, 
                    {p.Tags}, 
                    CAST({p.ImagesJson} AS jsonb), 
                    CAST({p.VariantsJson} AS jsonb), 
                    CAST({p.SpecsJson} AS jsonb), 
                    {p.SellerId}, 
                    {p.CategoryId}, 
                    {p.CreatedAtUtc}
                )
                RETURNING "Id" AS "Value"
                """).SingleAsync(ct);

            return p;
        }

        // Raw SQL: Memperbarui data produk
        public async Task<bool> UpdateAsync(Product p, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Products" 
                SET "LegacyId"     = {p.LegacyId}, 
                    "Name"         = {p.Name}, 
                    "Description"  = {p.Description}, 
                    "Price"        = {p.Price}, 
                    "OldPrice"     = {p.OldPrice}, 
                    "Currency"     = {p.Currency}, 
                    "Stock"        = {p.Stock}, 
                    "Moq"          = {p.Moq}, 
                    "Sold"         = {p.Sold}, 
                    "Rating"       = {p.Rating}, 
                    "Reviews"      = {p.Reviews}, 
                    "Weight"       = {p.Weight}, 
                    "Status"       = {p.Status}, 
                    "Sku"          = {p.Sku}, 
                    "Sub"          = {p.Sub}, 
                    "Tags"         = {p.Tags}, 
                    "ImagesJson"   = CAST({p.ImagesJson} AS jsonb), 
                    "VariantsJson" = CAST({p.VariantsJson} AS jsonb), 
                    "SpecsJson"    = CAST({p.SpecsJson} AS jsonb), 
                    "SellerId"     = {p.SellerId}, 
                    "CategoryId"   = {p.CategoryId}
                WHERE "Id" = {p.Id}
                """, ct) > 0;

        // Raw SQL: Menghapus produk berdasarkan Id
        public async Task<bool> DeleteAsync(int id, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "Products"
                WHERE "Id" = {id}
                """, ct) > 0;

        // Raw SQL: Mengambil seluruh ulasan untuk suatu produk
        public async Task<IReadOnlyList<ProductReview>> ListReviewsAsync(int productId, CancellationToken ct) =>
            await db.ProductReviews
                .FromSqlInterpolated($"""
                    SELECT 
                        r.*
                    FROM "ProductReviews" r
                    WHERE r."ProductId" = {productId}
                    ORDER BY r."CreatedAtUtc" DESC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

        // Raw SQL: Menambahkan ulasan baru dan memperbarui rata-rata rating serta jumlah ulasan produk
        public async Task<ProductReview> CreateReviewAsync(ProductReview review, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            review.Id = await db.Database.SqlQuery<int>($"""
                INSERT INTO "ProductReviews" (
                    "ProductId", 
                    "UserId", 
                    "BuyerName", 
                    "BuyerCountry", 
                    "Rating", 
                    "Text", 
                    "Variant", 
                    "CreatedAtUtc"
                )
                VALUES (
                    {review.ProductId}, 
                    {review.UserId}, 
                    {review.BuyerName}, 
                    {review.BuyerCountry}, 
                    {review.Rating}, 
                    {review.Text}, 
                    {review.Variant}, 
                    {review.CreatedAtUtc}
                )
                RETURNING "Id" AS "Value"
                """).SingleAsync(ct);

            // Raw SQL: Sinkronisasi rating dan jumlah ulasan pada tabel Products
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Products" 
                SET "Reviews" = (SELECT COUNT(*)::int FROM "ProductReviews" WHERE "ProductId" = {review.ProductId}),
                    "Rating"  = (SELECT COALESCE(ROUND(AVG("Rating")::numeric, 1), 5.0) FROM "ProductReviews" WHERE "ProductId" = {review.ProductId})
                WHERE "Id" = {review.ProductId}
                """, ct);

            await transaction.CommitAsync(ct);
            return review;
        }
    }
}
