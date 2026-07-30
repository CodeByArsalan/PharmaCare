using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PharmaCare.Application.Implementations.Accounting;
using PharmaCare.Application.Implementations.Configuration;
using PharmaCare.Application.Implementations.Finance;
using PharmaCare.Application.Implementations.Logging;
using PharmaCare.Application.Implementations.Transactions;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Infrastructure;
using PharmaCare.Infrastructure.Implementations;
using PharmaCare.Infrastructure.Implementations.Logging;
using PharmaCare.Infrastructure.Implementations.Reports;
using PharmaCare.Infrastructure.Implementations.Tenancy;
using PharmaCare.Infrastructure.Interceptors;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Owns the throwaway SQL Server databases these tests run against.
///
/// <para>
/// Deliberately a REAL database, not in-memory or SQLite. The defects this suite exists to catch
/// live precisely in what those providers cannot reproduce: CHECK constraints, filtered unique
/// indexes, <c>sp_getapplock</c>, EF global query filters over a real provider, and a second
/// database for the audit log that cannot join the business transaction.
/// </para>
///
/// <para>
/// The databases are dropped and re-created from migrations once per run, so the migrations
/// themselves are exercised on every execution — a broken or missing migration fails here.
/// </para>
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string DefaultServer = "Server=Arsalan-NSD;Trusted_Connection=True;Encrypt=false";

    public string ConnectionString { get; }
    public string LogConnectionString { get; }

    private ServiceProvider _provider = null!;

    public DatabaseFixture()
    {
        // Override for a different server/instance with PHARMACARE_TEST_SQL.
        var server = Environment.GetEnvironmentVariable("PHARMACARE_TEST_SQL") ?? DefaultServer;
        ConnectionString = $"{server};Database=PharmaCareDB_IntegrationTests";
        LogConnectionString = $"{server};Database=PharmaCareDB_IntegrationTests_Log";
    }

    public async Task InitializeAsync()
    {
        AppTime.Initialize("Asia/Karachi");

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Mirrors the registrations in PharmaCare.Web/Program.cs. Kept explicit rather than booting
        // the web host so these tests exercise services directly without HTTP or auth plumbing.
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<LogDbContext>(o => o.UseSqlServer(LogConnectionString));
        services.AddDbContext<PharmaCareDBContext>((sp, o) =>
        {
            o.UseSqlServer(ConnectionString);
            o.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        // The production tenant resolver works without an HttpContext: it falls back to an explicit
        // override, which is exactly what SetTenant/BeginScope give us here.
        services.AddScoped<ICurrentTenant, CurrentTenantService>();
        services.AddScoped<IPharmacyService, PharmacyService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();

        // Mirrors the production password policy in Program.cs. Relaxing it here would let a test
        // create a user the real application would reject.
        services.AddIdentityCore<User>(o =>
        {
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireNonAlphanumeric = false;
            o.Password.RequireUppercase = true;
            o.Password.RequiredLength = 8;
            o.Password.RequiredUniqueChars = 1;
            o.User.RequireUniqueEmail = true;
        }).AddEntityFrameworkStores<PharmaCareDBContext>();

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<ISessionService, TestSessionService>();

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISubCategoryService, SubCategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IPartyService, PartyService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IProfitSettingsService, ProfitSettingsService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();
        services.AddScoped<IJournalVoucherService, JournalVoucherService>();

        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<ISaleReturnService, SaleReturnService>();
        services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();

        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<IExpenseService, ExpenseService>();

        services.AddScoped<ISalesReportService, SalesReportService>();
        services.AddScoped<IPurchaseReportService, PurchaseReportService>();
        services.AddScoped<IInventoryReportService, InventoryReportService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();

        _provider = services.BuildServiceProvider();

        await ResetDatabasesAsync();
        await SeedGlobalReferenceDataAsync();
    }

    /// <summary>
    /// Drops and rebuilds both databases from migrations. Running the migrations (rather than
    /// EnsureCreated) means a migration that does not apply cleanly fails the suite.
    /// </summary>
    private async Task ResetDatabasesAsync()
    {
        using var scope = _provider.CreateScope();

        var log = scope.ServiceProvider.GetRequiredService<LogDbContext>();
        await log.Database.EnsureDeletedAsync();
        await log.Database.MigrateAsync();

        var db = scope.ServiceProvider.GetRequiredService<PharmaCareDBContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Seeds the GLOBAL (cross-pharmacy) reference rows that DbInitializer owns in production:
    /// account types, transaction types, voucher types, and the page catalog.
    /// </summary>
    private async Task SeedGlobalReferenceDataAsync()
    {
        using var scope = _provider.CreateScope();
        await DbInitializer.InitializeAsync(_provider);
    }

    /// <summary>
    /// Provisions a brand-new pharmacy and returns a scope bound to it.
    /// <para>
    /// Every test gets its own tenant, which is what makes them independent without any truncation
    /// between runs: the global query filters mean one test physically cannot see another's rows.
    /// It also means the provisioning path itself is covered by every test.
    /// </para>
    /// </summary>
    public async Task<TenantScope> NewTenantAsync(string? label = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var code = $"T{suffix}";

        var scope = _provider.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

        var result = await provisioning.ProvisionAsync(new ProvisionPharmacyRequest
        {
            PharmacyName = label ?? $"Test Pharmacy {suffix}",
            PharmacyCode = code,
            AdminEmail = $"admin-{suffix}@test.local",
            AdminPassword = "TestPass123",
            AdminFullName = "Test Admin"
        }, actingUserId: 0);

        if (!result.Success || result.PharmacyId <= 0)
        {
            scope.Dispose();
            throw new InvalidOperationException($"Could not provision a test pharmacy: {result.ErrorMessage}");
        }

        var pharmacyId = result.PharmacyId;

        // Fresh scope so the DbContext starts with an empty change tracker and the tenant set.
        scope.Dispose();
        return new TenantScope(_provider.CreateScope(), pharmacyId);
    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>A DI scope pinned to one provisioned pharmacy.</summary>
public sealed class TenantScope : IDisposable
{
    private readonly IServiceScope _scope;

    public int PharmacyId { get; }
    public IServiceProvider Services => _scope.ServiceProvider;

    public TenantScope(IServiceScope scope, int pharmacyId)
    {
        _scope = scope;
        PharmacyId = pharmacyId;
        _scope.ServiceProvider.GetRequiredService<ICurrentTenant>().SetTenant(pharmacyId);
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public PharmaCareDBContext Db => Get<PharmaCareDBContext>();

    public void Dispose() => _scope.Dispose();
}

[CollectionDefinition(Collections.Database)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

public static class Collections
{
    /// <summary>
    /// All database tests share one collection so xUnit runs them sequentially — they share a
    /// server and the fixture rebuilds the schema once.
    /// </summary>
    public const string Database = "Database";
}
