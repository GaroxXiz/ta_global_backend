using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.Abstractions
{
    public interface IUserRepository
    {
        Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
        Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
        Task<User?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken);
        Task<User?> FindByIdWithSellerAsync(int id, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(User user, CancellationToken cancellationToken);
        Task EnsureSuperadminAsync(string username, string email, string passwordHash, CancellationToken cancellationToken);
        Task ProvisionBuyerAsync(User user, CancellationToken cancellationToken);
        Task<User> CreateAsync(User user, CancellationToken cancellationToken);
    }
}

namespace TAGlobal.Api.Infrastructure.Repositories
{
    using TAGlobal.Api.Application.Abstractions;

    public sealed class EfUserRepository(AppDbContext db) : IUserRepository
    {
        // Raw SQL: Mengecek apakah username sudah terdaftar (case-insensitive)
        public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) =>
            db.Database.SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 
                    FROM "Users" u
                    WHERE LOWER(u."Username") = LOWER({username})
                ) AS "Value"
                """).SingleAsync(ct);

        // Raw SQL: Mengecek apakah email sudah terdaftar (case-insensitive)
        public Task<bool> EmailExistsAsync(string email, CancellationToken ct) =>
            db.Database.SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 
                    FROM "Users" u
                    WHERE LOWER(u."Email") = LOWER({email})
                ) AS "Value"
                """).SingleAsync(ct);

        // Raw SQL: Mencari user berdasarkan username atau email, beserta data toko (Seller) jika ada
        public async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
        {
            var user = await db.Users
                .FromSqlInterpolated($"""
                    SELECT 
                        u.* 
                    FROM "Users" u
                    WHERE LOWER(u."Email")    = LOWER({identifier}) 
                       OR LOWER(u."Username") = LOWER({identifier}) 
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (user is not null && user.SellerId.HasValue)
            {
                user.Seller = await db.Sellers
                    .FromSqlInterpolated($"""
                        SELECT 
                            s.*
                        FROM "Sellers" s
                        WHERE s."Id" = {user.SellerId.Value}
                        LIMIT 1
                        """)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);
            }

            return user;
        }

        // Raw SQL: Mencari user berdasarkan Id, beserta data toko (Seller) jika ada
        public async Task<User?> FindByIdWithSellerAsync(int id, CancellationToken ct)
        {
            var user = await db.Users
                .FromSqlInterpolated($"""
                    SELECT 
                        u.*
                    FROM "Users" u
                    WHERE u."Id" = {id}
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (user is not null && user.SellerId.HasValue)
            {
                user.Seller = await db.Sellers
                    .FromSqlInterpolated($"""
                        SELECT 
                            s.*
                        FROM "Sellers" s
                        WHERE s."Id" = {user.SellerId.Value}
                        LIMIT 1
                        """)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct);
            }

            return user;
        }

        // Raw SQL: Memperbarui data profil, keranjang belanja, wishlist, dan pengaturan seller user
        public async Task<bool> UpdateAsync(User user, CancellationToken ct) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Users" 
                SET "Name"               = {user.Name}, 
                    "Email"              = {user.Email}, 
                    "Country"            = {user.Country}, 
                    "CartJson"           = {user.CartJson}, 
                    "WishlistJson"       = {user.WishlistJson}, 
                    "SellerShippingJson" = {user.SellerShippingJson}, 
                    "SellerHubId"        = {user.SellerHubId}, 
                    "HandlingDays"       = {user.HandlingDays}
                WHERE "Id" = {user.Id}
                """, ct) > 0;

        // Raw SQL: Menambahkan data user baru beserta Seller jika user mendaftar sebagai toko
        public async Task<User> CreateAsync(User user, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            if (user.Seller is not null)
            {
                user.SellerId = await db.Database.SqlQuery<int>($"""
                    INSERT INTO "Sellers" (
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
                        {user.Seller.Name}, 
                        {user.Seller.Owner}, 
                        {user.Seller.Block}, 
                        {user.Seller.Email}, 
                        {user.Seller.Phone}, 
                        {user.Seller.Country}, 
                        {user.Seller.Status}, 
                        {user.Seller.JoinedAtUtc}
                    )
                    RETURNING "Id" AS "Value"
                    """).SingleAsync(ct);

                user.Seller.Id = user.SellerId.Value;
            }

            user.Id = await db.Database.SqlQuery<int>($"""
                INSERT INTO "Users" (
                    "Username", 
                    "Email", 
                    "Name", 
                    "Country", 
                    "PasswordHash", 
                    "Role", 
                    "SellerId", 
                    "CartJson", 
                    "WishlistJson", 
                    "SellerShippingJson", 
                    "SellerHubId", 
                    "HandlingDays"
                )
                VALUES (
                    {user.Username}, 
                    {user.Email}, 
                    {user.Name}, 
                    {user.Country}, 
                    {user.PasswordHash}, 
                    {user.Role}, 
                    {user.SellerId}, 
                    {user.CartJson}, 
                    {user.WishlistJson}, 
                    {user.SellerShippingJson}, 
                    {user.SellerHubId}, 
                    {user.HandlingDays}
                )
                RETURNING "Id" AS "Value"
                """).SingleAsync(ct);

            await transaction.CommitAsync(ct);
            return user;
        }

        // Raw SQL: Memastikan akun superadmin ada di database (idempotent seed)
        public async Task EnsureSuperadminAsync(string username, string email, string passwordHash, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var matches = await db.Users
                .FromSqlInterpolated($"""
                    SELECT 
                        u.* 
                    FROM "Users" u
                    WHERE LOWER(u."Username") = LOWER({username}) 
                       OR LOWER(u."Email")    = LOWER({email}) 
                    ORDER BY u."Id" ASC
                    """)
                .AsNoTracking()
                .ToListAsync(ct);

            var current = matches.FirstOrDefault();
            int userId;

            if (current is not null)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM "Users" 
                    WHERE (LOWER("Username") = LOWER({username}) OR LOWER("Email") = LOWER({email})) 
                      AND "Id" <> {current.Id}
                    """, ct);
            }

            if (current is null)
            {
                userId = await db.Database.SqlQuery<int>($"""
                    INSERT INTO "Users" (
                        "Username", 
                        "Email", 
                        "Name", 
                        "Country", 
                        "PasswordHash", 
                        "Role", 
                        "SellerId", 
                        "HandlingDays"
                    )
                    VALUES (
                        {username}, 
                        {email}, 
                        {"TAGlobal Superadmin"}, 
                        {"Indonesia"}, 
                        {passwordHash}, 
                        {"admin"}, 
                        NULL, 
                        2
                    )
                    RETURNING "Id" AS "Value"
                    """).SingleAsync(ct);
            }
            else
            {
                userId = current.Id;
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Users" 
                    SET "Username"     = {username}, 
                        "Email"        = {email}, 
                        "Name"         = {"TAGlobal Superadmin"}, 
                        "Country"      = {"Indonesia"}, 
                        "PasswordHash" = {passwordHash}, 
                        "Role"         = {"admin"}, 
                        "SellerId"     = NULL
                    WHERE "Id" = {userId}
                    """, ct);
            }

            await transaction.CommitAsync(ct);
        }

        // Raw SQL: Provisioning akun buyer baru setelah validasi keunikan username/email
        public async Task ProvisionBuyerAsync(User user, CancellationToken ct)
        {
            if (await UsernameExistsAsync(user.Username, ct) || await EmailExistsAsync(user.Email, ct))
                throw new InvalidOperationException("A user with that username or email already exists.");
            await CreateAsync(user, ct);
        }
    }
}
