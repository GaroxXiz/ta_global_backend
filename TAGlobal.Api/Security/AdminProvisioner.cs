using TAGlobal.Api.Application.Abstractions;

namespace TAGlobal.Api.Security;

public static class AdminProvisioner
{
    public static Task EnsureSuperadminAsync(IUserRepository users, string username, string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Admin username, email, and password are required.");
        return users.EnsureSuperadminAsync(username.Trim(), email.Trim().ToLowerInvariant(), BCrypt.Net.BCrypt.HashPassword(password), ct);
    }
}
