using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.UseCases;

public interface IAuthUseCases
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthUserResponse?> GetCurrentUserAsync(int id, CancellationToken ct);
}

public sealed class AuthUseCases(IUserRepository users, IConfiguration configuration) : IAuthUseCases
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.UsernameExistsAsync(username, ct)) throw new InvalidOperationException("Username sudah digunakan.");
        if (await users.EmailExistsAsync(email, ct)) throw new InvalidOperationException("Email sudah digunakan.");

        var user = new User { Username = username, Email = email, Name = request.Name.Trim(), Country = request.Country.Trim(), PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), Role = "buyer" };
        if (!string.IsNullOrWhiteSpace(request.StoreName)) user.Seller = new Seller { Name = request.StoreName.Trim(), Owner = user.Name, Email = user.Email, Country = user.Country };
        await users.CreateAsync(user, ct);
        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var identifier = request.Email.Trim().ToLowerInvariant();
        var user = await users.FindByIdentifierAsync(identifier, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) throw new UnauthorizedAccessException("Email/username atau kata sandi salah.");
        return CreateResponse(user);
    }

    public async Task<AuthUserResponse?> GetCurrentUserAsync(int id, CancellationToken ct)
    {
        var user = await users.FindByIdWithSellerAsync(id, ct);
        return user is null ? null : ToResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("JWT signing secret belum dikonfigurasi. Isi Jwt__Secret di .env (minimal 32 byte).");
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim("id", user.Id.ToString()), new Claim(JwtRegisteredClaimNames.UniqueName, user.Username), new Claim(JwtRegisteredClaimNames.Email, user.Email), new Claim(ClaimTypes.Name, user.Name), new Claim("name", user.Name), new Claim(ClaimTypes.Role, user.Role), new Claim("role", user.Role), new Claim("sellerId", user.SellerId?.ToString() ?? "") };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"] ?? "TAGlobal.Api", configuration["Jwt:Audience"] ?? "TAGlobal.Frontend", claims, expires: DateTime.UtcNow.AddHours(12), signingCredentials: credentials);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), ToResponse(user));
    }

    private static AuthUserResponse ToResponse(User user) => new(user.Id, user.Username, user.Name, user.Email, user.Country, user.Role, user.SellerId is int id ? $"s{id}" : null, user.Seller?.Name);
}
