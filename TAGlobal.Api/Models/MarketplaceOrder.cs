namespace TAGlobal.Api.Models;

public sealed class MarketplaceOrder
{
    public int Id { get; set; }
    public required string OrderNumber { get; set; }
    public string BuyerName { get; set; } = "";
    public string Country { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public int SellerId { get; set; }
    public string ProviderId { get; set; } = "";
    public decimal Total { get; set; }
    public string Status { get; set; } = "awaiting_acceptance";

    public int Stage { get; set; }
    public string Awb { get; set; } = "-";
    public string? Note { get; set; }
    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}


