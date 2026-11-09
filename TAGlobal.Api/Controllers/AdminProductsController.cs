using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;

namespace TAGlobal.Api.Controllers;

[ApiController,Route("api/admin/products"),Authorize(Roles="admin")]
public sealed class AdminProductsController(AppDbContext db):ControllerBase
{
 [HttpDelete("{id:int}")]
 public async Task<IActionResult> Delete(int id,CancellationToken ct){var p=await db.Products.FindAsync([id],ct);if(p is null)return NotFound();db.Products.Remove(p);await db.SaveChangesAsync(ct);return NoContent();}
 [HttpGet]
 public async Task<IActionResult> Get(CancellationToken ct)=>Ok(await db.Products.AsNoTracking().Include(p=>p.Category).OrderByDescending(p=>p.CreatedAtUtc).Select(p=>new{id=p.LegacyId??$"pr{p.Id:0000}",dbId=p.Id,legacyId=p.LegacyId,sellerId=p.SellerId==null?null:$"s{p.SellerId}",title=p.Name,category=p.Category!.Name,sub=p.Sub,price=p.Price,oldPrice=p.OldPrice,moq=p.Moq,sold=p.Sold,rating=p.Rating,reviews=p.Reviews,stock=p.Stock,weight=p.Weight,status=p.Status,tags=p.Tags,sku=p.Sku,desc=p.Description,images=JsonDoc(p.ImagesJson),variants=p.VariantsJson,specs=p.SpecsJson}).ToListAsync(ct));
 private static object JsonDoc(string value)=>System.Text.Json.JsonSerializer.Deserialize<object>(value)??Array.Empty<object>();
}
