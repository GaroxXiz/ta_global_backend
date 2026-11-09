using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;
namespace TAGlobal.Api.Controllers;
[ApiController,Route("api/catalog/products")]
public sealed class ProductsController(AppDbContext db):ControllerBase
{
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 [AllowAnonymous,HttpGet]
 public async Task<IActionResult> Get([FromQuery]string? search,[FromQuery]int? categoryId,[FromQuery]bool? isActive,[FromQuery,Range(1,int.MaxValue)]int page=1,[FromQuery,Range(1,100)]int pageSize=20,CancellationToken ct=default){var q=db.Products.AsNoTracking().Include(x=>x.Category).Where(x=>x.Status=="active");if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>EF.Functions.ILike(x.Name,$"%{search.Trim()}%"));if(categoryId.HasValue)q=q.Where(x=>x.CategoryId==categoryId.Value);var rows=await q.OrderByDescending(x=>x.CreatedAtUtc).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);return Ok(rows.Select(Map));}
 [AllowAnonymous,HttpGet("{id:int}")]
 public async Task<IActionResult> GetOne(int id,CancellationToken ct){var p=await db.Products.AsNoTracking().Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==id,ct);return p is null?NotFound():Ok(Map(p));}
 [Authorize(Roles="admin"),HttpPost]
 public async Task<IActionResult> Create(CreateProductRequest r,CancellationToken ct){var cat=await db.Categories.FindAsync([r.CategoryId],ct);if(cat is null)return BadRequest();var p=new Product{Name=r.Name,Description=r.Description,Price=r.Price,Currency=r.Currency,Stock=r.Stock,CategoryId=r.CategoryId,Status="active"};db.Products.Add(p);await db.SaveChangesAsync(ct);return CreatedAtAction(nameof(GetOne),new{id=p.Id},Map(p));}
 [Authorize(Roles="admin"),HttpPut("{id:int}")]
 public async Task<IActionResult> Update(int id,UpdateProductRequest r,CancellationToken ct){var p=await db.Products.FindAsync([id],ct);if(p is null)return NotFound();p.Name=r.Name;p.Description=r.Description;p.Price=r.Price;p.Currency=r.Currency;p.Stock=r.Stock;p.CategoryId=r.CategoryId;p.Status=r.IsActive?"active":"draft";await db.SaveChangesAsync(ct);return Ok(Map(p));}
 [Authorize(Roles="admin"),HttpDelete("{id:int}")]
 public async Task<IActionResult> Delete(int id,CancellationToken ct){var p=await db.Products.FindAsync([id],ct);if(p is null)return NotFound();db.Products.Remove(p);await db.SaveChangesAsync(ct);return NoContent();}
 private static object Map(Product p)=>new{id=p.LegacyId??$"pr{p.Id:0000}",dbId=p.Id,legacyId=p.LegacyId,sellerId=p.SellerId is int s?$"s{s}":null,title=p.Name,category=p.Category?.Name,categoryId=p.CategoryId,sub=p.Sub,price=p.Price,oldPrice=p.OldPrice,moq=p.Moq,sold=p.Sold,rating=p.Rating,reviews=p.Reviews,stock=p.Stock,weight=p.Weight,status=p.Status,tags=p.Tags,sku=p.Sku,desc=p.Description,images=JsonSerializer.Deserialize<object[]>(p.ImagesJson,Json)??[],variants=JsonSerializer.Deserialize<object[]>(p.VariantsJson,Json)??[],specs=JsonSerializer.Deserialize<object[]>(p.SpecsJson,Json)??[]};
}

