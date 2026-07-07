using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Implementations.Transactions;
using PharmaCare.Application.Implementations.Finance;
using PharmaCare.Application.Implementations.Accounting;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;
using PharmaCare.Infrastructure;
using PharmaCare.Infrastructure.Implementations;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Application.Interfaces.Logging;

namespace PharmaCare.AuditTests
{
    public class MockSessionService : ISessionService
    {
        public Task InitializeSessionAsync(int userId) => Task.CompletedTask;
        public void ClearSession() { }
        public PharmaCare.Application.DTOs.Security.UserSessionInfo? GetCurrentUser() => new PharmaCare.Application.DTOs.Security.UserSessionInfo { UserId = 1, FullName = "System Auditor", RoleNames = new List<string> { "Admin" } };
        public List<PharmaCare.Application.DTOs.Security.PagePermission> GetAccessiblePages() => new List<PharmaCare.Application.DTOs.Security.PagePermission>();
        public bool HasPageAccess(string controller, string action, string permissionType) => true;
        public List<PharmaCare.Application.DTOs.Security.SidebarMenuItemDTO> GetSidebarMenu() => new List<PharmaCare.Application.DTOs.Security.SidebarMenuItemDTO>();
    }

    // Pins all DbContext reads/writes in this harness to the default pharmacy (tenant 1).
    public class MockCurrentTenant : PharmaCare.Application.Interfaces.Tenancy.ICurrentTenant
    {
        public int? TenantId => 1;
        public bool HasValue => true;
        public void SetTenant(int pharmacyId) { }
        public IDisposable BeginScope(int pharmacyId) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    public class MockActivityLogService : IActivityLogService
    {
        public Task LogActivityAsync(int userId, string userName, ActivityType activityType, string entityName, string? entityId = null, string? oldValues = null, string? newValues = null, string? description = null) => Task.CompletedTask;
        public Task<PharmaCare.Application.DTOs.Logging.ActivityLogPagedResult> GetLogsAsync(PharmaCare.Application.DTOs.Logging.ActivityLogFilterDto filter) => Task.FromResult(new PharmaCare.Application.DTOs.Logging.ActivityLogPagedResult());
        public Task<IEnumerable<PharmaCare.Application.DTOs.Logging.ActivityLogDto>> GetLogsByEntityAsync(string entityName, string entityId) => Task.FromResult(Enumerable.Empty<PharmaCare.Application.DTOs.Logging.ActivityLogDto>());
        public Task<IEnumerable<PharmaCare.Application.DTOs.Logging.ActivityLogDto>> GetLogsByUserAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null) => Task.FromResult(Enumerable.Empty<PharmaCare.Application.DTOs.Logging.ActivityLogDto>());
        public Task<PharmaCare.Application.DTOs.Logging.ActivityLogDto?> GetByIdAsync(long id) => Task.FromResult<PharmaCare.Application.DTOs.Logging.ActivityLogDto?>(null);
        public Task<ActivityLogSummary> GetSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null) => Task.FromResult(new ActivityLogSummary());
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddDbContext<PharmaCareDBContext>(options =>
                options.UseSqlServer("Server=Arsalan-NSD;Database=PharmaCareDB_Test;Trusted_Connection=True;Encrypt=false"));
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<PharmaCare.Application.Interfaces.Tenancy.ICurrentTenant, MockCurrentTenant>();
            services.AddScoped<IFinancialPeriodService, FinancialPeriodService>();
            services.AddScoped<ISaleService, SaleService>();
            services.AddScoped<ISaleReturnService, SaleReturnService>();
            services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
            services.AddScoped<IProductService, PharmaCare.Application.Implementations.Configuration.ProductService>();
            services.AddScoped<ISessionService, MockSessionService>();
            services.AddScoped<IActivityLogService, MockActivityLogService>();

            var serviceProvider = services.BuildServiceProvider();
            
            Console.WriteLine("=== FINAL AUDIT VERIFICATION ===");

            using var scope = serviceProvider.CreateScope();
            var saleService = scope.ServiceProvider.GetRequiredService<ISaleService>();
            var returnService = scope.ServiceProvider.GetRequiredService<ISaleReturnService>();
            var receiptService = scope.ServiceProvider.GetRequiredService<ICustomerPaymentService>();
            var db = scope.ServiceProvider.GetRequiredService<PharmaCareDBContext>();
            
            var product = await db.Set<Product>().FirstOrDefaultAsync(p => p.IsActive);
            var customer = await db.Set<Party>().FirstOrDefaultAsync(p => p.PartyType == "Customer");
            var cashAccount = await db.Set<Account>().FirstOrDefaultAsync(a => a.Name.Contains("Cash"));

            // 1. Verify Discount Hardening
            Console.WriteLine("\n[1] Testing Discount Hardening...");
            var sale = new StockMain {
                Party_ID = customer.PartyID,
                TransactionDate = DateTime.Now,
                DiscountPercent = 0,
                DiscountAmount = 999, // SPOOF
                StockDetails = new List<StockDetail> { new StockDetail { Product_ID = product.ProductID, Quantity = 1, UnitPrice = 1000 } }
            };
            var resultSale = await saleService.CreateAsync(sale, 1);
            if (resultSale.DiscountAmount == 0) Console.WriteLine("[FIXED] Discount spoof was reset to 0.");
            else Console.WriteLine("[FAILED] Discount spoof still allowed.");

            // 2. Verify Refund Hardening
            Console.WriteLine("\n[2] Testing Refund Hardening...");
            var refund = new Payment {
                Party_ID = customer.PartyID,
                Account_ID = cashAccount.AccountID,
                Amount = 1000000,
                PaymentDate = DateTime.Now,
                PaymentMethod = "Cash"
            };
            try {
                await receiptService.CreateRefundAsync(refund, 1);
                Console.WriteLine("[FAILED] Phantom refund still allowed.");
            } catch (Exception ex) {
                Console.WriteLine($"[FIXED] Refund rejected: {ex.Message}");
            }

            // 3. Verify Return Price Hardening
            Console.WriteLine("\n[3] Testing Return Price Hardening...");
            var ret = new StockMain {
                ReferenceStockMain_ID = resultSale.StockMainID,
                TransactionDate = DateTime.Now,
                StockDetails = new List<StockDetail> {
                    new StockDetail { Product_ID = product.ProductID, Quantity = 1, UnitPrice = 5000 } // SPOOF (Sold at 1000)
                }
            };
            var resultReturn = await returnService.CreateAsync(ret, 1);
            if (resultReturn.StockDetails.First().UnitPrice == 1000) Console.WriteLine("[FIXED] Return price was forced back to original sale price.");
            else Console.WriteLine($"[FAILED] Return price remains spoofed at {resultReturn.StockDetails.First().UnitPrice}.");
        }
    }
}
