using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Security;

public static class BuyerProvisioner
{
    public static Task CreateAsync(IUserRepository users, string name, string username, string email, string password, CancellationToken ct = default)
    {
        name = name.Trim(); username = username.Trim(); email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Name, username, email, and password are required.");
        if (password.Length < 8) throw new ArgumentException("Password must be at least 8 characters long.");
        var user = new User { Name = name, Username = username, Email = email, Country = "Indonesia", Role = "buyer", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) };
        return users.ProvisionBuyerAsync(user, ct);
    }
}
