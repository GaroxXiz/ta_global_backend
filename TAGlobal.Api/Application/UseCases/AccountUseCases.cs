using System.Text.Json;
using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.UseCases;

public interface IAccountUseCases
{
    Task<object?> GetStateAsync(int userId, CancellationToken ct);
    Task<bool> UpdateStateAsync(int userId, JsonElement body, CancellationToken ct);
    Task<AuthUserResponse?> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct);
}

public sealed class AccountUseCases(IUserRepository repository) : IAccountUseCases
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<object?> GetStateAsync(int userId, CancellationToken ct)
    {
        var user = await repository.FindByIdWithSellerAsync(userId, ct);
        return user is null ? null : new { cart = Parse<object[]>(user.CartJson, []), wishlist = Parse<string[]>(user.WishlistJson, []), sellerShipping = Parse<Dictionary<string, bool>>(user.SellerShippingJson, new()), sellerHub = user.SellerHubId is int hub ? $"h{hub}" : "h2", handlingDays = user.HandlingDays };
    }

    public async Task<bool> UpdateStateAsync(int userId, JsonElement body, CancellationToken ct)
    {
        var user = await repository.FindByIdWithSellerAsync(userId, ct);
        if (user is null) return false;
        if (body.TryGetProperty("cart", out var cart)) user.CartJson = cart.GetRawText();
        if (body.TryGetProperty("wishlist", out var wishlist)) user.WishlistJson = wishlist.GetRawText();
        if (body.TryGetProperty("sellerShipping", out var shipping)) user.SellerShippingJson = shipping.GetRawText();
        if (body.TryGetProperty("sellerHub", out var hub) && hub.ValueKind == JsonValueKind.String && int.TryParse(hub.GetString()!.Replace("h", ""), out var hubId)) user.SellerHubId = hubId;
        if (body.TryGetProperty("handlingDays", out var days) && days.TryGetInt32(out var handlingDays)) user.HandlingDays = Math.Clamp(handlingDays, 0, 60);
        return await repository.UpdateAsync(user, ct);
    }

    public async Task<AuthUserResponse?> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await repository.FindByIdWithSellerAsync(userId, ct);
        if (user is null) return null;
        user.Name = request.Name.Trim(); user.Email = request.Email.Trim().ToLowerInvariant(); user.Country = request.Country;
        if (!await repository.UpdateAsync(user, ct)) return null;
        return new AuthUserResponse(user.Id, user.Username, user.Name, user.Email, user.Country, user.Role, user.SellerId is int sellerId ? $"s{sellerId}" : null, null);
    }

    private static T Parse<T>(string? value, T fallback) => string.IsNullOrWhiteSpace(value) ? fallback : JsonSerializer.Deserialize<T>(value, Json) ?? fallback;
}
