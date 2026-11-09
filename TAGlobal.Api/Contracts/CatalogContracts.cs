using System.ComponentModel.DataAnnotations;
namespace TAGlobal.Api.Contracts;
public sealed record CreateCategoryRequest(string Name,string? Description);
public sealed record CreateProductRequest([property:Required,StringLength(200,MinimumLength=2)]string Name,[property:StringLength(4000)]string? Description,[property:Range(typeof(decimal),"0.01","9999999999")]decimal Price,[property:Required,RegularExpression("^[A-Z]{3}$")]string Currency,[property:Range(0,int.MaxValue)]int Stock,[property:Range(1,int.MaxValue)]int CategoryId);
public sealed record UpdateProductRequest([property:Required,StringLength(200,MinimumLength=2)]string Name,[property:StringLength(4000)]string? Description,[property:Range(typeof(decimal),"0.01","9999999999")]decimal Price,[property:Required,RegularExpression("^[A-Z]{3}$")]string Currency,[property:Range(0,int.MaxValue)]int Stock,[property:Range(1,int.MaxValue)]int CategoryId,bool IsActive);
