using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Security;

public static class BuyerProvisioner
{
    public static async Task CreateAsync(
        AppDbContext db,
        string name,
        string username,
        string email,
        string password,
        CancellationToken ct = default)
    {
        name = name.Trim();
        username = username.Trim();
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Name, username, email, and password are required.");
        if (password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.");
        if (await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower() || u.Email.ToLower() == email, ct))
            throw new InvalidOperationException("A user with that username or email already exists.");

        db.Users.Add(new User
        {
            Name = name,
            Username = username,
            Email = email,
            Country = "Indonesia",
            Role = "buyer",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        });
        await db.SaveChangesAsync(ct);
    }
}
