using System.Text.Json;
using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.UseCases;

public sealed record MarketplaceOperation(string Outcome, object? Data = null, string? ResourceId = null);

public interface IOrderUseCases
{
    Task<IReadOnlyList<MarketplaceOrder>> ListAsync(bool isAdmin, int? sellerId, string buyerName, CancellationToken ct);
    Task<MarketplaceOperation> CreateAsync(CreateOrderRequest request, string buyerName, CancellationToken ct);
    Task<MarketplaceOperation> GetAsync(string orderNumber, bool isAdmin, int? sellerId, string buyerName, CancellationToken ct);
    Task<object?> TrackAsync(string id, CancellationToken ct);
    Task<MarketplaceOperation> UpdateStatusAsync(string id, OrderStatusRequest request, bool isAdmin, int? sellerId, CancellationToken ct);
    Task<object> CreateManifestAsync(JsonElement body, int sellerId, CancellationToken ct);
    Task<IReadOnlyList<object>> ListManifestsAsync(int sellerId, CancellationToken ct);
}

public sealed class OrderUseCases(IOrderRepository repository) : IOrderUseCases
{
    private static readonly string[] AllowedStatuses = ["awaiting_acceptance", "to_dropoff", "at_hub", "in_transit", "out_for_delivery", "delivered", "cancelled"];

    public Task<IReadOnlyList<MarketplaceOrder>> ListAsync(bool isAdmin, int? sellerId, string buyerName, CancellationToken ct) => repository.ListAsync(isAdmin, sellerId, buyerName, ct);

