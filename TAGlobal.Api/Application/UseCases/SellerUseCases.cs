using System.Text.Json;
using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.UseCases;

public interface ISellerUseCases
{
    Task<IReadOnlyList<object>> ListPublicAsync(CancellationToken ct);
    Task<IReadOnlyList<object>> ListAdminAsync(CancellationToken ct);
    Task<object?> GetAsync(int id, CancellationToken ct);
    Task<object?> UpdateAsync(int id, JsonElement body, bool includeStatus, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
    Task<object> GetAnalyticsAsync(int sellerId, CancellationToken ct);
}

public sealed class SellerUseCases(ISellerRepository repository) : ISellerUseCases
{
    public async Task<IReadOnlyList<object>> ListPublicAsync(CancellationToken ct) =>
        (await repository.ListAsync(true, ct)).Select(s => ToResponse(s, 0)).ToArray();

    public async Task<IReadOnlyList<object>> ListAdminAsync(CancellationToken ct)
    {
        var sellers = await repository.ListAsync(false, ct);
        var responses = new List<object>(sellers.Count);
        foreach (var seller in sellers) responses.Add(ToResponse(seller, await repository.CountProductsAsync(seller.Id, ct)));
        return responses;
    }

    public async Task<object?> GetAsync(int id, CancellationToken ct)
    {
        var seller = await repository.FindAsync(id, ct);
        return seller is null ? null : ToResponse(seller, 0);
    }

    public async Task<object?> UpdateAsync(int id, JsonElement body, bool includeStatus, CancellationToken ct)
    {
        var seller = await repository.FindAsync(id, ct);
        if (seller is null) return null;
        seller.Name = Read(body, "name") ?? seller.Name;
        seller.Owner = Read(body, "owner") ?? seller.Owner;
        seller.Block = Read(body, "block") ?? seller.Block;
        seller.Email = Read(body, "email") ?? seller.Email;
        seller.Phone = Read(body, "phone") ?? seller.Phone;
        if (includeStatus) seller.Status = Read(body, "status") ?? seller.Status;
        if (!await repository.UpdateAsync(seller, ct)) return null;
        return ToResponse(seller, 0);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        return await repository.DeleteAsync(id, ct);
    }

    public Task<object> GetAnalyticsAsync(int sellerId, CancellationToken ct) =>
        repository.GetAnalyticsAsync(sellerId, ct);

    private static object ToResponse(Seller s, int products) => new { id = $"s{s.Id}", dbId = s.Id, name = s.Name, owner = s.Owner, block = s.Block, email = s.Email, phone = s.Phone, country = s.Country, status = s.Status, joined = s.JoinedAtUtc.ToString("yyyy-MM-dd"), rating = 0, reviews = 0, products, sales30d = 0, responseRate = 0, shipOnTime = 0 };
    private static string? Read(JsonElement body, string key) => body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
