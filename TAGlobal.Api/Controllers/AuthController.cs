using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Data;
using TAGlobal.Api.Security;

namespace TAGlobal.Api.Controllers;

[ApiController,Route("api/auth")]
public sealed class AuthController(AuthService auth,AppDbContext db):ControllerBase
{
 [HttpPost("register")]
 public async Task<ActionResult<AuthResponse>> Register(RegisterRequest r,CancellationToken ct){if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Username)||string.IsNullOrWhiteSpace(r.Email)||r.Password.Length<8)return BadRequest(new ProblemDetails{Title="Data tidak valid",Detail="Nama, username, email wajib diisi; password minimal 8 karakter."});try{return StatusCode(201,await auth.RegisterAsync(r,ct));}catch(InvalidOperationException e){return Conflict(new ProblemDetails{Title="Akun sudah terdaftar",Detail=e.Message});}}
 [HttpPost("login")]
 public async Task<ActionResult<AuthResponse>> Login(LoginRequest r,CancellationToken ct){try{return Ok(await auth.LoginAsync(r,ct));}catch(UnauthorizedAccessException e){return Unauthorized(new ProblemDetails{Title="Login gagal",Detail=e.Message});}}
 [Authorize,HttpGet("me")]
 public async Task<ActionResult<AuthUserResponse>> Me(CancellationToken ct){if(!int.TryParse(User.FindFirst("sub")?.Value,out var id))return Unauthorized();var u=await db.Users.Include(x=>x.Seller).SingleOrDefaultAsync(x=>x.Id==id,ct);return u is null?Unauthorized():Ok(new AuthUserResponse(u.Id,u.Username,u.Name,u.Email,u.Country,u.Role,u.SellerId is int sid?$"s{sid}":null,u.Seller?.Name));}
}
