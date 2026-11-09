using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;
namespace TAGlobal.Api.Controllers;
[ApiController,Route("api")]
public sealed class MarketplaceController(AppDbContext db):ControllerBase
{
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);private int UserId=>int.TryParse(User.FindFirst("sub")?.Value,out var id)?id:0;private bool IsAdmin=>User.IsInRole("admin");private int? SellerId=>int.TryParse(User.FindFirst("sellerId")?.Value,out var id)?id:null;
 [AllowAnonymous,HttpGet("products")]
 public async Task<IActionResult> Products([FromQuery]string? search,[FromQuery]string? category,[FromQuery]int? sellerId,[FromQuery]string? status,[FromQuery]decimal? minPrice,[FromQuery]decimal? maxPrice,[FromQuery]int page=1,[FromQuery]int pageSize=100,CancellationToken ct=default){var q=db.Products.AsNoTracking().Include(p=>p.Category).AsQueryable();if(!User.Identity!.IsAuthenticated)q=q.Where(p=>p.Status=="active");if(SellerId is int own&&!IsAdmin)q=q.Where(p=>p.SellerId==own);if(!string.IsNullOrWhiteSpace(status)&&(IsAdmin||SellerId.HasValue))q=q.Where(p=>p.Status==status);if(!string.IsNullOrWhiteSpace(search))q=q.Where(p=>EF.Functions.ILike(p.Name,$"%{search.Trim()}%")||EF.Functions.ILike(p.Sub,$"%{search.Trim()}%"));if(!string.IsNullOrWhiteSpace(category))q=q.Where(p=>p.Category!.Name==category);if(sellerId.HasValue)q=q.Where(p=>p.SellerId==sellerId);if(minPrice.HasValue)q=q.Where(p=>p.Price>=minPrice);if(maxPrice.HasValue)q=q.Where(p=>p.Price<=maxPrice);return Ok((await q.OrderByDescending(p=>p.CreatedAtUtc).Skip((Math.Max(1,page)-1)*Math.Clamp(pageSize,1,200)).Take(Math.Clamp(pageSize,1,200)).ToListAsync(ct)).Select(ProductResponse));}
 [AllowAnonymous,HttpGet("products/{id:int}")]
 public async Task<IActionResult> Product(int id,CancellationToken ct){var p=await db.Products.AsNoTracking().Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==id,ct);return p is null?NotFound():Ok(ProductResponse(p));}
 [Authorize,HttpPost("products")]
 public async Task<IActionResult> CreateProduct(JsonElement b,CancellationToken ct){if(!IsAdmin&&SellerId is null)return Forbid();var cat=await ResolveCategory(b,ct);if(cat is null)return BadRequest();var p=ReadProduct(b,cat.Id,IsAdmin?ReadInt(b,"sellerId"):SellerId);db.Products.Add(p);await db.SaveChangesAsync(ct);return Created($"/api/products/{p.Id}",ProductResponse(p));}
 [Authorize,HttpPut("products/{id:int}")]
 public async Task<IActionResult> UpdateProduct(int id,JsonElement b,CancellationToken ct){var p=await db.Products.Include(x=>x.Category).SingleOrDefaultAsync(x=>x.Id==id,ct);if(p is null)return NotFound();if(!IsAdmin&&p.SellerId!=SellerId)return Forbid();var cat=await ResolveCategory(b,ct);if(cat is null)return BadRequest();p.Name=ReadString(b,"title")??ReadString(b,"name")??p.Name;p.Sub=ReadString(b,"sub")??p.Sub;p.Description=ReadString(b,"desc")??ReadString(b,"description")??p.Description;p.Price=ReadDecimal(b,"price",p.Price);p.OldPrice=ReadNullableDecimal(b,"oldPrice");p.Stock=ReadInt(b,"stock",p.Stock);p.Moq=ReadInt(b,"moq",p.Moq);p.Weight=ReadDecimal(b,"weight",p.Weight);p.Status=ReadString(b,"status")??p.Status;p.Sku=ReadString(b,"sku")??p.Sku;p.Tags=ReadStrings(b,"tags");p.ImagesJson=Raw(b,"images",p.ImagesJson);p.VariantsJson=Raw(b,"variants",p.VariantsJson);p.SpecsJson=Raw(b,"specs",p.SpecsJson);p.CategoryId=cat.Id;await db.SaveChangesAsync(ct);return Ok(ProductResponse(p));}
 [Authorize,HttpDelete("products/{id:int}")]
 public async Task<IActionResult> DeleteProduct(int id,CancellationToken ct){var p=await db.Products.FindAsync([id],ct);if(p is null)return NotFound();if(!IsAdmin&&p.SellerId!=SellerId)return Forbid();db.Products.Remove(p);await db.SaveChangesAsync(ct);return NoContent();}
 private async Task<Category?> ResolveCategory(JsonElement b,CancellationToken ct){var id=ReadInt(b,"categoryId");var n=ReadString(b,"category");if(id<=0&&n?.StartsWith("c")==true&&int.TryParse(n[1..],out var parsed))id=parsed;return id>0?await db.Categories.FindAsync([id],ct):await db.Categories.FirstOrDefaultAsync(x=>x.Name==n,ct);}
 private static Product ReadProduct(JsonElement b,int cid,int? sid)=>new(){LegacyId=ReadString(b,"legacyId"),Name=ReadString(b,"title")??ReadString(b,"name")??"",Sub=ReadString(b,"sub")??"",Description=ReadString(b,"desc")??ReadString(b,"description"),Price=ReadDecimal(b,"price"),OldPrice=ReadNullableDecimal(b,"oldPrice"),Moq=ReadInt(b,"moq",1),Stock=ReadInt(b,"stock"),Weight=ReadDecimal(b,"weight",.2m),Sold=ReadInt(b,"sold"),Rating=ReadDecimal(b,"rating"),Reviews=ReadInt(b,"reviews"),Status=ReadString(b,"status")??"active",Currency=ReadString(b,"currency")??"USD",Sku=ReadString(b,"sku")??"",Tags=ReadStrings(b,"tags"),ImagesJson=Raw(b,"images","[]"),VariantsJson=Raw(b,"variants","[]"),SpecsJson=Raw(b,"specs","[]"),SellerId=sid,CategoryId=cid};
 private static object ProductResponse(Product p)=>new{id=p.LegacyId??$"pr{p.Id:0000}",dbId=p.Id,legacyId=p.LegacyId,sellerId=p.SellerId is int sid?$"s{sid}":null,title=p.Name,category=p.Category?.Name,categoryId=p.CategoryId,sub=p.Sub,price=p.Price,oldPrice=p.OldPrice,moq=p.Moq,sold=p.Sold,rating=p.Rating,reviews=p.Reviews,stock=p.Stock,weight=p.Weight,status=p.Status,tags=p.Tags,sku=p.Sku,desc=p.Description,images=Parse<string[]>(p.ImagesJson,[]),variants=Parse<object[]>(p.VariantsJson,[]),specs=Parse<JsonElement[]>(p.SpecsJson,[])};
 [Authorize,HttpGet("sellers/me")]
 public async Task<IActionResult> MySeller(CancellationToken ct){if(SellerId is not int id)return NotFound();var s=await db.Sellers.FindAsync([id],ct);return s is null?NotFound():Ok(SellerResponse(s));}
 [Authorize,HttpPut("sellers/me")]
 public async Task<IActionResult> UpdateMySeller(JsonElement b,CancellationToken ct){if(SellerId is not int id)return Forbid();var s=await db.Sellers.FindAsync([id],ct);if(s is null)return NotFound();UpdateSeller(s,b);await db.SaveChangesAsync(ct);return Ok(SellerResponse(s));}
 [Authorize(Roles="admin"),HttpPut("sellers/{id:int}")]
 public async Task<IActionResult> UpdateSeller(int id,JsonElement b,CancellationToken ct){var s=await db.Sellers.FindAsync([id],ct);if(s is null)return NotFound();UpdateSeller(s,b);s.Status=ReadString(b,"status")??s.Status;await db.SaveChangesAsync(ct);return Ok(SellerResponse(s));}
 [Authorize(Roles="admin"),HttpDelete("sellers/{id:int}")]
 public async Task<IActionResult> DeleteSeller(int id,CancellationToken ct){var s=await db.Sellers.FindAsync([id],ct);if(s is null)return NotFound();db.Sellers.Remove(s);await db.SaveChangesAsync(ct);return NoContent();}
 private static object SellerResponse(Seller s)=>new{id=$"s{s.Id}",dbId=s.Id,name=s.Name,owner=s.Owner,block=s.Block,email=s.Email,phone=s.Phone,country=s.Country,status=s.Status,joined=s.JoinedAtUtc.ToString("yyyy-MM-dd"),rating=0,reviews=0,products=0,sales30d=0,responseRate=0,shipOnTime=0};
 private static void UpdateSeller(Seller s,JsonElement b){s.Name=ReadString(b,"name")??s.Name;s.Owner=ReadString(b,"owner")??s.Owner;s.Block=ReadString(b,"block")??s.Block;s.Email=ReadString(b,"email")??s.Email;s.Phone=ReadString(b,"phone")??s.Phone;}
 [Authorize,HttpGet("orders")]
 public async Task<IActionResult> Orders(CancellationToken ct){var q=db.Orders.AsNoTracking().Include(x=>x.Items).AsQueryable();if(!IsAdmin&&SellerId is int sid)q=q.Where(x=>x.SellerId==sid);else if(!IsAdmin){var name=User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value??"";q=q.Where(x=>x.BuyerName==name);}return Ok(await q.OrderByDescending(x=>x.PlacedAtUtc).ToListAsync(ct));}
 [Authorize,HttpPost("orders")]
 public IActionResult CreateOrder()=>StatusCode(501,new ProblemDetails{Title="Checkout belum aktif",Detail="Pembayaran sandbox; tidak ada pesanan dibuat."});
 [AllowAnonymous,HttpGet("public/orders/{id}")]
 public IActionResult TrackOrder(string id)=>NotFound(new ProblemDetails{Title="Tracking belum tersedia"});
 [Authorize,HttpPatch("orders/{id}/status")]
 public IActionResult UpdateOrderStatus(string id)=>StatusCode(501,new ProblemDetails{Title="Status pesanan belum tersedia"});
 [Authorize,HttpGet("settings/{key}")]
 public async Task<IActionResult> GetSetting(string key,CancellationToken ct){var s=await db.Settings.AsNoTracking().SingleOrDefaultAsync(x=>x.Key==key,ct);return s is null?NotFound():Content(s.ValueJson,"application/json");}
 [Authorize(Roles="admin"),HttpPut("settings/{key}")]
 public async Task<IActionResult> PutSetting(string key,JsonElement b,CancellationToken ct){if(key is not("providers" or "hubs" or "vouchers" or "handlingDays"))return BadRequest();var s=await db.Settings.SingleOrDefaultAsync(x=>x.Key==key,ct);if(s is null)db.Settings.Add(new MarketplaceSetting{Key=key,ValueJson=b.GetRawText()});else s.ValueJson=b.GetRawText();await db.SaveChangesAsync(ct);return Ok(b);}
 [Authorize,HttpGet("account/state")]
 public async Task<IActionResult> AccountState(CancellationToken ct){var u=await db.Users.FindAsync([UserId],ct);if(u is null)return Unauthorized();return Ok(new{cart=Parse<object[]>(u.CartJson,[]),wishlist=Parse<string[]>(u.WishlistJson,[]),sellerShipping=Parse<Dictionary<string,bool>>(u.SellerShippingJson,new()),sellerHub=u.SellerHubId is int h?$"h{h}":"h2",handlingDays=u.HandlingDays});}
 [Authorize,HttpPut("account/state")]
 public async Task<IActionResult> UpdateAccountState(JsonElement b,CancellationToken ct){var u=await db.Users.FindAsync([UserId],ct);if(u is null)return Unauthorized();if(b.TryGetProperty("cart",out var cart))u.CartJson=cart.GetRawText();if(b.TryGetProperty("wishlist",out var wish))u.WishlistJson=wish.GetRawText();if(b.TryGetProperty("sellerShipping",out var ship))u.SellerShippingJson=ship.GetRawText();if(b.TryGetProperty("sellerHub",out var hub)&&hub.ValueKind==JsonValueKind.String&&int.TryParse(hub.GetString()!.Replace("h",""),out var hid))u.SellerHubId=hid;if(b.TryGetProperty("handlingDays",out var days)&&days.TryGetInt32(out var hd))u.HandlingDays=Math.Clamp(hd,0,60);await db.SaveChangesAsync(ct);return NoContent();}
 [Authorize,HttpGet("threads")]
 public async Task<IActionResult> Threads(CancellationToken ct){var items=await db.Threads.AsNoTracking().Include(x=>x.Messages).OrderByDescending(x=>x.UpdatedAtUtc).ToListAsync(ct);return Ok(items.Select(ToThread));}
 [Authorize,HttpPost("threads")]
 public async Task<IActionResult> CreateThread(CreateThreadRequest r,CancellationToken ct){var t=new ChatThread{ThreadKey=Guid.NewGuid().ToString("N"),With=r.With,Role=r.Role,Subject=r.Subject,Party=r.Party};t.Messages.Add(new ChatMessage{FromUserId=UserId.ToString(),Text=r.Text});db.Threads.Add(t);await db.SaveChangesAsync(ct);return Ok(ToThread(t));}
 [Authorize,HttpPost("threads/{id:int}/messages")]
 public async Task<IActionResult> SendMessage(int id,ThreadMessageRequest r,CancellationToken ct){var t=await db.Threads.Include(x=>x.Messages).SingleOrDefaultAsync(x=>x.Id==id,ct);if(t is null)return NotFound();t.Messages.Add(new ChatMessage{FromUserId=UserId.ToString(),Text=r.Text});t.UpdatedAtUtc=DateTime.UtcNow;t.Unread=0;await db.SaveChangesAsync(ct);return Ok(ToThread(t));}
 [Authorize,HttpPost("threads/{id:int}/read")]
 public async Task<IActionResult> ReadThread(int id,CancellationToken ct){var t=await db.Threads.FindAsync([id],ct);if(t is null)return NotFound();t.Unread=0;await db.SaveChangesAsync(ct);return NoContent();}
 private object ToThread(ChatThread t)=>new{id=t.Id,with=t.With,role=t.Role,subject=t.Subject,party=t.Party,unread=t.Unread,last=t.UpdatedAtUtc.ToLocalTime().ToString("HH:mm"),online=false,messages=t.Messages.Select(m=>new{from=m.FromUserId==UserId.ToString()?"me":"them",text=m.Text,time=m.SentAtUtc.ToLocalTime().ToString("HH:mm")})};
 [Authorize,HttpPut("account/profile")]
 public async Task<IActionResult> UpdateProfile(UpdateProfileRequest r,CancellationToken ct){var u=await db.Users.FindAsync([UserId],ct);if(u is null)return Unauthorized();u.Name=r.Name.Trim();u.Email=r.Email.Trim().ToLowerInvariant();u.Country=r.Country;await db.SaveChangesAsync(ct);return Ok(new AuthUserResponse(u.Id,u.Username,u.Name,u.Email,u.Country,u.Role,u.SellerId is int sid?$"s{sid}":null,null));}
 private static string? ReadString(JsonElement j,string k)=>j.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
 private static int ReadInt(JsonElement j,string k,int f=0)=>j.TryGetProperty(k,out var v)&&v.TryGetInt32(out var n)?n:f;
 private static decimal ReadDecimal(JsonElement j,string k,decimal f=0)=>j.TryGetProperty(k,out var v)&&v.TryGetDecimal(out var n)?n:f;
 private static decimal? ReadNullableDecimal(JsonElement j,string k)=>j.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetDecimal(out var n)?n:null;
 private static string[] ReadStrings(JsonElement j,string k)=>j.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.Array?v.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()!).ToArray():[];
 private static string Raw(JsonElement j,string k,string f)=>j.TryGetProperty(k,out var v)?v.GetRawText():f;
 private static T Parse<T>(string? s,T f)=>string.IsNullOrWhiteSpace(s)?f:JsonSerializer.Deserialize<T>(s,Json)??f;
}

