using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;
using TAGlobal.Api.Contracts;

namespace TAGlobal.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthUseCases useCases) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || request.Password.Length < 8)
            return BadRequest(new ProblemDetails { Title = "Data tidak valid", Detail = "Nama, username, email wajib diisi; password minimal 8 karakter." });
        try { return StatusCode(201, await useCases.RegisterAsync(request, ct)); }
        catch (InvalidOperationException e) { return Conflict(new ProblemDetails { Title = "Akun sudah terdaftar", Detail = e.Message }); }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try { return Ok(await useCases.LoginAsync(request, ct)); }
        catch (UnauthorizedAccessException e) { return Unauthorized(new ProblemDetails { Title = "Login gagal", Detail = e.Message }); }
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me(CancellationToken ct)
    {
        var subject = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!int.TryParse(subject, out var id)) return Unauthorized();
        var user = await useCases.GetCurrentUserAsync(id, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}
