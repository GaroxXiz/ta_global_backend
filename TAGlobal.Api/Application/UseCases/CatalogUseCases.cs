using System.Text.Json;
using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.UseCases;

public interface ICategoryUseCases
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken ct);
    Task<CategoryResponse?> GetAsync(int id, CancellationToken ct);
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}

public interface IProductUseCases
{
    Task<IReadOnlyList<object>> ListAsync(string? search, int? categoryId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<object>> ListAllForAdminAsync(CancellationToken ct);
    Task<object?> GetAsync(int id, CancellationToken ct);
    Task<ProductUseCases.CreatedProduct?> CreateAsync(CreateProductRequest request, CancellationToken ct);
    Task<object?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<object>> SearchMarketplaceAsync(string? search, string? category, int? sellerId, string? status, decimal? minPrice, decimal? maxPrice, int? ownSellerId, bool isAdmin, int page, int pageSize, CancellationToken ct);
    Task<ProductActionResult> CreateMarketplaceAsync(JsonElement body, int? sellerId, bool isAdmin, CancellationToken ct);
    Task<ProductActionResult> UpdateMarketplaceAsync(int id, JsonElement body, int? sellerId, bool isAdmin, CancellationToken ct);
    Task<ProductActionResult> DeleteMarketplaceAsync(int id, int? sellerId, bool isAdmin, CancellationToken ct);
    Task<IReadOnlyList<object>> ListReviewsAsync(int productId, CancellationToken ct);
    Task<object> CreateReviewAsync(int productId, JsonElement body, int userId, string userName, string userCountry, CancellationToken ct);
}

public sealed record ProductActionResult(string Outcome, object? Data = null, int? ResourceId = null);

public sealed class CategoryUseCases(ICategoryRepository repository) : ICategoryUseCases
{
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken ct) =>
        (await repository.ListAsync(ct)).Select(ToResponse).ToArray();

    public async Task<CategoryResponse?> GetAsync(int id, CancellationToken ct)
    {
        var category = await repository.FindAsync(id, ct);
        return category is null ? null : ToResponse(category);
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var category = new Category { Name = request.Name.Trim(), Description = request.Description };
        await repository.CreateAsync(category, ct);
        return ToResponse(category);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        return await repository.DeleteAsync(id, ct);
    }

    private static CategoryResponse ToResponse(Category category) => new(category.Id, category.Name, category.Description);
}

public sealed class ProductUseCases(IProductRepository repository) : IProductUseCases
{
    public sealed record CreatedProduct(int Id, object Response);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<object>> ListAsync(string? search, int? categoryId, int page, int pageSize, CancellationToken ct) =>
        (await repository.ListActiveAsync(search, categoryId, page, pageSize, ct)).Select(ToResponse).ToArray();

    public async Task<IReadOnlyList<object>> ListAllForAdminAsync(CancellationToken ct) =>
        (await repository.ListAllAsync(ct)).Select(ToAdminResponse).ToArray();

    public async Task<IReadOnlyList<object>> SearchMarketplaceAsync(string? search, string? category, int? sellerId, string? status, decimal? minPrice, decimal? maxPrice, int? ownSellerId, bool isAdmin, int page, int pageSize, CancellationToken ct) =>
        (await repository.SearchMarketplaceAsync(search, category, sellerId, status, minPrice, maxPrice, ownSellerId, isAdmin, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct)).Select(ToResponse).ToArray();

    public async Task<ProductActionResult> CreateMarketplaceAsync(JsonElement body, int? sellerId, bool isAdmin, CancellationToken ct)
    {
        var category = await ResolveCategory(body, ct);
        if (category is null) return new("bad_request");
        var product = BuildProduct(body, category.Id, isAdmin ? ReadInt(body, "sellerId") : sellerId);
        await repository.CreateAsync(product, ct);
        var saved = await repository.FindWithCategoryAsync(product.Id, ct) ?? product;
        return new("created", ToResponse(saved), saved.Id);
    }

