using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Infrastructure;
using PharmaCare.Infrastructure.Implementations.Tenancy;

namespace PharmaCare.WebTests;

/// <summary>
/// Boots PharmaCare.Web in-process (TestServer) against its OWN throwaway SQL databases and drives
/// the real HTTP pipeline — auth cookie, session, the page-permission authorization filter, model
/// binding and antiforgery all run exactly as in production. Nothing is stubbed: the point of these
/// tests is to catch defects that only surface over HTTP, so faking any of that plumbing would
/// defeat them.
///
/// <para>
/// The app itself never migrates; it only seeds reference data (DbInitializer) at startup. So the
/// fixture drops + migrates both databases once, RE-runs DbInitializer (its first startup pass ran
/// before the tables existed and quietly no-opped), then provisions one tenant through the real
/// provisioning service — giving a known admin login — and one deliberately restricted view-only
/// user for the RBAC probes.
/// </para>
/// </summary>
public sealed class PharmaCareWebFactory : WebApplicationFactory<Program>
{
    private const string DefaultSqlServer = "Server=Arsalan-NSD;Trusted_Connection=True;Encrypt=false";

    public string ConnectionString { get; }
    public string LogConnectionString { get; }

    public PharmaCareWebFactory()
    {
        // Same override as PharmaCare.IntegrationTests' DatabaseFixture: without it these tests only
        // run on the machine whose server name is baked in above.
        var sqlServer = Environment.GetEnvironmentVariable("PHARMACARE_TEST_SQL") ?? DefaultSqlServer;

        // Dedicated web-test databases, never the shared integration ones. A suffix keeps parallel
        // runs apart (the "web" auditor runs with PHARMACARE_TEST_DB_SUFFIX=_WEB).
        var suffix = Environment.GetEnvironmentVariable("PHARMACARE_TEST_DB_SUFFIX") ?? string.Empty;
        ConnectionString = $"{sqlServer};Database=PharmaCareDB_WebTests{suffix}";
        LogConnectionString = $"{sqlServer};Database=PharmaCareDB_WebTests{suffix}_Log";

        // Program.cs reads the connection strings at WebApplication.CreateBuilder time, before any
        // WebApplicationFactory ConfigureAppConfiguration hook can layer on top (appsettings.json
        // ships an explicit empty value that would otherwise win). Environment variables ARE picked
        // up by CreateBuilder's AddEnvironmentVariables, so set them here as the reliable override.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__PharmaCareDBConnectionString", ConnectionString);
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__PharmaCareLogDBConnectionString", LogConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development so the auth/session cookies use CookieSecurePolicy.SameAsRequest — the
        // TestServer speaks HTTP, and a Secure-only cookie would never be stored, breaking login.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PharmaCareDBConnectionString"] = ConnectionString,
                ["ConnectionStrings:PharmaCareLogDBConnectionString"] = LogConnectionString,
                // Disable the nightly log-retention hosted job during tests.
                ["LogRetention:Enabled"] = "false",
            });
        });
    }

    /// <summary>Drops, migrates and seeds both databases, then provisions the test tenant/users.</summary>
    public async Task ResetAndSeedAsync()
    {
        // Touch the host so it builds (its own DbInitializer pass runs now and no-ops on empty DB).
        _ = Services;

        using (var scope = Services.CreateScope())
        {
            // LogDbContext ships no migrations (the audit DB is schema-managed from the model), so
            // build it with EnsureCreated — MigrateAsync would leave ActivityLogs missing and every
            // activity-logging write (login included) would 500.
            var log = scope.ServiceProvider.GetRequiredService<LogDbContext>();
            await log.Database.EnsureDeletedAsync();
            await log.Database.EnsureCreatedAsync();

            var db = scope.ServiceProvider.GetRequiredService<PharmaCareDBContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }

        // Now that the schema exists, seed the global reference data + page catalog.
        await DbInitializer.InitializeAsync(Services);
    }

    /// <summary>Runs <paramref name="action"/> in a DI scope pinned to <paramref name="pharmacyId"/>.</summary>
    public async Task RunInTenantScopeAsync(int pharmacyId, Func<IServiceProvider, Task> action)
    {
        using var scope = Services.CreateScope();
        if (pharmacyId > 0) scope.ServiceProvider.GetRequiredService<ICurrentTenant>().SetTenant(pharmacyId);
        await action(scope.ServiceProvider);
    }

    public async Task<T> RunInTenantScopeAsync<T>(int pharmacyId, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        if (pharmacyId > 0) scope.ServiceProvider.GetRequiredService<ICurrentTenant>().SetTenant(pharmacyId);
        return await action(scope.ServiceProvider);
    }
}

/// <summary>
/// One provisioned tenant + its known logins, shared across every probe class so the expensive
/// drop/migrate/seed/provision happens once per test run.
/// </summary>
public sealed class WebTestFixture : IAsyncLifetime
{
    public PharmaCareWebFactory Factory { get; } = new();

