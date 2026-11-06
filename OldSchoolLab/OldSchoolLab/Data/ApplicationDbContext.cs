using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Models;

namespace OldSchoolLab.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<StatusCatalog> Statuses => Set<StatusCatalog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<ProductCommissionTier> ProductCommissionTiers => Set<ProductCommissionTier>();
    public DbSet<ProductStockMovement> ProductStockMovements => Set<ProductStockMovement>();
    public DbSet<CustomerRecord> CustomerRecords => Set<CustomerRecord>();
    public DbSet<CustomerRecordPayment> CustomerRecordPayments => Set<CustomerRecordPayment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<LeadCallAttempt> LeadCallAttempts => Set<LeadCallAttempt>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<WhatsAppContact> WhatsAppContacts => Set<WhatsAppContact>();
    public DbSet<WhatsAppConsentChange> WhatsAppConsentChanges => Set<WhatsAppConsentChange>();
    public DbSet<WhatsAppMessageSetting> WhatsAppMessageSettings => Set<WhatsAppMessageSetting>();
    public DbSet<WhatsAppCampaign> WhatsAppCampaigns => Set<WhatsAppCampaign>();
    public DbSet<WhatsAppCampaignRecipient> WhatsAppCampaignRecipients => Set<WhatsAppCampaignRecipient>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<WhatsAppContact>().HasIndex(x => new { x.CompanyId, x.Phone }).IsUnique();
        builder.Entity<WhatsAppMessageSetting>().HasIndex(x => new { x.CompanyId, x.UserId }).IsUnique();
        builder.Entity<WhatsAppCampaign>().HasIndex(x => new { x.CompanyId, x.UserId, x.CreatedAt });
        builder.Entity<WhatsAppCampaignRecipient>().HasIndex(x => new { x.CampaignId, x.Phone }).IsUnique();
        builder.Entity<WhatsAppCampaignRecipient>().HasIndex(x => new { x.CompanyId, x.Phone, x.Status, x.DeclaredSentAt });
        builder.Entity<WhatsAppCampaignRecipient>().HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<WhatsAppConsentChange>().HasIndex(x => new { x.CompanyId, x.Phone, x.ChangedAt });
        builder.Entity<WhatsAppContact>().Property(x => x.ConsentExpiresAt).HasColumnType("timestamp without time zone");
        builder.Entity<WhatsAppContact>().Property(x => x.ChangedAt).HasColumnType("timestamp without time zone");
        builder.Entity<WhatsAppConsentChange>().Property(x => x.ExpiresAt).HasColumnType("timestamp without time zone");
        builder.Entity<WhatsAppConsentChange>().Property(x => x.ChangedAt).HasColumnType("timestamp without time zone");
        builder.Entity<WhatsAppCampaign>().Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        builder.Entity<WhatsAppCampaignRecipient>().Property(x => x.OpenedAt).HasColumnType("timestamp without time zone");
        builder.Entity<WhatsAppCampaignRecipient>().Property(x => x.DeclaredSentAt).HasColumnType("timestamp without time zone");
        builder.Entity<WhatsAppCampaignRecipient>().Property(x => x.UpdatedAt).HasColumnType("timestamp without time zone");
        builder.Entity<LeadCallAttempt>().HasIndex(x => new { x.CompanyId, x.RecordId, x.CreatedAt });
        builder.Entity<LeadCallAttempt>().Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        builder.Entity<LeadCallAttempt>().Property(x => x.NextCallAt).HasColumnType("timestamp without time zone");
        builder.Entity<LeadCallAttempt>().HasOne<CustomerRecord>().WithMany().HasForeignKey(x => x.RecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CashRegister>().HasIndex(x => x.CompanyId).IsUnique().HasFilter("\"ClosedAt\" IS NULL");
        builder.Entity<CashRegister>().Property(x => x.OpenedAt).HasColumnType("timestamp without time zone");
        builder.Entity<CashRegister>().Property(x => x.ClosedAt).HasColumnType("timestamp without time zone").IsConcurrencyToken();
        builder.Entity<Expense>().HasOne<CashRegister>().WithMany().HasForeignKey(x => x.CashRegisterId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Company>()
            .HasIndex(x => x.Name)
            .IsUnique();

        builder.Entity<StatusCatalog>()
            .HasIndex(x => new { x.CompanyId, x.Name })
            .IsUnique();

        builder.Entity<Product>()
            .HasIndex(x => new { x.CompanyId, x.Name })
            .IsUnique();

        builder.Entity<ProductPrice>()
            .HasIndex(x => new { x.ProductId, x.Quantity })
            .IsUnique();

        builder.Entity<ProductPrice>()
            .Property(x => x.Price)
            .HasPrecision(10, 2);

        builder.Entity<Product>()
            .Property(x => x.PurchaseUnitCost)
            .HasPrecision(10, 2);

        builder.Entity<ProductCommissionTier>()
            .HasIndex(x => new { x.ProductId, x.Quantity })
            .IsUnique();

        builder.Entity<ProductCommissionTier>()
            .Property(x => x.CommissionRate)
            .HasPrecision(5, 2);

        builder.Entity<ProductStockMovement>()
            .HasIndex(x => new { x.ProductId, x.MovementDate });

        builder.Entity<ProductStockMovement>()
            .Property(x => x.MovementDate)
            .HasColumnType("date");

        builder.Entity<ProductStockMovement>()
            .Property(x => x.CreatedAt)
            .HasColumnType("timestamp without time zone");

        builder.Entity<ProductStockMovement>()
            .Property(x => x.UnitCost)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.ProductAmount)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.PurchaseUnitCost)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.CostAmount)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.CommissionRate)
            .HasPrecision(5, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.CommissionAmount)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.PaidAmount)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.BalanceDue)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.Amount)
            .HasPrecision(10, 2);

        builder.Entity<Expense>()
            .Property(x => x.Amount)
            .HasPrecision(10, 2);

        builder.Entity<CustomerRecord>()
            .Property(x => x.RecordDate)
            .HasColumnType("date");

        builder.Entity<CustomerRecord>()
            .Property(x => x.CreatedAt)
            .HasColumnType("timestamp without time zone");

        builder.Entity<CustomerRecord>()
            .Property(x => x.CallScheduledAt)
            .HasColumnType("timestamp without time zone");

        builder.Entity<CustomerRecord>()
            .Property(x => x.IsCallConcrete)
            .HasDefaultValue(false);

        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.PaymentDate)
            .HasColumnType("date");

        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.CreatedAt)
            .HasColumnType("timestamp without time zone");

        builder.Entity<Expense>()
            .Property(x => x.ExpenseDate)
            .HasColumnType("date");

        builder.Entity<Expense>()
            .Property(x => x.CreatedAt)
            .HasColumnType("timestamp without time zone");

        builder.Entity<CustomerRecordPayment>()
            .Property(x => x.ReversedAt)
            .HasColumnType("timestamp without time zone");

        builder.Entity<AuditLog>()
            .Property(x => x.ChangedAt)
            .HasColumnType("timestamp without time zone");

        builder.Entity<ApplicationUser>()
            .HasOne(x => x.Company)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Product>()
            .HasOne(x => x.Company)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Product>()
            .HasMany(x => x.CommissionTiers)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Product>()
            .HasMany(x => x.StockMovements)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StatusCatalog>()
            .HasOne(x => x.Company)
            .WithMany(x => x.Statuses)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CustomerRecord>()
            .HasOne(x => x.Company)
            .WithMany(x => x.Records)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AuditLog>()
            .HasOne(x => x.Company)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.Entity<ProductStockMovement>()
            .HasOne(x => x.CustomerRecord)
            .WithMany()
            .HasForeignKey(x => x.CustomerRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<CustomerRecordPayment>()
            .HasOne(x => x.CustomerRecord)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.CustomerRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CustomerRecordPayment>()
            .HasIndex(x => new { x.CustomerRecordId, x.PaymentDate });

        builder.Entity<Expense>()
            .HasIndex(x => new { x.CompanyId, x.ExpenseDate });

        builder.Entity<Expense>()
            .HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
