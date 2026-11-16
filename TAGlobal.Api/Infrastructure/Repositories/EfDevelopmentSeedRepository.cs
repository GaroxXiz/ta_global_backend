using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.Abstractions
{
    public interface IDevelopmentSeedRepository
    {
        Task EnsureTablesCreatedAsync(CancellationToken ct);
        Task AddCategoryIfMissingAsync(int id, string name, string? description, CancellationToken ct);
        Task AddSellerIfMissingAsync(Seller seller, CancellationToken ct);
        Task AddSettingIfMissingAsync(string key, object value, CancellationToken ct);
    }
}

namespace TAGlobal.Api.Infrastructure.Repositories
{
    using TAGlobal.Api.Application.Abstractions;

    public sealed class EfDevelopmentSeedRepository(AppDbContext db) : IDevelopmentSeedRepository
    {
        // Raw SQL: Memastikan tabel-tabel baru (ProductReviews, DropoffManifests) dibuat jika belum ada di database
        public async Task EnsureTablesCreatedAsync(CancellationToken ct)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ProductReviews" (
                    "Id" SERIAL PRIMARY KEY,
                    "ProductId" INTEGER NOT NULL REFERENCES "Products"("Id") ON DELETE CASCADE,
                    "UserId" INTEGER NOT NULL,
                    "BuyerName" VARCHAR(200) NOT NULL,
                    "BuyerCountry" VARCHAR(100) DEFAULT 'Indonesia',
                    "Rating" INTEGER NOT NULL DEFAULT 5,
                    "Text" VARCHAR(2000),
                    "Variant" VARCHAR(200),
                    "CreatedAtUtc" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS "DropoffManifests" (
                    "Id" SERIAL PRIMARY KEY,
                    "ManifestCode" VARCHAR(50) NOT NULL UNIQUE,
                    "SellerId" INTEGER NOT NULL,
                    "HubId" VARCHAR(50) DEFAULT 'h2',
                    "Slot" VARCHAR(100),
                    "OrderIdsJson" JSONB DEFAULT '[]',
                    "TotalOrders" INTEGER DEFAULT 0,
                    "TotalWeight" NUMERIC(10, 2) DEFAULT 0,
                    "CreatedAtUtc" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
                );
                """, ct);
        }

        // Raw SQL: Menambahkan data awal kategori jika belum tersedia (idempotent seed)
        public async Task AddCategoryIfMissingAsync(int id, string name, string? description, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Categories" (
                    "Id", 
                    "Name", 
                    "Description"
                ) 
                VALUES (
                    {id}, 
                    {name}, 
                    {description}
                ) 
                ON CONFLICT ("Id") DO NOTHING
                """, ct);

        // Raw SQL: Menambahkan data awal seller toko jika belum tersedia
        public async Task AddSellerIfMissingAsync(Seller seller, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Sellers" (
                    "Id", 
                    "Name", 
                    "Owner", 
                    "Block", 
                    "Email", 
                    "Phone", 
                    "Country", 
                    "Status", 
                    "JoinedAtUtc"
                )
                VALUES (
                    {seller.Id}, 
                    {seller.Name}, 
                    {seller.Owner}, 
                    {seller.Block}, 
                    {seller.Email}, 
                    {seller.Phone}, 
                    {seller.Country}, 
                    {seller.Status}, 
                    {seller.JoinedAtUtc}
                )
                ON CONFLICT ("Id") DO NOTHING
                """, ct);

        // Raw SQL: Menambahkan konfigurasi default marketplace (JSONB) jika belum ada
        public async Task AddSettingIfMissingAsync(string key, object value, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(value);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Settings" (
                    "Key", 
                    "ValueJson"
                ) 
                VALUES (
                    {key}, 
                    CAST({json} AS jsonb)
                ) 
                ON CONFLICT ("Key") DO NOTHING
                """, ct);
        }
    }
}
