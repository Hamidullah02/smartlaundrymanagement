using LaundryMVC.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LaundryMVC.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ServiceCatalogItem> ServiceCatalog => Set<ServiceCatalogItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Order>(entity =>
        {
            entity.Property(o => o.QrToken).IsRequired(false);
            entity.Property(o => o.QrPngBase64).IsRequired(false);
            entity.HasIndex(o => o.QrToken).IsUnique();
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.CustomerId);
        });
    }
}
