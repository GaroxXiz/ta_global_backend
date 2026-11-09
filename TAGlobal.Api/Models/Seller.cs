namespace TAGlobal.Api.Models;

public sealed class Seller
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Owner { get; set; } = "";
    public string Block { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Country { get; set; } = "Indonesia";
    public string Status { get; set; } = "pending";
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
