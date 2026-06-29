using Microsoft.EntityFrameworkCore;
using OldSchool.AuthService.Models;

namespace OldSchool.AuthService.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>().ToTable("AspNetUsers");
        builder.Entity<AppUser>().HasKey(x => x.Id);
        builder.Entity<AppUser>().Property(x => x.UserName).HasMaxLength(256);
        builder.Entity<AppUser>().Property(x => x.NormalizedUserName).HasMaxLength(256);
        builder.Entity<AppUser>().Property(x => x.PasswordHash).HasMaxLength(1000);
    }
}