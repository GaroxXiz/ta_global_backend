using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;

namespace TAGlobal.Api.Controllers;

[ApiController, Route("api/health")]
public sealed class HealthController(IHealthUseCases useCases) : ControllerBase
{
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        await useCases.IsDatabaseAvailableAsync(cancellationToken)
            ? Ok(new { status = "ok", database = "connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "degraded", database = "unavailable" });
}
