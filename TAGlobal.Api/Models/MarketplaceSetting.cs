namespace TAGlobal.Api.Models;

public sealed class MarketplaceSetting
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string ValueJson { get; set; }
}
