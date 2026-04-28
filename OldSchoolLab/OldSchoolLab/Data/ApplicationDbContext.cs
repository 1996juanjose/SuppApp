using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Models;

namespace OldSchoolLab.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<StatusCatalog> Statuses => Set<StatusCatalog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<CustomerRecord> CustomerRecords => Set<CustomerRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<StatusCatalog>()
            .HasIndex(x => x.Name)
            .IsUnique();

        builder.Entity<Product>()
            .HasIndex(x => x.Name)
            .IsUnique();

        builder.Entity<ProductPrice>()
            .HasIndex(x => new { x.ProductId, x.Quantity })
            .IsUnique();

        builder.Entity<ProductPrice>()
            .Property(x => x.Price)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.ProductAmount)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.PaidAmount)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.BalanceDue)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .HasOne(x => x.StatusCatalog)
            .WithMany(x => x.CustomerRecords)
            .HasForeignKey(x => x.StatusCatalogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CustomerRecord>()
            .HasOne(x => x.Product)
            .WithMany(x => x.CustomerRecords)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