    public async Task<ProductActionResult> UpdateMarketplaceAsync(int id, JsonElement body, int? sellerId, bool isAdmin, CancellationToken ct)
    {
        var product = await repository.FindAsync(id, ct);
        if (product is null) return new("not_found");
        if (!isAdmin && product.SellerId != sellerId) return new("forbidden");
        var category = await ResolveCategory(body, ct);
        if (category is null) return new("bad_request");
        product.Name = ReadString(body, "title") ?? ReadString(body, "name") ?? product.Name;
        product.Sub = ReadString(body, "sub") ?? product.Sub;
        product.Description = ReadString(body, "desc") ?? ReadString(body, "description") ?? product.Description;
        product.Price = ReadDecimal(body, "price", product.Price); product.OldPrice = ReadNullableDecimal(body, "oldPrice");
        product.Stock = ReadInt(body, "stock", product.Stock); product.Moq = ReadInt(body, "moq", product.Moq); product.Weight = ReadDecimal(body, "weight", product.Weight);
        product.Status = ReadString(body, "status") ?? product.Status; product.Sku = ReadString(body, "sku") ?? product.Sku;
        product.Tags = ReadStrings(body, "tags"); product.ImagesJson = Raw(body, "images", product.ImagesJson); product.VariantsJson = Raw(body, "variants", product.VariantsJson); product.SpecsJson = Raw(body, "specs", product.SpecsJson); product.CategoryId = category.Id;
        if (!await repository.UpdateAsync(product, ct)) return new("not_found");
        return new("ok", await GetAsync(id, ct));
    }

    public async Task<ProductActionResult> DeleteMarketplaceAsync(int id, int? sellerId, bool isAdmin, CancellationToken ct)
    {
        var product = await repository.FindAsync(id, ct);
        if (product is null) return new("not_found");
        if (!isAdmin && product.SellerId != sellerId) return new("forbidden");
        await repository.DeleteAsync(id, ct);
        return new("no_content");
    }

    public async Task<object?> GetAsync(int id, CancellationToken ct)
    {
        var product = await repository.FindWithCategoryAsync(id, ct);
        return product is null ? null : ToResponse(product);
    }

