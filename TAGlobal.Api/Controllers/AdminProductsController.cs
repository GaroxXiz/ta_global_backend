using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;

namespace TAGlobal.Api.Controllers;

[ApiController, Route("api/admin/products"), Authorize(Roles = "admin")]
public sealed class AdminProductsController(IProductUseCases useCases) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await useCases.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await useCases.ListAllForAdminAsync(ct));
}
