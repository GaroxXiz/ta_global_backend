using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;
using TAGlobal.Api.Contracts;

namespace TAGlobal.Api.Controllers;

[ApiController, Route("api/catalog/products")]
public sealed class ProductsController(IProductUseCases useCases) : ControllerBase
{
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] bool? isActive,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await useCases.ListAsync(search, categoryId, page, pageSize, ct));

    [AllowAnonymous, HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id, CancellationToken ct)
    {
        var product = await useCases.GetAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "admin"), HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await useCases.CreateAsync(request, ct);
        return product is null ? BadRequest() : Created($"/api/catalog/products/{product.Id}", product.Response);
    }

    [Authorize(Roles = "admin"), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await useCases.UpdateAsync(id, request, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = "admin"), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await useCases.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
