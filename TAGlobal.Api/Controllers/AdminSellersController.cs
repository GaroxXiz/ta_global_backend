using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;

namespace TAGlobal.Api.Controllers;

[ApiController]
public sealed class AdminSellersController(ISellerUseCases useCases) : ControllerBase
{
    [AllowAnonymous, HttpGet("/api/public/sellers")]
    public async Task<IActionResult> Public(CancellationToken ct) => Ok(await useCases.ListPublicAsync(ct));

    [Authorize(Roles = "admin"), HttpGet("/api/admin/sellers")]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await useCases.ListAdminAsync(ct));

    [Authorize(Roles = "admin"), HttpPut("/api/admin/sellers/{id:int}")]
    public async Task<IActionResult> Update(int id, System.Text.Json.JsonElement body, CancellationToken ct)
    {
        var seller = await useCases.UpdateAsync(id, body, includeStatus: true, ct);
        return seller is null ? NotFound() : Ok(seller);
    }

    [Authorize(Roles = "admin"), HttpDelete("/api/admin/sellers/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await useCases.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
