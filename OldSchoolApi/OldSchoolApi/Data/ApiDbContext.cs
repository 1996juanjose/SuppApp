using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Models;

namespace OldSchoolApi.Data;

public class ApiDbContext(DbContextOptions<ApiDbContext> options) : DbContext(options)
{
    public DbSet<CustomerRecord> CustomerRecords => Set<CustomerRecord>();
    public DbSet<CustomerRecordPayment> CustomerRecordPayments => Set<CustomerRecordPayment>();
    public DbSet<StatusCatalog> Statuses => Set<StatusCatalog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>().ToTable("AspNetUsers");
        builder.Entity<AppRole>().ToTable("AspNetRoles");
        builder.Entity<AppUserRole>().ToTable("AspNetUserRoles").HasKey(x => new { x.UserId, x.RoleId });

        builder.Entity<CustomerRecord>()
            .Property(x => x.ProductAmount).HasPrecision(10, 2);
        builder.Entity<CustomerRecord>()
            .Property(x => x.PaidAmount).HasPrecision(10, 2);
        builder.Entity<CustomerRecord>()
            .Property(x => x.BalanceDue).HasPrecision(10, 2);
        builder.Entity<CustomerRecord>()
            .Property(x => x.RecordDate).HasColumnType("date");
        builder.Entity<CustomerRecord>()
            .Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.Amount).HasPrecision(10, 2);
        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.PaymentDate).HasColumnType("date");
        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.ReversedAt).HasColumnType("timestamp without time zone");
        builder.Entity<ProductPrice>()
            .Property(x => x.Price).HasPrecision(10, 2);
    }
}
