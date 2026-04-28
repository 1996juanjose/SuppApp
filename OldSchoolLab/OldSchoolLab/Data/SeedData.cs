using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Models;

namespace OldSchoolLab.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var db = scopedServices.GetRequiredService<ApplicationDbContext>();
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE IF EXISTS "CustomerRecords"
            ALTER COLUMN "RecordDate" TYPE date
            USING "RecordDate"::date;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE IF EXISTS "AuditLogs"
            ALTER COLUMN "ChangedAt" TYPE timestamp without time zone
            USING "ChangedAt"::timestamp without time zone;
            """);


        var roles = new[] { "Gerencia", "Gestor", "Monitoreo" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "ADMIN", "ADMIN", "Gerencia");
        await EnsureUserAsync(userManager, "gerencia", "Gerencia123!", "Gerencia");
        await EnsureUserAsync(userManager, "gestor", "Gestor123!", "Gestor");
        await EnsureUserAsync(userManager, "monitoreo", "Monitoreo123!", "Monitoreo");

        var legacyClienteStatus = await db.Statuses.FirstOrDefaultAsync(x => x.Name == "Cliente");
        var clientesStatus = await db.Statuses.FirstOrDefaultAsync(x => x.Name == "Clientes");

        if (legacyClienteStatus is not null && clientesStatus is null)
        {
            legacyClienteStatus.Name = "Clientes";
            clientesStatus = legacyClienteStatus;
        }

        var defaultStatuses = new[]
        {
            new { Name = "Clientes", BadgeClass = "success", SortOrder = 1 },
            new { Name = "Rechazo", BadgeClass = "danger", SortOrder = 2 },
            new { Name = "Interesado", BadgeClass = "info", SortOrder = 3 },
            new { Name = "Por Pagar", BadgeClass = "warning", SortOrder = 4 },
            new { Name = "Prospecto", BadgeClass = "primary", SortOrder = 5 }
        };

        foreach (var item in defaultStatuses)
        {
            var status = await db.Statuses.FirstOrDefaultAsync(x => x.Name == item.Name);
            if (status is null)
            {
                db.Statuses.Add(new StatusCatalog
                {
                    Name = item.Name,
                    BadgeClass = item.BadgeClass,
                    SortOrder = item.SortOrder,
                    IsActive = true
                });
            }
            else
            {
                status.BadgeClass = item.BadgeClass;
                status.SortOrder = item.SortOrder;
                status.IsActive = true;
            }
        }

        var creatina = await db.Products
            .Include(x => x.Prices)
            .FirstOrDefaultAsync(x => x.Name == "Creatina");

        if (creatina is null)
        {
            creatina = new Product
            {
                Name = "Creatina",
                IsActive = true,
                Prices = new List<ProductPrice>
                {
                    new() { Quantity = 1, Price = 89m },
                    new() { Quantity = 2, Price = 149m },
                    new() { Quantity = 3, Price = 189m }
                }
            };

            db.Products.Add(creatina);
        }
        else
        {
            creatina.IsActive = true;
            await EnsurePriceAsync(db, creatina.Id, 1, 89m);
            await EnsurePriceAsync(db, creatina.Id, 2, 149m);
            await EnsurePriceAsync(db, creatina.Id, 3, 189m);
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string username, string password, string role)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = username,
                Email = $"{username}@local.test",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var error = string.Join(", ", result.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"No se pudo crear el usuario {username}: {error}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0)
            {
                await userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            await userManager.AddToRoleAsync(user, role);
        }
    }

    private static async Task EnsurePriceAsync(ApplicationDbContext db, int productId, int quantity, decimal price)
    {
        var productPrice = await db.ProductPrices.FirstOrDefaultAsync(x => x.ProductId == productId && x.Quantity == quantity);
        if (productPrice is null)
        {
            db.ProductPrices.Add(new ProductPrice
            {
                ProductId = productId,
                Quantity = quantity,
                Price = price
            });

            return;
        }

        productPrice.Price = price;
    }
}
