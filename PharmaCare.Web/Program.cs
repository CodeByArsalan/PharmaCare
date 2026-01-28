using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Infrastructure;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Domain.Interfaces;
using PharmaCare.Application.Interfaces;
using PharmaCare.Infrastructure.Implementations;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("PharmaCareDBConnectionString") 
    ?? throw new InvalidOperationException("Connection string 'PharmaCareDBConnectionString' not found.");

// Database Context
builder.Services.AddScoped<DbContext, PharmaCareDBContext>();
builder.Services.AddDbContext<PharmaCareDBContext>(options => options.UseSqlServer(connectionString));

// Store Context for multi-tenancy
builder.Services.AddScoped<IStoreContext, StoreContext>();

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

// Core Services
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();
app.Run();
