using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using PharmaCare.Application.Implementations;
using PharmaCare.Application.Implementations.Membership;
using PharmaCare.Application.Implementations.SaleManagement;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Application.Interfaces.PurchaseManagement;
using PharmaCare.Application.Implementations.Inventory;
using PharmaCare.Application.Implementations.PurchaseManagement;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Implementations.Reports;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Implementations.Finance;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Infrastructure;
using PharmaCare.Infrastructure.Implementations;
using PharmaCare.Infrastructure.Implementations.Membership;
using PharmaCare.Infrastructure.Implementations.PointOfSale;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Membership;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Implementations.Configuration;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("PharmaCareDBConnectionString") ?? throw new InvalidOperationException("Connection string 'PharmaCareDBConnectionString' not found.");
var connectionString2 = builder.Configuration.GetConnectionString("PharmaCareLogDBConnectionString") ?? throw new InvalidOperationException("Connection string 'PharmaCareLogDBConnectionString' not found.");

builder.Services.AddScoped<DbContext, PharmaCareDBContext>();
builder.Services.AddDbContext<PharmaCareDBContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddDefaultIdentity<SystemUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<PharmaCareDBContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IPosService, PosService>();
builder.Services.AddScoped<IPosRepository, PosRepository>();
builder.Services.AddScoped<ISystemUserService, SystemUserService>();
builder.Services.AddScoped<ISystemUserRepository, SystemUserRepository>();
builder.Services.AddScoped<IUserTypeService, UserTypeService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IGrnService, GrnService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IComboBoxRepository, ComboBoxRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ISubCategoryService, SubCategoryService>();
builder.Services.AddScoped<IStockAlertService, StockAlertService>();
builder.Services.AddScoped<IBarcodeService, BarcodeService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
builder.Services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
// Sale Enhancement Services
builder.Services.AddScoped<ISalesReturnService, SalesReturnService>();
builder.Services.AddScoped<IHeldSaleService, HeldSaleService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.AccountManagement.IAccountingService, PharmaCare.Application.Implementations.AccountManagement.AccountingService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.AccountManagement.IHeadService, PharmaCare.Application.Implementations.AccountManagement.HeadService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.AccountManagement.ISubheadService, PharmaCare.Application.Implementations.AccountManagement.SubheadService>();
builder.Services.AddScoped<IPartyService, PartyService>();
builder.Services.AddScoped<PharmaCare.Application.Interfaces.AccountManagement.IAccountMappingService, PharmaCare.Application.Implementations.AccountManagement.AccountMappingService>();
builder.Services.AddScoped<PharmaCare.Domain.Interfaces.IStoreContext, StoreContext>();

// Unit of Work for atomic transactions (Step 5.2)
builder.Services.AddScoped<IUnitOfWork, PharmaCare.Infrastructure.Persistence.UnitOfWork>();

// Journal Posting Engine for centralized accounting (Step 5.3)
builder.Services.AddScoped<PharmaCare.Infrastructure.Interfaces.Accounting.IJournalPostingEngine, PharmaCare.Infrastructure.Implementations.Accounting.JournalPostingEngine>();

// Inventory Accounting Service for atomic stock + accounting (Step 5.4)
builder.Services.AddScoped<PharmaCare.Infrastructure.Inventory.FifoCostCalculator>();
builder.Services.AddScoped<PharmaCare.Infrastructure.Interfaces.Inventory.IInventoryAccountingService, PharmaCare.Infrastructure.Inventory.InventoryAccountingService>();

// Fiscal Period Service for period management (Step 5.6)
builder.Services.AddScoped<PharmaCare.Infrastructure.Interfaces.Accounting.IFiscalPeriodService, PharmaCare.Infrastructure.Implementations.Accounting.FiscalPeriodService>();

// Fiscal Period Initializer - auto-creates fiscal year on startup
builder.Services.AddScoped<PharmaCare.Infrastructure.Implementations.Accounting.FiscalPeriodInitializer>();

// Add session support for cart
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Initialize fiscal year on startup
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<PharmaCare.Infrastructure.Implementations.Accounting.FiscalPeriodInitializer>();
    await initializer.EnsureCurrentFiscalYearExistsAsync();
}


// Configure the HTTP request pipeline.
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
app.UseSession(); // Enable session
app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();
app.Run();
