using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Infrastructure;
using PharmaCare.Web.Utilities;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Application.Interfaces;
using PharmaCare.Infrastructure.Implementations;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Implementations.Configuration;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Application.Implementations.Security;
using PharmaCare.Infrastructure.Implementations.Security;
using PharmaCare.Web.Middleware;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Implementations.Accounting;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Application.Implementations.Logging;
using PharmaCare.Infrastructure.Interceptors;
using PharmaCare.Infrastructure.Implementations.Logging;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Implementations.Transactions;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Implementations.Finance;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("PharmaCareDBConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'PharmaCareDBConnectionString' is not configured. Set it via appsettings.Development.json, environment variables, or user-secrets.");
var logConnectionString = builder.Configuration.GetConnectionString("PharmaCareLogDBConnectionString");
if (string.IsNullOrWhiteSpace(logConnectionString))
    throw new InvalidOperationException("Connection string 'PharmaCareLogDBConnectionString' is not configured. Set it via appsettings.Development.json, environment variables, or user-secrets.");

// Logging Database Context (separate database for audit logs)
builder.Services.AddDbContext<LogDbContext>(options => options.UseSqlServer(logConnectionString));

// Register the audit interceptor and logging repository
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();

// Ambient tenant (current pharmacy) — resolved from the auth claim / explicit override.
// Scoped so it shares the request scope with the DbContext that consumes it.
builder.Services.AddScoped<PharmaCare.Application.Interfaces.Tenancy.ICurrentTenant, PharmaCare.Infrastructure.Implementations.Tenancy.CurrentTenantService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.Tenancy.IPharmacyService, PharmaCare.Infrastructure.Implementations.Tenancy.PharmacyService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.Tenancy.ITenantProvisioningService, PharmaCare.Infrastructure.Implementations.Tenancy.TenantProvisioningService>();

// Platform super-admin gate: only users carrying the IsPlatformAdmin claim reach the /Pharmacies area.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireClaim(PharmaCare.Infrastructure.Implementations.Tenancy.TenantClaimsPrincipalFactory.PlatformAdminClaimType, "true"));
});

// Activity-log retention: nightly job that archives ActivityLogs older than the hot
// window and purges archived rows older than the archive window (see "LogRetention" config).
builder.Services.Configure<LogRetentionOptions>(builder.Configuration.GetSection(LogRetentionOptions.SectionName));
builder.Services.AddHostedService<LogRetentionService>();

// Database Context with audit interceptor
builder.Services.AddDbContext<PharmaCareDBContext>((serviceProvider, options) => 
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
});


// Data Protection — persist keys to a stable on-disk location and pin the application
// name so issued tokens (e.g. encrypted URL IDs) stay valid across restarts/deployments.
// NOTE: the Keys folder contains the master key and must NEVER be committed to source control.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "Keys")))
    .SetApplicationName("PharmaCare");

// Identity
builder.Services.AddDefaultIdentity<User>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    // Password policy — strengthened for a system holding financial/inventory data.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    options.User.RequireUniqueEmail = true;

    // Account lockout — throttles brute-force / credential-stuffing attacks.
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    // Emit the pharmacy (tenant) claim into the auth cookie at sign-in.
    .AddClaimsPrincipalFactory<PharmaCare.Infrastructure.Implementations.Tenancy.TenantClaimsPrincipalFactory>()
    .AddEntityFrameworkStores<PharmaCareDBContext>();

// Re-validate the security stamp every 5 minutes (default is 30): deactivating a user or
// suspending a pharmacy rotates their stamp, so revocation takes effect within this window.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

// Configure Authentication Cookie for "Remember Me" functionality
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8); // Bounded session (was 30 days)
    options.SlidingExpiration = true; // Refresh the cookie on activity
    options.Cookie.HttpOnly = true;
    // Always require HTTPS for the auth cookie outside Development (where local HTTP is common).
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Rate limiting — primary defense against brute-force on authentication endpoints,
// with a relaxed per-IP global limiter as a safety net against abusive clients.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Applied to the login endpoint via [EnableRateLimiting("auth")].
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Core Services
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IComboboxRepository, ComboboxRepository>();

