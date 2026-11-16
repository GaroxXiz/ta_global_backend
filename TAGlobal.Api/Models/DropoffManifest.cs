namespace TAGlobal.Api.Models;

public sealed class DropoffManifest
{
    public int Id { get; set; }
    public string ManifestCode { get; set; } = string.Empty;
    public int SellerId { get; set; }
    public string HubId { get; set; } = "h2";
    public string Slot { get; set; } = string.Empty;
    public string OrderIdsJson { get; set; } = "[]";
    public int TotalOrders { get; set; }
    public decimal TotalWeight { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
