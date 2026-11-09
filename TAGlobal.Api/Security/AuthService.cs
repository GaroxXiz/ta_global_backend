using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Security;

public sealed class AuthService(AppDbContext db,IConfiguration configuration)
{
 public async Task<AuthResponse> RegisterAsync(RegisterRequest r,CancellationToken ct){var username=r.Username.Trim();var email=r.Email.Trim().ToLowerInvariant();if(await db.Users.AnyAsync(x=>x.Username.ToLower()==username.ToLower(),ct))throw new InvalidOperationException("Username sudah digunakan.");if(await db.Users.AnyAsync(x=>x.Email==email,ct))throw new InvalidOperationException("Email sudah digunakan.");var u=new User{Username=username,Email=email,Name=r.Name.Trim(),Country=r.Country.Trim(),PasswordHash=BCrypt.Net.BCrypt.HashPassword(r.Password),Role="buyer"};if(!string.IsNullOrWhiteSpace(r.StoreName))u.Seller=new Seller{Name=r.StoreName.Trim(),Owner=u.Name,Email=u.Email,Country=u.Country};db.Users.Add(u);await db.SaveChangesAsync(ct);return CreateResponse(u);}
 public async Task<AuthResponse> LoginAsync(LoginRequest r,CancellationToken ct){var identifier=r.Email.Trim().ToLowerInvariant();var u=await db.Users.Include(x=>x.Seller).SingleOrDefaultAsync(x=>x.Email.ToLower()==identifier||x.Username.ToLower()==identifier,ct);if(u is null||!BCrypt.Net.BCrypt.Verify(r.Password,u.PasswordHash))throw new UnauthorizedAccessException("Email/username atau kata sandi salah.");return CreateResponse(u);}
 private AuthResponse CreateResponse(User u){var secret=configuration["Jwt:Secret"];if(string.IsNullOrWhiteSpace(secret)||Encoding.UTF8.GetByteCount(secret)<32)throw new InvalidOperationException("JWT signing secret belum dikonfigurasi. Isi Jwt__Secret di .env (minimal 32 byte).");var sid=u.SellerId?.ToString()??"";var claims=new[]{new Claim(JwtRegisteredClaimNames.Sub,u.Id.ToString()),new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim("id",u.Id.ToString()),new Claim(JwtRegisteredClaimNames.UniqueName,u.Username),new Claim(JwtRegisteredClaimNames.Email,u.Email),new Claim(ClaimTypes.Name,u.Name),new Claim("name",u.Name),new Claim(ClaimTypes.Role,u.Role),new Claim("role",u.Role),new Claim("sellerId",sid)};var creds=new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),SecurityAlgorithms.HmacSha256);var token=new JwtSecurityToken(configuration["Jwt:Issuer"]??"TAGlobal.Api",configuration["Jwt:Audience"]??"TAGlobal.Frontend",claims,expires:DateTime.UtcNow.AddHours(12),signingCredentials:creds);var user=new AuthUserResponse(u.Id,u.Username,u.Name,u.Email,u.Country,u.Role,u.SellerId is int s?$"s{s}":null,u.Seller?.Name);return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token),user);}
}

