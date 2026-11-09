using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;
using System.Text.Json;
namespace TAGlobal.Api.Controllers;
[ApiController,Route("api/admin")]
public sealed class AdminOperationsController(AppDbContext db):ControllerBase
{
 [Authorize(Roles="admin"),HttpGet("orders")]
 public async Task<IActionResult> Orders(CancellationToken ct)=>Ok(await db.Orders.AsNoTracking().Include(x=>x.Items).OrderByDescending(x=>x.PlacedAtUtc).ToListAsync(ct));
 [Authorize(Roles="admin"),HttpGet("messages")]
 public async Task<IActionResult> Threads(CancellationToken ct)=>Ok(await db.Threads.AsNoTracking().Include(x=>x.Messages).OrderByDescending(x=>x.UpdatedAtUtc).ToListAsync(ct));
 [Authorize(Roles="admin"),HttpPut("messages/{id:int}")]
 public async Task<IActionResult> MarkRead(int id,CancellationToken ct){var t=await db.Threads.FindAsync([id],ct);if(t is null)return NotFound();t.Unread=0;await db.SaveChangesAsync(ct);return NoContent();}
 [Authorize(Roles="admin"),HttpPost("messages")]
 public async Task<IActionResult> CreateThread(JsonElement b,CancellationToken ct){var t=new ChatThread{ThreadKey=Guid.NewGuid().ToString("N"),With=Str(b,"with")??"Seller",Role=Str(b,"role")??"seller",Subject=Str(b,"subject")??"Pesan dari Admin",Party=Str(b,"party")??"admin"};t.Messages.Add(new ChatMessage{FromUserId="admin",Text=Str(b,"text")??"Halo."});db.Threads.Add(t);await db.SaveChangesAsync(ct);return Ok(t);}
 [Authorize(Roles="admin"),HttpPost("broadcast")]
 public async Task<IActionResult> Broadcast(JsonElement b,CancellationToken ct){var text=Str(b,"message")??Str(b,"text")??"";var subject=Str(b,"subject")??"Siaran TAGlobal";var users=await db.Users.AsNoTracking().Select(x=>new{x.Id,x.SellerId}).ToListAsync(ct);var audience=Str(b,"audience");foreach(var u in users.Where(x=>audience=="sellers"?x.SellerId!=null:x.SellerId==null)){var t=new ChatThread{ThreadKey=Guid.NewGuid().ToString("N"),With="Admin TAGlobal",Role="admin",Subject=subject,Party=$"admin:{u.Id}"};t.Messages.Add(new ChatMessage{FromUserId="admin",Text=text});db.Threads.Add(t);}await db.SaveChangesAsync(ct);return Ok(new{delivered=users.Count(x=>audience=="sellers"?x.SellerId!=null:x.SellerId==null)});}
 [AllowAnonymous,HttpGet("providers")] public async Task<IActionResult> Providers(CancellationToken ct)=>await Get("providers",ct);
 [Authorize(Roles="admin"),HttpPut("providers")] public async Task<IActionResult> PutProviders(JsonElement b,CancellationToken ct)=>await Save("providers",b,ct);
 [AllowAnonymous,HttpGet("hubs")] public async Task<IActionResult> Hubs(CancellationToken ct)=>await Get("hubs",ct);
 [Authorize(Roles="admin"),HttpPut("hubs")] public async Task<IActionResult> PutHubs(JsonElement b,CancellationToken ct)=>await Save("hubs",b,ct);
 [AllowAnonymous,HttpGet("vouchers")] public async Task<IActionResult> Vouchers(CancellationToken ct)=>await Get("vouchers",ct);
 [Authorize(Roles="admin"),HttpPut("vouchers")] public async Task<IActionResult> PutVouchers(JsonElement b,CancellationToken ct)=>await Save("vouchers",b,ct);
 [Authorize(Roles="admin"),HttpGet("dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>Ok(new{products=await db.Products.CountAsync(ct),sellers=await db.Sellers.CountAsync(ct),orders=await db.Orders.CountAsync(ct),gmv=await db.Orders.SumAsync(x=>(decimal?)x.Total,ct)??0});
 private async Task<IActionResult> Get(string k,CancellationToken ct){var s=await db.Settings.AsNoTracking().SingleOrDefaultAsync(x=>x.Key==k,ct);return s is null?NotFound():Content(s.ValueJson,"application/json");}
 private async Task<IActionResult> Save(string k,JsonElement b,CancellationToken ct){var s=await db.Settings.SingleOrDefaultAsync(x=>x.Key==k,ct);if(s is null)db.Settings.Add(new MarketplaceSetting{Key=k,ValueJson=b.GetRawText()});else s.ValueJson=b.GetRawText();await db.SaveChangesAsync(ct);return Ok(b);}
 private static string? Str(JsonElement e,string k)=>e.TryGetProperty(k,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString():null;
}
