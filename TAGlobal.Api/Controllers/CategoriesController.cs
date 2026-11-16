using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;
using TAGlobal.Api.Contracts;

namespace TAGlobal.Api.Controllers;

[ApiController, Route("api/categories")]
public sealed class CategoriesController(ICategoryUseCases useCases) : ControllerBase
{
    [AllowAnonymous, HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> Get(CancellationToken ct) => Ok(await useCases.ListAsync(ct));

    [AllowAnonymous, HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var category = await useCases.GetAsync(id, ct);
        return category is null ? NotFound() : Ok(category);
    }

    [Authorize(Roles = "admin"), HttpPost]
    public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await useCases.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [Authorize(Roles = "admin"), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await useCases.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