    public async Task<CreatedProduct?> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        if (!await repository.CategoryExistsAsync(request.CategoryId, ct)) return null;
        var product = new Product { Name = request.Name, Description = request.Description, Price = request.Price, Currency = request.Currency, Stock = request.Stock, CategoryId = request.CategoryId, Status = "active" };
        await repository.CreateAsync(product, ct);
        product = await repository.FindWithCategoryAsync(product.Id, ct) ?? product;
        return new CreatedProduct(product.Id, ToResponse(product));
    }

    public async Task<object?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await repository.FindAsync(id, ct);
        if (product is null) return null;
        if (!await repository.CategoryExistsAsync(request.CategoryId, ct)) return null;
        product.Name = request.Name; product.Description = request.Description; product.Price = request.Price; product.Currency = request.Currency;
        product.Stock = request.Stock; product.CategoryId = request.CategoryId; product.Status = request.IsActive ? "active" : "draft";
        if (!await repository.UpdateAsync(product, ct)) return null;
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        return await repository.DeleteAsync(id, ct);
    }

    public async Task<IReadOnlyList<object>> ListReviewsAsync(int productId, CancellationToken ct)
    {
        var reviews = await repository.ListReviewsAsync(productId, ct);
        return reviews.Select(ToReviewResponse).ToArray();
    }

    public async Task<object> CreateReviewAsync(int productId, JsonElement body, int userId, string userName, string userCountry, CancellationToken ct)
    {
        var rating = Math.Clamp(ReadInt(body, "rating", 5), 1, 5);
        var text = ReadString(body, "text") ?? ReadString(body, "comment") ?? "";
        var variant = ReadString(body, "variant") ?? "Standar";

        var review = new ProductReview
        {
            ProductId = productId,
            UserId = userId,
            BuyerName = string.IsNullOrWhiteSpace(userName) ? "Pembeli TAGlobal" : userName,
            BuyerCountry = string.IsNullOrWhiteSpace(userCountry) ? "Indonesia" : userCountry,
            Rating = rating,
            Text = text,
            Variant = variant,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.CreateReviewAsync(review, ct);
        return ToReviewResponse(review);
    }

    private static object ToResponse(Product p) => new { id = p.LegacyId ?? $"pr{p.Id:0000}", dbId = p.Id, legacyId = p.LegacyId, sellerId = p.SellerId is int s ? $"s{s}" : null, title = p.Name, category = p.Category?.Name, categoryId = p.CategoryId, sub = p.Sub, price = p.Price, oldPrice = p.OldPrice, moq = p.Moq, sold = p.Sold, rating = p.Rating, reviews = p.Reviews, stock = p.Stock, weight = p.Weight, status = p.Status, tags = p.Tags, sku = p.Sku, desc = p.Description, images = JsonSerializer.Deserialize<object[]>(p.ImagesJson, Json) ?? [], variants = JsonSerializer.Deserialize<object[]>(p.VariantsJson, Json) ?? [], specs = JsonSerializer.Deserialize<object[]>(p.SpecsJson, Json) ?? [] };
    private static object ToAdminResponse(Product p) => new { id = p.LegacyId ?? $"pr{p.Id:0000}", dbId = p.Id, legacyId = p.LegacyId, sellerId = p.SellerId is int sid ? $"s{sid}" : null, title = p.Name, category = p.Category?.Name, sub = p.Sub, price = p.Price, oldPrice = p.OldPrice, moq = p.Moq, sold = p.Sold, rating = p.Rating, reviews = p.Reviews, stock = p.Stock, weight = p.Weight, status = p.Status, tags = p.Tags, sku = p.Sku, desc = p.Description, images = JsonSerializer.Deserialize<object>(p.ImagesJson, Json) ?? Array.Empty<object>(), variants = p.VariantsJson, specs = p.SpecsJson };
    private static object ToReviewResponse(ProductReview r) => new { id = r.Id, productId = r.ProductId, buyer = r.BuyerName, country = r.BuyerCountry, rating = r.Rating, text = r.Text, variant = r.Variant, date = r.CreatedAtUtc.ToString("yyyy-MM-dd") };

    private async Task<Category?> ResolveCategory(JsonElement body, CancellationToken ct)
    {
        var id = ReadInt(body, "categoryId"); var name = ReadString(body, "category");
        if (id <= 0 && name?.StartsWith('c') == true && int.TryParse(name[1..], out var parsed)) id = parsed;
        return await repository.ResolveCategoryAsync(id, name, ct);
    }
    private static Product BuildProduct(JsonElement body, int categoryId, int? sellerId) => new()
    {
        LegacyId = ReadString(body, "legacyId"), Name = ReadString(body, "title") ?? ReadString(body, "name") ?? "", Sub = ReadString(body, "sub") ?? "",
        Description = ReadString(body, "desc") ?? ReadString(body, "description"), Price = ReadDecimal(body, "price"), OldPrice = ReadNullableDecimal(body, "oldPrice"),
        Moq = ReadInt(body, "moq", 1), Stock = ReadInt(body, "stock"), Weight = ReadDecimal(body, "weight", .2m), Sold = ReadInt(body, "sold"),
        Rating = ReadDecimal(body, "rating"), Reviews = ReadInt(body, "reviews"), Status = ReadString(body, "status") ?? "active", Currency = ReadString(body, "currency") ?? "USD",
        Sku = ReadString(body, "sku") ?? "", Tags = ReadStrings(body, "tags"), ImagesJson = Raw(body, "images", "[]"), VariantsJson = Raw(body, "variants", "[]"), SpecsJson = Raw(body, "specs", "[]"), SellerId = sellerId, CategoryId = categoryId
    };
    private static string? ReadString(JsonElement body, string key) => body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int ReadInt(JsonElement body, string key, int fallback = 0) => body.TryGetProperty(key, out var value) && value.TryGetInt32(out var number) ? number : fallback;
    private static decimal ReadDecimal(JsonElement body, string key, decimal fallback = 0) => body.TryGetProperty(key, out var value) && value.TryGetDecimal(out var number) ? number : fallback;
    private static decimal? ReadNullableDecimal(JsonElement body, string key) => body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;
    private static string[] ReadStrings(JsonElement body, string key) => body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
    private static string Raw(JsonElement body, string key, string fallback) => body.TryGetProperty(key, out var value) ? value.GetRawText() : fallback;
}
