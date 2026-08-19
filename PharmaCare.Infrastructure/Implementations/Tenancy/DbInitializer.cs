using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Infrastructure.Implementations.Tenancy;

/// <summary>
/// Idempotent startup initializer for DATA that EF migrations do not own: the global
/// (cross-pharmacy) reference tables — account types, transaction types, voucher types — the page
/// catalog, and the first platform super-admin. Safe to run on every startup; it only fills gaps.
///
/// <para>
/// Schema belongs to migrations, never here. An earlier version patched a missing column onto the
/// log database with a raw ALTER TABLE at startup; because that change never reached the model
/// snapshot, the next scaffolded migration mistook the column for a rename and would have destroyed
/// data. Add a migration instead.
/// </para>
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        try
        {
            var context = sp.GetRequiredService<PharmaCareDBContext>();
            await SeedAccountTypesAsync(context);
            await SeedTransactionTypesAsync(context);
            await SeedVoucherTypesAsync(context);
            await PageCatalog.SeedAsync(context, logger);

            await EnsurePlatformAdminAsync(sp, logger);
        }
        catch (Exception ex)
        {
            // Don't crash startup if the database isn't migrated yet; log and continue.
            logger.LogError(ex, "DbInitializer failed. Ensure migrations have been applied (dotnet ef database update).");
        }
    }

    private static async Task SeedAccountTypesAsync(PharmaCareDBContext context)
    {
        // Order matters on a fresh DB: it must reproduce the ids the Category dropdowns hard-code
        // (Cash=1, Bank=2 for SaleService; STK=6, COGS=7, SALE=8, DMG=9 for the category accounts).
        // Codes must match what TenantProvisioningService resolves against — no INV/EXP duplicates.
        var wanted = new (string Code, string Name)[]
        {
            ("CASH", "Cash Account"),
            ("BANK", "Bank Account"),
            ("AR", "Accounts Receivable"),
            ("AP", "Accounts Payable"),
            ("GEN", "General"),
            ("STK", "Stock Account"),
            ("COGS", "COGS Account"),
            ("SALE", "Sale Account"),
            ("DMG", "Damaged"),
        };

        var existing = await context.AccountTypes.Select(t => t.Code).ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var toAdd = wanted.Where(w => !existingSet.Contains(w.Code))
            .Select(w => new AccountType { Code = w.Code, Name = w.Name }).ToList();
        if (toAdd.Count > 0)
        {
            context.AccountTypes.AddRange(toAdd);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedTransactionTypesAsync(PharmaCareDBContext context)
    {
        var wanted = new (string Code, string Name, string Category, int Dir, bool Affects, bool Voucher)[]
        {
            ("PO",    "Purchase Order",       "PURCHASE",   0, false, false),
            ("GRN",   "Goods Received Note",  "PURCHASE",  +1, true,  true),
            ("PRTN",  "Purchase Return",      "PURCHASE",  -1, true,  true),
            ("SALE",  "Sale",                 "SALE",      -1, true,  true),
            ("SRTN",  "Sale Return",          "SALE",      +1, true,  true),
            ("SADJ+", "Stock Adjustment In",  "INVENTORY", +1, true,  true),
            ("SADJ-", "Stock Adjustment Out", "INVENTORY", -1, true,  true),
        };

        var existing = new HashSet<string>(await context.TransactionTypes.Select(t => t.Code).ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var toAdd = wanted.Where(w => !existing.Contains(w.Code))
            .Select(w => new TransactionType
            {
                Code = w.Code, Name = w.Name, Category = w.Category,
                StockDirection = w.Dir, AffectsStock = w.Affects, CreatesVoucher = w.Voucher, IsActive = true
            }).ToList();
        if (toAdd.Count > 0)
        {
            context.TransactionTypes.AddRange(toAdd);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedVoucherTypesAsync(PharmaCareDBContext context)
    {
        var wanted = new (string Code, string Name, bool Auto)[]
        {
            ("JV",  "Journal Voucher",          false),
            ("PV",  "Purchase Voucher",         true),
            ("SV",  "Sales Voucher",            true),
            ("PRV", "Purchase Return Voucher",  true),
            ("SRT", "Sale Return Voucher",      true),
            ("CR",  "Cash Receipt",             true),
            ("BR",  "Bank Receipt",             true),
            ("CP",  "Cash Payment",             true),
            ("BP",  "Bank Payment",             true),
            ("EXP", "Expense Voucher",          true),
        };

        var existing = new HashSet<string>(await context.VoucherTypes.Select(t => t.Code).ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var toAdd = wanted.Where(w => !existing.Contains(w.Code))
            .Select(w => new VoucherType { Code = w.Code, Name = w.Name, IsAutoGenerated = w.Auto, IsActive = true }).ToList();
        if (toAdd.Count > 0)
        {
            context.VoucherTypes.AddRange(toAdd);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>Creates a platform super-admin (no pharmacy) if none exists, using credentials
    /// from the "PlatformAdmin" configuration section.</summary>
    private static async Task EnsurePlatformAdminAsync(IServiceProvider sp, ILogger logger)
    {
        var context = sp.GetRequiredService<PharmaCareDBContext>();
        if (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.IsPlatformAdmin))
        {
            return;
        }

        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["PlatformAdmin:Email"];
        var password = config["PlatformAdmin:Password"];

        // No baked-in fallback credentials: a well-known default here would hand over the
        // whole platform. Without explicit config we create nothing and surface a critical log.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogCritical(
                "No platform super-admin exists and PlatformAdmin:Email / PlatformAdmin:Password are not configured. " +
                "Set them via user-secrets, environment variables, or appsettings and restart — until then no one can manage pharmacies.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<User>>();
        if (await userManager.FindByEmailAsync(email) != null)
        {
            return;
        }

        var admin = new User
        {
            UserName = email,
            Email = email,
            FullName = "Arsalan",
            IsActive = true,
            Pharmacy_ID = null,
            IsPlatformAdmin = true,
            // The config password is bootstrap-only: it sits in plaintext config, so the first
            // login must replace it before the account can do anything else.
            MustChangePassword = true,
            CreatedAt = AppTime.Now,
            CreatedBy = 0
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            logger.LogInformation("Created initial platform super-admin '{Email}' from configuration.", email);
        }
        else
        {
            logger.LogError("Failed to create platform super-admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
