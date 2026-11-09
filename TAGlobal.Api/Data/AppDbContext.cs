using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<MarketplaceOrder> Orders => Set<MarketplaceOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<MarketplaceSetting> Settings => Set<MarketplaceSetting>();
    public DbSet<ChatThread> Threads => Set<ChatThread>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(4000);
            entity.Property(product => product.Price).HasPrecision(12, 2);
            entity.Property(product => product.OldPrice).HasPrecision(12, 2);
            entity.Property(product => product.Rating).HasPrecision(4, 2);
            entity.Property(product => product.Weight).HasPrecision(8, 3);
            entity.Property(product => product.Currency).HasMaxLength(3).IsRequired();
            entity.Property(product => product.Tags).HasColumnType("text[]");
            entity.Property(product => product.ImagesJson).HasColumnType("jsonb");
            entity.Property(product => product.VariantsJson).HasColumnType("jsonb");
            entity.Property(product => product.SpecsJson).HasColumnType("jsonb");
            entity.HasOne(product => product.Category).WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(product => product.Seller).WithMany(seller => seller.Products)
                .HasForeignKey(product => product.SellerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(product => product.Name);
            entity.HasIndex(product => product.LegacyId).IsUnique();
        });

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.HasKey(seller => seller.Id);
            entity.Property(seller => seller.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(seller => seller.Email);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(50).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.Name).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(20).IsRequired();
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasOne(user => user.Seller).WithOne(seller => seller.User)
                .HasForeignKey<User>(user => user.SellerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MarketplaceOrder>(entity =>
        {
            entity.HasKey(order => order.Id);
            entity.Property(order => order.OrderNumber).HasMaxLength(32).IsRequired();
            entity.Property(order => order.Total).HasPrecision(12, 2);
            entity.HasIndex(order => order.OrderNumber).IsUnique();
            entity.HasMany(order => order.Items).WithOne(item => item.Order)
                .HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(item => item.ProductName).HasMaxLength(200);
            entity.Property(item => item.Variant).HasMaxLength(200);
        });

        modelBuilder.Entity<MarketplaceSetting>(entity =>
        {
            entity.HasKey(setting => setting.Id);
            entity.HasIndex(setting => setting.Key).IsUnique();
            entity.Property(setting => setting.ValueJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ChatThread>(entity =>
        {
            entity.HasKey(thread => thread.Id);
            entity.Property(thread => thread.ThreadKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(thread => thread.ThreadKey).IsUnique();
            entity.HasMany(thread => thread.Messages).WithOne(message => message.Thread)
                .HasForeignKey(message => message.ThreadId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}




