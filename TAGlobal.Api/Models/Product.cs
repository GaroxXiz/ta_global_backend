namespace TAGlobal.Api.Models;

public sealed class Product
{
    public int Id { get; set; }
    public string? LegacyId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int Stock { get; set; }
    public int Moq { get; set; } = 1;
    public int Sold { get; set; }
    public decimal Rating { get; set; }
    public int Reviews { get; set; }
    public decimal Weight { get; set; } = 0.2m;
    public string Status { get; set; } = "active";
    public string Sku { get; set; } = "";
    public string Sub { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public string ImagesJson { get; set; } = "[]";
    public string VariantsJson { get; set; } = "[]";
    public string SpecsJson { get; set; } = "[]";
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
