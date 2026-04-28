using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Models;

namespace OldSchoolApi.Data;

public class ApiDbContext(DbContextOptions<ApiDbContext> options) : DbContext(options)
{
    public DbSet<CustomerRecord> CustomerRecords => Set<CustomerRecord>();
    public DbSet<StatusCatalog> Statuses => Set<StatusCatalog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Mapear a las mismas tablas que Identity usa OldSchoolLab
        builder.Entity<AppUser>().ToTable("AspNetUsers");
        builder.Entity<AppRole>().ToTable("AspNetRoles");
        builder.Entity<AppUserRole>().ToTable("AspNetUserRoles").HasKey(x => new { x.UserId, x.RoleId });

        builder.Entity<CustomerRecord>()
            .Property(x => x.ProductAmount).HasPrecision(10, 2);
        builder.Entity<CustomerRecord>()
            .Property(x => x.PaidAmount).HasPrecision(10, 2);
        builder.Entity<CustomerRecord>()
            .Property(x => x.BalanceDue).HasPrecision(10, 2);
        builder.Entity<ProductPrice>()
            .Property(x => x.Price).HasPrecision(10, 2);
    }
}
