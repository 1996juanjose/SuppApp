extern alias Web;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Db = Web::OldSchoolLab.Data.ApplicationDbContext;
using Cash = Web::OldSchoolLab.Services.CashRegisterService;

static class PostgreSqlCashRegression
{
    // Opt-in integration test. All tables and movements live in a new scratch schema.
    public static async Task RunAsync(string appSettingsPath)
    {
        var config = new ConfigurationManager();
        config.AddJsonFile(Path.GetFullPath(appSettingsPath));
        var connection = config.GetConnectionString("DefaultConnection") ?? throw new Exception("Database connection missing");
        var schema = "oldschool_cash_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var isolated = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
            var options = new DbContextOptionsBuilder<Db>().UseNpgsql(isolated.ConnectionString,
                npgsql => npgsql.EnableRetryOnFailure()).Options;
            await using var db = new Db(options);
            var sql = db.Database.GenerateCreateScript();
            if (sql.Contains("\"public\".")) throw new Exception("Schema isolation check failed");
            await db.Database.ExecuteSqlRawAsync(sql);
            db.Companies.Add(new Web::OldSchoolLab.Models.Company { Id = 1, Name = "Isolated regression" });
            await db.SaveChangesAsync();
            var actor = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "ADMIN"),
                new Claim(ClaimTypes.NameIdentifier, "isolated-test"), new Claim("company_id", "1") }, "test"));
            var service = new Cash(db, config);
            var box = await service.OpenAsync(1, actor);
            await service.AddExpenseAsync(1, box.Id, "Isolated test expense", 1m, null, actor);
            await service.CloseAsync(1, box.Id, actor);
            if (await service.GetOpenAsync(1) is not null || await db.Expenses.CountAsync() != 1 || !box.ClosedAt.HasValue)
                throw new Exception("PostgreSQL cash lifecycle did not persist correctly");
            var second = await service.OpenAsync(1, actor);
            if (second.Id == box.Id) throw new Exception("Reopen did not create a new period");
            Console.WriteLine("PASS real PostgreSQL retry strategy: open, expense, close and reopen in isolated schema");
        }
        finally
        {
            // This identifier is generated above, never supplied by the caller.
            if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^oldschool_cash_test_[a-f0-9]{32}$"))
                throw new Exception("Refusing cleanup outside the generated test schema");
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
