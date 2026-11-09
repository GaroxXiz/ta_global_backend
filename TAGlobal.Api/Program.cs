using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Text;
using TAGlobal.Api.Data;
using TAGlobal.Api.Models;
using TAGlobal.Api.Security;

var builder = WebApplication.CreateBuilder(args);
if (args.Length == 1 && args[0] == "provision-admin")
{
    var username = Environment.GetEnvironmentVariable("TAGLOBAL_ADMIN_USERNAME") ?? "superadmin_taglobal";
    var email = Environment.GetEnvironmentVariable("TAGLOBAL_ADMIN_EMAIL") ?? "superadmin@taglobal.com";
    var password = Environment.GetEnvironmentVariable("TAGLOBAL_ADMIN_PASSWORD");
    if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Admin password missing from process environment.");
    var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.CommandTimeout(5)).Options;
    await using var db = new AppDbContext(options);
    await db.Database.MigrateAsync();
    await AdminProvisioner.EnsureSuperadminAsync(db, username, email, password);
    Console.WriteLine($"Provisioned {username} ({email}); password stored as BCrypt hash.");
    return;
}
var envFile = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".env"));
if (File.Exists(envFile))
{
    foreach (var rawLine in await File.ReadAllLinesAsync(envFile))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        var separator = line.IndexOf('=');
        if (separator <= 0) continue;
        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
        Environment.SetEnvironmentVariable(key, value);
    }
}
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "TAGlobal API", Version = "v1" }));
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AuthService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    var secret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("Configure Jwt__Secret in .env (minimum 32 bytes).");
    o.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TAGlobal.Api", ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TAGlobal.Frontend", ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), NameClaimType = System.Security.Claims.ClaimTypes.Name, RoleClaimType = System.Security.Claims.ClaimTypes.Role };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "TAGlobal API v1")); }
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DevelopmentSeed.ApplyAsync(db);
    var adminPassword = builder.Configuration["Seed:SuperadminPassword"];
    if (!string.IsNullOrWhiteSpace(adminPassword) && adminPassword.Length >= 12)
        await AdminProvisioner.EnsureSuperadminAsync(db, "superadmin_taglobal", "superadmin@taglobal.com", adminPassword);
}
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();


