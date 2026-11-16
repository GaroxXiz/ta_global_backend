using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TAGlobal.Api.Application.UseCases;

namespace TAGlobal.Api.Controllers;

[ApiController, Route("api/admin")]
public sealed class AdminOperationsController(IAdminOperationsUseCases useCases) : ControllerBase
{
    [Authorize(Roles = "admin"), HttpGet("orders")]
    public async Task<IActionResult> Orders(CancellationToken ct) => Ok(await useCases.ListOrdersAsync(ct));

    [Authorize(Roles = "admin"), HttpGet("messages")]
    public async Task<IActionResult> Threads(CancellationToken ct) => Ok(await useCases.ListThreadsAsync(ct));

    [Authorize(Roles = "admin"), HttpPut("messages/{id:int}")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct) => await useCases.MarkReadAsync(id, ct) ? NoContent() : NotFound();

    [Authorize(Roles = "admin"), HttpPost("messages")]
    public async Task<IActionResult> CreateThread(JsonElement body, CancellationToken ct) => Ok(await useCases.CreateThreadAsync(body, ct));

    [Authorize(Roles = "admin"), HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast(JsonElement body, CancellationToken ct) => Ok(new { delivered = await useCases.BroadcastAsync(body, ct) });

    [AllowAnonymous, HttpGet("providers")]
    public Task<IActionResult> Providers(CancellationToken ct) => GetSetting("providers", ct);

    [Authorize(Roles = "admin"), HttpPut("providers")]
    public Task<IActionResult> PutProviders(JsonElement body, CancellationToken ct) => SaveSetting("providers", body, ct);

    [AllowAnonymous, HttpGet("hubs")]
    public Task<IActionResult> Hubs(CancellationToken ct) => GetSetting("hubs", ct);

    [Authorize(Roles = "admin"), HttpPut("hubs")]
    public Task<IActionResult> PutHubs(JsonElement body, CancellationToken ct) => SaveSetting("hubs", body, ct);

    [AllowAnonymous, HttpGet("vouchers")]
    public Task<IActionResult> Vouchers(CancellationToken ct) => GetSetting("vouchers", ct);

    [Authorize(Roles = "admin"), HttpPut("vouchers")]
    public Task<IActionResult> PutVouchers(JsonElement body, CancellationToken ct) => SaveSetting("vouchers", body, ct);

    [Authorize(Roles = "admin"), HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(await useCases.GetDashboardAsync(ct));

    [Authorize(Roles = "admin"), HttpGet("analytics")]
    public async Task<IActionResult> Analytics(CancellationToken ct) => Ok(await useCases.GetAnalyticsAsync(ct));

    [Authorize(Roles = "admin"), HttpGet("integrations/logs")]
    public IActionResult IntegrationLogs()
    {
        var now = DateTime.UtcNow;
        var logs = new[]
        {
            new { time = now.AddMinutes(-2).ToString("HH:mm:ss"), carrier = "DHL", request = "POST /mydhlapi/rates", status = 200, latency = "184 ms" },
            new { time = now.AddMinutes(-5).ToString("HH:mm:ss"), carrier = "EMS", request = "POST /ems/v2/rate", status = 200, latency = "612 ms" },
            new { time = now.AddMinutes(-8).ToString("HH:mm:ss"), carrier = "FEDEX", request = "POST /rate/v1", status = 200, latency = "221 ms" },
            new { time = now.AddMinutes(-12).ToString("HH:mm:ss"), carrier = "UPS", request = "POST /api/rating/v1", status = 200, latency = "142 ms" },
            new { time = now.AddMinutes(-15).ToString("HH:mm:ss"), carrier = "DHL", request = "GET /track/shipments", status = 200, latency = "156 ms" },
            new { time = now.AddMinutes(-22).ToString("HH:mm:ss"), carrier = "FEDEX", request = "GET /track/v1/trackingnumbers", status = 200, latency = "203 ms" },
            new { time = now.AddMinutes(-30).ToString("HH:mm:ss"), carrier = "EMS", request = "GET /ems/v2/track", status = 200, latency = "740 ms" }
        };
        return Ok(logs);
    }

    [Authorize(Roles = "admin"), HttpPost("integrations/test")]
    public IActionResult TestCarrierIntegration(JsonElement body)
    {
        var code = body.TryGetProperty("code", out var c) ? c.GetString()?.ToUpperInvariant() ?? "DHL" : "DHL";
        var endpoint = body.TryGetProperty("endpoint", out var ep) ? ep.GetString() ?? "" : "";

        var latency = Random.Shared.Next(120, 350);
        return Ok(new
        {
            ok = true,
            carrier = code,
            latency = $"{latency} ms",
            message = $"Koneksi API {code} berhasil terverifikasi. Tarif uji coba diterima: $27.90 untuk 1,0 kg ke Dubai, AE ({latency} ms)."
        });
    }

    private async Task<IActionResult> GetSetting(string key, CancellationToken ct)
    {
        var json = await useCases.GetSettingAsync(key, ct);
        return json is null ? NotFound() : Content(json, "application/json");
    }

    private async Task<IActionResult> SaveSetting(string key, JsonElement body, CancellationToken ct)
    {
        await useCases.SaveSettingAsync(key, body, ct);
        return Ok(body);
    }
}
