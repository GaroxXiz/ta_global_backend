using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;
using TAGlobal.Api.Contracts;

namespace TAGlobal.Api.Controllers;

[ApiController, Route("api")]
public sealed class MarketplaceController(
    IProductUseCases products,
    ISellerUseCases sellers,
    IOrderUseCases orders,
    IAccountUseCases accounts,
    IChatUseCases chat,
    IAdminOperationsUseCases adminOperations) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value, out var id) ? id : 0;
    private bool IsAdmin => User.IsInRole("admin") || User.HasClaim(ClaimTypes.Role, "admin") || User.HasClaim("role", "admin");
    private int? SellerId => int.TryParse(User.FindFirst("sellerId")?.Value, out var id) ? id : null;
    private string BuyerName => User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "";

    [AllowAnonymous, HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] string? search, [FromQuery] string? category, [FromQuery] int? sellerId, [FromQuery] string? status, [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) =>
        Ok(await products.SearchMarketplaceAsync(search, category, sellerId, status, minPrice, maxPrice, SellerId, IsAdmin, page, pageSize, ct));

    [AllowAnonymous, HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id, CancellationToken ct)
    {
        var product = await products.GetAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize, HttpPost("products")]
    public async Task<IActionResult> CreateProduct(JsonElement body, CancellationToken ct)
    {
        if (!IsAdmin && SellerId is null) return Forbid();
        var result = await products.CreateMarketplaceAsync(body, SellerId, IsAdmin, ct);
        return result.Outcome == "bad_request" ? BadRequest() : Created($"/api/products/{result.ResourceId}", result.Data);
    }

    [Authorize, HttpPut("products/{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, JsonElement body, CancellationToken ct)
    {
        var result = await products.UpdateMarketplaceAsync(id, body, SellerId, IsAdmin, ct);
        return result.Outcome switch { "not_found" => NotFound(), "forbidden" => Forbid(), "bad_request" => BadRequest(), _ => Ok(result.Data) };
    }

    [Authorize, HttpDelete("products/{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        var result = await products.DeleteMarketplaceAsync(id, SellerId, IsAdmin, ct);
        return result.Outcome switch { "not_found" => NotFound(), "forbidden" => Forbid(), _ => NoContent() };
    }

    [AllowAnonymous, HttpGet("products/{id:int}/reviews")]
    public async Task<IActionResult> Reviews(int id, CancellationToken ct) =>
        Ok(await products.ListReviewsAsync(id, ct));

    [Authorize, HttpPost("products/{id:int}/reviews")]
    public async Task<IActionResult> CreateReview(int id, JsonElement body, CancellationToken ct)
    {
        var country = User.FindFirst("country")?.Value ?? "Indonesia";
        var result = await products.CreateReviewAsync(id, body, UserId, BuyerName, country, ct);
        return StatusCode(201, result);
    }

    [Authorize, HttpGet("sellers/me")]
    public async Task<IActionResult> MySeller(CancellationToken ct)
    {
        if (SellerId is not int id) return NotFound();
        var seller = await sellers.GetAsync(id, ct);
        return seller is null ? NotFound() : Ok(seller);
    }

    [Authorize, HttpPut("sellers/me")]
    public async Task<IActionResult> UpdateMySeller(JsonElement body, CancellationToken ct)
    {
        if (SellerId is not int id) return Forbid();
        var result = await sellers.UpdateAsync(id, body, includeStatus: false, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize, HttpGet("sellers/me/analytics")]
    public async Task<IActionResult> MyAnalytics(CancellationToken ct)
    {
        if (SellerId is not int id) return Forbid();
        return Ok(await sellers.GetAnalyticsAsync(id, ct));
    }

    [Authorize, HttpGet("seller/analytics")]
    public async Task<IActionResult> SellerAnalytics(CancellationToken ct)
    {
        if (SellerId is not int id) return Forbid();
        return Ok(await sellers.GetAnalyticsAsync(id, ct));
    }

    [Authorize, HttpPost("seller/dropoff/manifest")]
    public async Task<IActionResult> CreateDropoffManifest(JsonElement body, CancellationToken ct)
    {
        if (SellerId is not int id) return Forbid();
        return Ok(await orders.CreateManifestAsync(body, id, ct));
    }

    [Authorize, HttpGet("seller/dropoff/manifests")]
    public async Task<IActionResult> ListDropoffManifests(CancellationToken ct)
    {
        if (SellerId is not int id) return Forbid();
        return Ok(await orders.ListManifestsAsync(id, ct));
    }

    [Authorize(Roles = "admin"), HttpPut("sellers/{id:int}")]
    public async Task<IActionResult> UpdateSeller(int id, JsonElement body, CancellationToken ct)
    {
        var result = await sellers.UpdateAsync(id, body, includeStatus: true, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize(Roles = "admin"), HttpDelete("sellers/{id:int}")]
    public async Task<IActionResult> DeleteSeller(int id, CancellationToken ct) => await sellers.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [Authorize, HttpGet("orders")]
    public async Task<IActionResult> Orders(CancellationToken ct) => Ok(await orders.ListAsync(IsAdmin, SellerId, BuyerName, ct));

    [Authorize(Roles = "buyer"), HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(BuyerName)) return Unauthorized();
        var result = await orders.CreateAsync(request, BuyerName, ct);
        return result.Outcome switch
        {
            "bad_request" => BadRequest(result.Data),
            "conflict" => Conflict(result.Data),
            _ => CreatedAtAction(nameof(GetOrder), new { id = result.ResourceId }, result.Data)
        };
    }

    [Authorize, HttpGet("orders/{id}")]
    public async Task<IActionResult> GetOrder(string id, CancellationToken ct)
    {
        var result = await orders.GetAsync(id, IsAdmin, SellerId, BuyerName, ct);
        return MapOperation(result);
    }

    [AllowAnonymous, HttpGet("public/orders/{id}")]
    public async Task<IActionResult> TrackOrder(string id, CancellationToken ct)
    {
        var result = await orders.TrackAsync(id, ct);
        return result is null ? NotFound(new ProblemDetails { Title = "Pesanan tidak ditemukan" }) : Ok(result);
    }

    [Authorize, HttpPatch("orders/{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(string id, OrderStatusRequest request, CancellationToken ct) =>
        MapOperation(await orders.UpdateStatusAsync(id, request, IsAdmin, SellerId, ct));

    [Authorize, HttpGet("settings/{key}")]
    public async Task<IActionResult> GetSetting(string key, CancellationToken ct)
    {
        var json = await adminOperations.GetSettingAsync(key, ct);
        return json is null ? NotFound() : Content(json, "application/json");
    }

    [Authorize(Roles = "admin"), HttpPut("settings/{key}")]
    public async Task<IActionResult> PutSetting(string key, JsonElement body, CancellationToken ct)
    {
        try { await adminOperations.SaveSettingAsync(key, body, ct); return Ok(body); }
        catch (ArgumentOutOfRangeException) { return BadRequest(); }
    }

    [Authorize, HttpGet("account/state")]
    public async Task<IActionResult> AccountState(CancellationToken ct)
    {
        var state = await accounts.GetStateAsync(UserId, ct);
        return state is null ? Unauthorized() : Ok(state);
    }

    [Authorize, HttpPut("account/state")]
    public async Task<IActionResult> UpdateAccountState(JsonElement body, CancellationToken ct) =>
        await accounts.UpdateStateAsync(UserId, body, ct) ? NoContent() : Unauthorized();

    [Authorize, HttpGet("threads")]
    public async Task<IActionResult> Threads(CancellationToken ct) => Ok(await chat.ListAsync(UserId, ct));

    [Authorize, HttpPost("threads")]
    public async Task<IActionResult> CreateThread(CreateThreadRequest request, CancellationToken ct) => Ok(await chat.CreateAsync(request, UserId, ct));

    [Authorize, HttpPost("threads/{id:int}/messages")]
    public async Task<IActionResult> SendMessage(int id, ThreadMessageRequest request, CancellationToken ct)
    {
        var thread = await chat.SendAsync(id, request.Text, UserId, ct);
        return thread is null ? NotFound() : Ok(thread);
    }

    [Authorize, HttpPost("threads/{id:int}/read")]
    public async Task<IActionResult> ReadThread(int id, CancellationToken ct) => await chat.MarkReadAsync(id, ct) ? NoContent() : NotFound();

    [Authorize, HttpPut("account/profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await accounts.UpdateProfileAsync(UserId, request, ct);
        return user is null ? Unauthorized() : Ok(user);
    }

    [Authorize, HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile? file, [FromServices] IWebHostEnvironment env)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Tidak ada berkas yang diunggah" });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(new ProblemDetails { Title = "Format berkas tidak didukung. Gunakan JPG, PNG, WEBP, atau GIF." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new ProblemDetails { Title = "Ukuran berkas maksimal 10 MB" });

        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/uploads/{fileName}";
        return Ok(new { url, name = file.FileName, size = file.Length });
    }

    private IActionResult MapOperation(MarketplaceOperation operation) => operation.Outcome switch
    {
        "not_found" => NotFound(),
        "forbidden" => Forbid(),
        "bad_request" => BadRequest(operation.Data),
        "conflict" => Conflict(operation.Data),
        _ => Ok(operation.Data)
    };
}
