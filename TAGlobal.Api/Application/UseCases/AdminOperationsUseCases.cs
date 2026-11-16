using System.Text.Json;
using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.UseCases;

public interface IAdminOperationsUseCases
{
    Task<IReadOnlyList<MarketplaceOrder>> ListOrdersAsync(CancellationToken ct);
    Task<IReadOnlyList<ChatThread>> ListThreadsAsync(CancellationToken ct);
    Task<bool> MarkReadAsync(int id, CancellationToken ct);
    Task<ChatThread> CreateThreadAsync(JsonElement body, CancellationToken ct);
    Task<int> BroadcastAsync(JsonElement body, CancellationToken ct);
    Task<string?> GetSettingAsync(string key, CancellationToken ct);
    Task SaveSettingAsync(string key, JsonElement value, CancellationToken ct);
    Task<object> GetDashboardAsync(CancellationToken ct);
    Task<object> GetAnalyticsAsync(CancellationToken ct);
}

public sealed class AdminOperationsUseCases(IAdminOperationsRepository repository) : IAdminOperationsUseCases
{
    private static readonly string[] SettingKeys = ["providers", "hubs", "vouchers", "handlingDays"];

    public Task<IReadOnlyList<MarketplaceOrder>> ListOrdersAsync(CancellationToken ct) => repository.ListOrdersAsync(ct);
    public Task<IReadOnlyList<ChatThread>> ListThreadsAsync(CancellationToken ct) => repository.ListThreadsAsync(ct);

    public async Task<bool> MarkReadAsync(int id, CancellationToken ct)
    {
        return await repository.MarkThreadReadAsync(id, ct);
    }

    public async Task<ChatThread> CreateThreadAsync(JsonElement body, CancellationToken ct)
    {
        var thread = new ChatThread { ThreadKey = Guid.NewGuid().ToString("N"), With = Read(body, "with") ?? "Seller", Role = Read(body, "role") ?? "seller", Subject = Read(body, "subject") ?? "Pesan dari Admin", Party = Read(body, "party") ?? "admin" };
        thread.Messages.Add(new ChatMessage { FromUserId = "admin", Text = Read(body, "text") ?? "Halo." });
        return await repository.CreateThreadAsync(thread, ct);
    }

    public async Task<int> BroadcastAsync(JsonElement body, CancellationToken ct)
    {
        var text = Read(body, "message") ?? Read(body, "text") ?? "";
        var subject = Read(body, "subject") ?? "Siaran TAGlobal";
        var audience = Read(body, "audience");
        var recipients = await repository.ListRecipientsAsync(ct);
        var target = recipients.Where(x => audience == "sellers" ? x.SellerId is not null : x.SellerId is null).ToArray();
        var threads = new List<ChatThread>(target.Length);
        foreach (var recipient in target)
        {
            var thread = new ChatThread { ThreadKey = Guid.NewGuid().ToString("N"), With = "Admin TAGlobal", Role = "admin", Subject = subject, Party = $"admin:{recipient.Id}" };
            thread.Messages.Add(new ChatMessage { FromUserId = "admin", Text = text });
            threads.Add(thread);
        }
        await repository.CreateThreadsAsync(threads, ct);
        return target.Length;
    }

    public Task<string?> GetSettingAsync(string key, CancellationToken ct) => repository.GetSettingAsync(key, ct);

    public Task SaveSettingAsync(string key, JsonElement value, CancellationToken ct)
    {
        if (!SettingKeys.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key));
        return repository.SaveSettingAsync(key, value.GetRawText(), ct);
    }

    public async Task<object> GetDashboardAsync(CancellationToken ct)
    {
        var data = await repository.GetDashboardAsync(ct);
        return new { products = data.Products, sellers = data.Sellers, orders = data.Orders, gmv = data.Gmv };
    }

    public Task<object> GetAnalyticsAsync(CancellationToken ct) => repository.GetAnalyticsAsync(ct);

    private static string? Read(JsonElement body, string key) => body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
