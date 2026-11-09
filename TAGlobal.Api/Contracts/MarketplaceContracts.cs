namespace TAGlobal.Api.Contracts;
public sealed record RegisterRequest(string Name,string Username,string Email,string Password,string Country="Indonesia",string? StoreName=null);
public sealed record LoginRequest(string Email,string Password);
public sealed record AuthResponse(string Token,AuthUserResponse User);
public sealed record AuthUserResponse(int Id,string Username,string Name,string Email,string Country,string Role,string? SellerId,string? StoreName);
public sealed record UpdateProfileRequest(string Name,string Email,string Country);
public sealed record ProductSeed(string LegacyId,int CategoryId,int? SellerId,string Title,string Sub,decimal Price,decimal? OldPrice,int Moq,int Sold,decimal Rating,int Reviews,int Stock,decimal Weight,string Status,string[] Tags,string Sku,string Description,string[] Images,object[] Variants,object[] Specs);
public sealed record CreateOrderRequest(string Country,string City,string Address,string ProviderId,decimal Total,string? Note,List<CreateOrderItemRequest> Items);
public sealed record CreateOrderItemRequest(int ProductId,int Quantity,string Variant);
public sealed record OrderStatusRequest(string Status,int Stage,string? Awb);
public sealed record ThreadMessageRequest(string Text);
public sealed record CreateThreadRequest(string With,string Role,string Subject,string Party,string Text);

