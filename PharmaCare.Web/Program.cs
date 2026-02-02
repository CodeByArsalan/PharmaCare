using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Infrastructure;
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

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("PharmaCareDBConnectionString") 
    ?? throw new InvalidOperationException("Connection string 'PharmaCareDBConnectionString' not found.");
var logConnectionString = builder.Configuration.GetConnectionString("PharmaCareLogDBConnectionString")
    ?? throw new InvalidOperationException("Connection string 'PharmaCareLogDBConnectionString' not found.");

// Logging Database Context (separate database for audit logs)
builder.Services.AddDbContext<LogDbContext>(options => options.UseSqlServer(logConnectionString));

// Register the audit interceptor and logging repository
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();

// Database Context with audit interceptor
builder.Services.AddDbContext<PharmaCareDBContext>((serviceProvider, options) => 
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
});


// Identity
builder.Services.AddDefaultIdentity<User>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<PharmaCareDBContext>();

// Configure Authentication Cookie for "Remember Me" functionality
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Cookie valid for 30 days when "Remember Me" is checked
    options.SlidingExpiration = true; // Refresh the cookie on each request
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
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

// Purchase Management Services
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();

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
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Set global culture to use PKR
var defaultCulture = new System.Globalization.CultureInfo("en-US");
defaultCulture.NumberFormat.CurrencySymbol = "PKR ";
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
app.UseSession();
app.UseAuthentication();
app.UseSessionInitialization(); // Re-initialize session for "Remember Me" users
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();
app.Run();