    public async Task<MarketplaceOperation> CreateAsync(CreateOrderRequest request, string buyerName, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0 || request.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0 || x.Quantity > 10000) || string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.ProviderId))
            return new("bad_request", new { title = "Data pesanan tidak valid" });
        var ids = request.Items.Select(x => x.ProductId).Distinct().ToArray();
        if (ids.Length != request.Items.Count) return new("bad_request", new { title = "Item duplikat", detail = "Gabungkan kuantitas item yang sama sebelum checkout." });
        var products = await repository.FindProductsAsync(ids, ct);
        if (products.Count != ids.Length || products.Any(p => p.Status != "active")) return new("bad_request", new { title = "Produk tidak tersedia", detail = "Satu atau beberapa produk sudah tidak aktif." });
        var byId = products.ToDictionary(p => p.Id);
        var sellerIds = products.Select(p => p.SellerId).Distinct().ToArray();
        if (sellerIds.Length != 1 || sellerIds[0] is null) return new("bad_request", new { title = "Keranjang harus satu penjual", detail = "Buat pesanan terpisah untuk setiap penjual." });
        foreach (var item in request.Items)
        {
            var product = byId[item.ProductId];
            if (item.Quantity < product.Moq || item.Quantity > product.Stock) return new("bad_request", new { title = "Stok atau MOQ tidak terpenuhi", detail = $"{product.Name}: MOQ {product.Moq}, stok {product.Stock}." });
        }
        var order = new MarketplaceOrder
        {
            OrderNumber = ($"TG{DateTime.UtcNow:yyMMdd}{Guid.NewGuid():N}")[..32].ToUpperInvariant(), BuyerName = buyerName,
            Country = request.Country, City = request.City.Trim(), Address = request.Address.Trim(), SellerId = sellerIds[0]!.Value,
            ProviderId = request.ProviderId.Trim(), Total = request.Items.Sum(i => byId[i.ProductId].Price * i.Quantity), Note = request.Note?.Trim(),
            Status = "awaiting_acceptance", Stage = 0, Awb = "-"
        };
        foreach (var item in request.Items) order.Items.Add(new OrderItem { ProductId = item.ProductId, ProductName = byId[item.ProductId].Name, Quantity = item.Quantity, Variant = item.Variant ?? "" });
        try { await repository.CreateAsync(order, ct); }
        catch (InvalidOperationException e) { return new("conflict", new { title = "Stok berubah", detail = e.Message }); }
        return new("created", ToResponse(order), order.OrderNumber);
    }

    public async Task<MarketplaceOperation> GetAsync(string orderNumber, bool isAdmin, int? sellerId, string buyerName, CancellationToken ct)
    {
        var order = await repository.FindAsync(orderNumber, ct);
        if (order is null) return new("not_found");
        if (!isAdmin && sellerId != order.SellerId && order.BuyerName != buyerName) return new("forbidden");
        return new("ok", ToResponse(order));
    }

    public async Task<object?> TrackAsync(string id, CancellationToken ct)
    {
        var order = await repository.FindForTrackingAsync(id, ct);
        return order is null ? null : new { id = order.OrderNumber, status = order.Status, stage = order.Stage, awb = order.Awb, provider = order.ProviderId, city = order.City, items = order.Items.Select(i => new { productId = i.ProductId, productName = i.ProductName, quantity = i.Quantity, variant = i.Variant }) };
    }

    public async Task<MarketplaceOperation> UpdateStatusAsync(string id, OrderStatusRequest request, bool isAdmin, int? sellerId, CancellationToken ct)
    {
        var order = await repository.FindAsync(id, ct);
        if (order is null) return new("not_found");
        if (!AllowedStatuses.Contains(request.Status)) return new("bad_request", new { title = "Status tidak valid" });
        if (!isAdmin && sellerId != order.SellerId) return new("forbidden");
        if (order.Status is "delivered" or "cancelled") return new("conflict", new { title = "Pesanan sudah selesai" });
        var updated = await repository.UpdateStatusAsync(id, request.Status, Math.Clamp(request.Stage, 0, 5), string.IsNullOrWhiteSpace(request.Awb) ? null : request.Awb.Trim(), ct);
        return updated is null ? new("not_found") : new("ok", ToResponse(updated));
    }

    public async Task<object> CreateManifestAsync(JsonElement body, int sellerId, CancellationToken ct)
    {
        var hubId = body.TryGetProperty("hubId", out var h) ? h.GetString() ?? "h2" : "h2";
        var slot = body.TryGetProperty("slot", out var s) ? s.GetString() ?? "Hari ini 13:00 - 15:00" : "Hari ini 13:00 - 15:00";
        var orderNumbers = body.TryGetProperty("orderIds", out var ids) && ids.ValueKind == JsonValueKind.Array
            ? ids.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray()
            : Array.Empty<string>();

        var weight = body.TryGetProperty("totalWeight", out var w) && w.TryGetDecimal(out var dw) ? dw : 1.0m;

        var manifest = new DropoffManifest
        {
            ManifestCode = "DO-" + Random.Shared.Next(100000, 999999),
            SellerId = sellerId,
            HubId = hubId,
            Slot = slot,
            OrderIdsJson = JsonSerializer.Serialize(orderNumbers),
            TotalOrders = orderNumbers.Length,
            TotalWeight = weight,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.CreateManifestAsync(manifest, orderNumbers, ct);

        return new
        {
            code = manifest.ManifestCode,
            sellerId = $"s{manifest.SellerId}",
            hubId = manifest.HubId,
            slot = manifest.Slot,
            orders = orderNumbers,
            totalOrders = manifest.TotalOrders,
            totalWeight = manifest.TotalWeight,
            created = manifest.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
        };
    }

    public async Task<IReadOnlyList<object>> ListManifestsAsync(int sellerId, CancellationToken ct)
    {
        var manifests = await repository.ListManifestsAsync(sellerId, ct);
        return manifests.Select(m => new
        {
            id = m.Id,
            code = m.ManifestCode,
            hubId = m.HubId,
            slot = m.Slot,
            totalOrders = m.TotalOrders,
            totalWeight = m.TotalWeight,
            created = m.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
        }).ToArray();
    }

    private static object ToResponse(MarketplaceOrder o) => new { id = o.OrderNumber, sellerId = $"s{o.SellerId}", buyer = o.BuyerName, country = o.Country, city = o.City, address = o.Address, provider = o.ProviderId, total = o.Total, status = o.Status, stage = o.Stage, awb = o.Awb, note = o.Note, placed = o.PlacedAtUtc.ToString("yyyy-MM-dd HH:mm"), items = o.Items.Select(i => new { productId = i.ProductId, productName = i.ProductName, qty = i.Quantity, variant = i.Variant }) };
}
