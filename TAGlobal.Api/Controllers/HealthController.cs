using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;

namespace TAGlobal.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    [AllowAnonymous,HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseAvailable = await db.Database.CanConnectAsync(cancellationToken);
        return databaseAvailable
            ? Ok(new { status = "ok", database = "connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "degraded", database = "unavailable" });
    }
}

