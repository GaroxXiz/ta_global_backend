namespace TAGlobal.Api.Models;

public sealed class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public string Country { get; set; } = "Indonesia";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "buyer";
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    public string? CartJson { get; set; }
    public string? WishlistJson { get; set; }
    public string? SellerShippingJson { get; set; }
    public int? SellerHubId { get; set; }
    public int HandlingDays { get; set; } = 2;
}