    public int PharmacyId { get; private set; }

    // Full administrator (all pages, all permissions) provisioned by TenantProvisioningService.
    public string AdminEmail { get; private set; } = default!;
    public string AdminPassword { get; } = "WebTestPass123";

    // Deliberately restricted: VIEW permission only, on the JournalVoucher, SupplierPayment,
    // Sale and Purchase pages. Used to prove the RBAC void-permission gap.
    public string ViewOnlyEmail { get; private set; } = default!;
    public string ViewOnlyPassword { get; } = "ViewOnlyPass123";

    public async Task InitializeAsync()
    {
        AppTime.Initialize("Asia/Karachi");
        await Factory.ResetAndSeedAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        AdminEmail = $"admin-{suffix}@webtest.local";
        ViewOnlyEmail = $"viewonly-{suffix}@webtest.local";

        // 1. Provision the tenant through the real service — creates the admin user + role.
        PharmacyId = await Factory.RunInTenantScopeAsync(0, async sp =>
        {
            var provisioning = sp.GetRequiredService<ITenantProvisioningService>();
            var result = await provisioning.ProvisionAsync(new ProvisionPharmacyRequest
            {
                PharmacyName = $"Web Test Pharmacy {suffix}",
                PharmacyCode = $"W{suffix}",
                AdminEmail = AdminEmail,
                AdminPassword = AdminPassword,
                AdminFullName = "Web Test Admin"
            }, actingUserId: 0);

            if (!result.Success || result.PharmacyId <= 0)
                throw new InvalidOperationException($"Provisioning failed: {result.ErrorMessage}");
            return result.PharmacyId;
        });

        // 2. Create a VIEW-ONLY role + user in the same tenant.
        await Factory.RunInTenantScopeAsync(PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var userManager = sp.GetRequiredService<UserManager<User>>();

            // Pages whose void/reverse endpoints we want to reach with only view rights.
            string[] viewablePages = { "JournalVoucher", "SupplierPayment", "Sale", "Purchase" };

            var role = new Role
            {
                Name = "Web Test Viewer",
                Description = "View-only role for RBAC probes.",
                IsSystemRole = false,
                IsActive = true,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            db.Roles_Custom.Add(role);
            await db.SaveChangesAsync();

            var pages = await db.Pages
                .Where(p => p.Controller != null && viewablePages.Contains(p.Controller))
                .ToListAsync();

            foreach (var page in pages)
            {
                db.RolePages.Add(new RolePage
                {
                    Role_ID = role.RoleID,
                    Page_ID = page.PageID,
                    CanView = true,     // view only
                    CanCreate = false,
                    CanEdit = false,
                    CanDelete = false
                });
            }
            await db.SaveChangesAsync();

            var viewer = new User
            {
                UserName = ViewOnlyEmail,
                Email = ViewOnlyEmail,
                FullName = "View Only",
                IsActive = true,
                Pharmacy_ID = PharmacyId,
                IsPlatformAdmin = false,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            var created = await userManager.CreateAsync(viewer, ViewOnlyPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("View-only user creation failed: " +
                    string.Join("; ", created.Errors.Select(e => e.Description)));

            db.UserRoles_Custom.Add(new UserRole { User_ID = viewer.Id, Role_ID = role.RoleID });
            await db.SaveChangesAsync();
        });
    }

    private HttpClient? _adminClient;
    private HttpClient? _viewOnlyClient;
    private readonly SemaphoreSlim _loginGate = new(1, 1);

    /// <summary>A single admin session shared across probes. The login endpoint is rate-limited to
    /// 10/min, so every probe logging in separately trips HTTP 429 — logging in ONCE avoids that
    /// artifact without weakening any endpoint under test.</summary>
    public async Task<HttpClient> AdminClientAsync()
    {
        await _loginGate.WaitAsync();
        try
        {
            if (_adminClient == null)
            {
                _adminClient = Factory.CreateTestClient();
                await HttpTestHelpers.LoginOrThrowAsync(_adminClient, AdminEmail, AdminPassword);
            }
            return _adminClient;
        }
        finally { _loginGate.Release(); }
    }

    public async Task<HttpClient> ViewOnlyClientAsync()
    {
        await _loginGate.WaitAsync();
        try
        {
            if (_viewOnlyClient == null)
            {
                _viewOnlyClient = Factory.CreateTestClient();
                await HttpTestHelpers.LoginOrThrowAsync(_viewOnlyClient, ViewOnlyEmail, ViewOnlyPassword);
            }
            return _viewOnlyClient;
        }
        finally { _loginGate.Release(); }
    }

    public Task DisposeAsync()
    {
        _adminClient?.Dispose();
        _viewOnlyClient?.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(WebCollection.Name)]
public sealed class WebCollectionDefinition : ICollectionFixture<WebTestFixture> { }

public static class WebCollection
{
    public const string Name = "WebHarness";
}