// Security Repositories (Infrastructure Layer)
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IUserRoleRepository, UserRoleRepository>();
builder.Services.AddScoped<IRolePageRepository, RolePageRepository>();
builder.Services.AddScoped<IPageRepository, PageRepository>();
builder.Services.AddScoped<IUserManager, UserManagerAdapter>();

// Application Services - Clean Architecture
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ISubCategoryService, SubCategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IPartyService, PartyService>();
builder.Services.AddScoped<IAccountHeadService, AccountHeadService>();
builder.Services.AddScoped<IAccountSubHeadService, AccountSubHeadService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IJournalVoucherService, JournalVoucherService>();
builder.Services.AddScoped<IProfitSettingsService, ProfitSettingsService>();
builder.Services.AddScoped<IPricingService, PricingService>();

// Purchase Management Services
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();

// Sales Management Services
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<ISaleReturnService, SaleReturnService>();
builder.Services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();

// Finance Services
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
builder.Services.AddScoped<ISupplierCreditNoteService, SupplierCreditNoteService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();

// Reporting Services
builder.Services.AddScoped<PharmaCare.Application.Interfaces.Reports.ISalesReportService, PharmaCare.Infrastructure.Implementations.Reports.SalesReportService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.Reports.IPurchaseReportService, PharmaCare.Infrastructure.Implementations.Reports.PurchaseReportService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.Reports.IInventoryReportService, PharmaCare.Infrastructure.Implementations.Reports.InventoryReportService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.Reports.IFinancialReportService, PharmaCare.Infrastructure.Implementations.Reports.FinancialReportService>();

// Logging Services
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// Security Services (Application Layer)
builder.Services.AddScoped<IUserService, PharmaCare.Application.Implementations.Security.UserService>();
builder.Services.AddScoped<IRoleService, PharmaCare.Application.Implementations.Security.RoleService>();

// Session and Authorization Services
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<PharmaCare.Web.Filters.PageAuthorizationFilter>();

// HTTP Context for AuthService
builder.Services.AddHttpContextAccessor();

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

// Wire the static URL-ID protector (Utility.EncryptId/DecryptId) to Data Protection.
Utility.Initialize(app.Services.GetRequiredService<IDataProtectionProvider>());

// Wire the display currency (symbol/code) from the "Currency" config section.
CurrencyDisplay.Initialize(app.Configuration);

// Pin the business clock (AppTime.Now/Today) to the configured timezone — Pakistan time by
// default — so timestamps and daily document numbers do not shift with the server's OS clock.
AppTime.Initialize(app.Configuration["AppTime:TimeZone"]);

// Security response headers applied to every response.
// In Development, connect-src is relaxed to allow Visual Studio's Browser Link / hot-reload
// (ws/wss + localhost) and CDN sourcemaps; production keeps the strict "connect-src 'self'".
var cspIsDevelopment = app.Environment.IsDevelopment();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-XSS-Protection"] = "0"; // Deprecated header; explicitly disabled in favor of CSP.

    var connectSrc = cspIsDevelopment
        ? "connect-src 'self' ws: wss: http://localhost:* https://localhost:*; "
        : "connect-src 'self'; ";

    // All frontend assets are vendored under wwwroot/lib, so no external script/style/font
    // origins are allowed. 'unsafe-inline' for scripts remains until inline <script> blocks
    // move to nonce-based CSP (deliberately deferred).
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "img-src 'self' data:; " +
        connectSrc +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "object-src 'none'";
    await next();
});

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

// Set global culture; currency symbol comes from the "Currency" config section.
var defaultCulture = new System.Globalization.CultureInfo("en-US");
defaultCulture.NumberFormat.CurrencySymbol = CurrencyDisplay.Symbol + " ";
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(defaultCulture),
    SupportedCultures = new List<System.Globalization.CultureInfo> { defaultCulture },
    SupportedUICultures = new List<System.Globalization.CultureInfo> { defaultCulture }
};
app.UseRequestLocalization(localizationOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseSessionInitialization(); // Re-initialize session for "Remember Me" users
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();

// Idempotently ensure global reference data, the log-DB tenant column, and a platform super-admin.
await PharmaCare.Infrastructure.Implementations.Tenancy.DbInitializer.InitializeAsync(app.Services);

app.Run();
