using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;
namespace TAGlobal.Api.Controllers;
[ApiController,Route("api/categories")]
public sealed class CategoriesController(AppDbContext db):ControllerBase
{
 [AllowAnonymous,HttpGet]
 public async Task<IActionResult> Get(CancellationToken ct)=>Ok(await db.Categories.AsNoTracking().OrderBy(x=>x.Id).Select(x=>new{id=x.Id,name=x.Name,description=x.Description}).ToListAsync(ct));
 [AllowAnonymous,HttpGet("{id:int}")]
 public async Task<IActionResult> GetById(int id,CancellationToken ct){var c=await db.Categories.FindAsync([id],ct);return c is null?NotFound():Ok(new{id=c.Id,name=c.Name,description=c.Description});}
 [Authorize(Roles="admin"),HttpPost]
 public async Task<IActionResult> Create(CreateCategoryRequest r,CancellationToken ct){var c=new Category{Name=r.Name.Trim(),Description=r.Description};db.Categories.Add(c);await db.SaveChangesAsync(ct);return CreatedAtAction(nameof(GetById),new{id=c.Id},new{id=c.Id,name=c.Name,description=c.Description});}
 [Authorize(Roles="admin"),HttpDelete("{id:int}")]
 public async Task<IActionResult> Delete(int id,CancellationToken ct){var c=await db.Categories.FindAsync([id],ct);if(c is null)return NotFound();db.Categories.Remove(c);await db.SaveChangesAsync(ct);return NoContent();}
}
