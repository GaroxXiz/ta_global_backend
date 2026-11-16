namespace TAGlobal.Api.Models;

public sealed class ProductReview
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerCountry { get; set; } = "Indonesia";
    public int Rating { get; set; } = 5;
    public string Text { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}
