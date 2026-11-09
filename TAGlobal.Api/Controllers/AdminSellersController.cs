using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;

namespace TAGlobal.Api.Controllers;

[ApiController]
public sealed class AdminSellersController(AppDbContext db):ControllerBase
{
 [AllowAnonymous,HttpGet("/api/public/sellers")]
 public async Task<IActionResult> Public(CancellationToken ct)=>Ok((await db.Sellers.AsNoTracking().Where(s=>s.Status=="verified").OrderBy(s=>s.Id).ToListAsync(ct)).Select(ToResponse));
 [Authorize(Roles="admin"),HttpGet("/api/admin/sellers")]
 public async Task<IActionResult> Get(CancellationToken ct)=>Ok(await db.Sellers.AsNoTracking().OrderBy(s=>s.Id).Select(s=>new{id=$"s{s.Id}",dbId=s.Id,name=s.Name,owner=s.Owner,block=s.Block,email=s.Email,phone=s.Phone,country=s.Country,status=s.Status,joined=s.JoinedAtUtc.ToString("yyyy-MM-dd"),rating=0,reviews=0,products=db.Products.Count(p=>p.SellerId==s.Id),sales30d=0,responseRate=0,shipOnTime=0}).ToListAsync(ct));
 [Authorize(Roles="admin"),HttpPut("/api/admin/sellers/{id:int}")]
 public async Task<IActionResult> Update(int id,System.Text.Json.JsonElement b,CancellationToken ct){var s=await db.Sellers.FindAsync([id],ct);if(s is null)return NotFound();s.Name=Str(b,"name")??s.Name;s.Owner=Str(b,"owner")??s.Owner;s.Block=Str(b,"block")??s.Block;s.Email=Str(b,"email")??s.Email;s.Phone=Str(b,"phone")??s.Phone;s.Status=Str(b,"status")??s.Status;await db.SaveChangesAsync(ct);return Ok(ToResponse(s));}
 [Authorize(Roles="admin"),HttpDelete("/api/admin/sellers/{id:int}")]
 public async Task<IActionResult> Delete(int id,CancellationToken ct){var s=await db.Sellers.FindAsync([id],ct);if(s is null)return NotFound();db.Sellers.Remove(s);await db.SaveChangesAsync(ct);return NoContent();}
 private static object ToResponse(TAGlobal.Api.Models.Seller s)=>new{id=$"s{s.Id}",dbId=s.Id,name=s.Name,owner=s.Owner,block=s.Block,email=s.Email,phone=s.Phone,country=s.Country,status=s.Status,joined=s.JoinedAtUtc.ToString("yyyy-MM-dd"),rating=0,reviews=0,products=0,sales30d=0,responseRate=0,shipOnTime=0};
 private static string? Str(System.Text.Json.JsonElement e,string k)=>e.TryGetProperty(k,out var v)&&v.ValueKind==System.Text.Json.JsonValueKind.String?v.GetString():null;
}
