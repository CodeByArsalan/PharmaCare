using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Exceptions;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Locks in the defects found during the 2026-07-30 review. Each test fails against the code as it
/// was before that fix, so a regression is caught rather than rediscovered by reading the source.
/// </summary>
[Collection(Collections.Database)]
public class RegressionTests
{
    private readonly DatabaseFixture _fixture;

    public RegressionTests(DatabaseFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------- C-1: PO "Completed" status

    [Fact]
    public async Task Fully_receiving_a_purchase_order_marks_it_Completed()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var po = await tenant.Get<IPurchaseOrderService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 20, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await tenant.Get<IPurchaseOrderService>().ApproveAsync(po.StockMainID, TenantData.TestUserId);

        // Receive the whole order. Before the fix the status was decided in a read path and the
        // value was not permitted by CK_StockMains_Status_Valid, so persisting it threw.
        await tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = po.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 20, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        var stored = await tenant.Db.StockMains.AsNoTracking()
            .FirstAsync(s => s.StockMainID == po.StockMainID);

        Assert.Equal("Completed", stored.Status);
    }

    [Fact]
    public async Task Partially_receiving_a_purchase_order_leaves_it_Approved()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var po = await tenant.Get<IPurchaseOrderService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 20, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);
        await tenant.Get<IPurchaseOrderService>().ApproveAsync(po.StockMainID, TenantData.TestUserId);

        await tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = po.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        var stored = await tenant.Db.StockMains.AsNoTracking()
            .FirstAsync(s => s.StockMainID == po.StockMainID);

        Assert.Equal("Approved", stored.Status);
    }

    // ------------------------------------------------------- C-3: voided/unapproved rows in reports

    [Fact]
    public async Task Voided_receipts_are_excluded_from_cash_flow()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        // Credit sale, then collect against it, then void the receipt.
        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 20m }
            }
        }, TenantData.TestUserId);

        var receipt = await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
        {
            Party_ID = world.Customer.PartyID,
            StockMain_ID = sale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 200m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        var filter = new DateRangeFilter { FromDate = AppTime.Today, ToDate = AppTime.Today };
        var before = await tenant.Get<IFinancialReportService>().GetCashFlowReportAsync(filter);
        Assert.Equal(200m, before.TotalCashIn);

        await tenant.Get<ICustomerPaymentService>()
            .VoidReceiptAsync(receipt.PaymentID, "test", TenantData.TestUserId);

        var after = await tenant.Get<IFinancialReportService>().GetCashFlowReportAsync(filter);

        // The regression: a voided receipt used to keep counting as cash collected.
        Assert.Equal(0m, after.TotalCashIn);
    }

    [Fact]
    public async Task Draft_expenses_are_excluded_from_profit_and_loss()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var expenses = tenant.Get<IExpenseService>();

        var expenseAccount = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Damage & Loss");
        var cash = await tenant.CashAccountAsync();

        var category = await expenses.CreateCategoryAsync(new ExpenseCategory
        {
            Name = "Utilities",
            DefaultExpenseAccount_ID = expenseAccount.AccountID,
            IsActive = true
        }, TenantData.TestUserId);

        // Created but NOT approved.
        await expenses.CreateAsync(new Expense
        {
            ExpenseCategory_ID = category.ExpenseCategoryID,
            SourceAccount_ID = cash.AccountID,
            ExpenseAccount_ID = expenseAccount.AccountID,
            Amount = 500m,
            ExpenseDate = AppTime.Now,
            Description = "Unapproved draft"
        }, TenantData.TestUserId);

        var pl = await tenant.Get<IFinancialReportService>().GetProfitLossAsync(
            new DateRangeFilter { FromDate = AppTime.Today, ToDate = AppTime.Today });

        // The regression: draft (and voided) expenses used to inflate TotalExpenses.
        Assert.Equal(0m, pl.TotalExpenses);
    }

    // --------------------------------------------------------------------- H-3: audit + rollback

    [Fact]
    public async Task A_rolled_back_transaction_writes_no_audit_rows()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var log = tenant.Get<PharmaCare.Infrastructure.LogDbContext>();
        var before = await log.ActivityLogs.CountAsync();

        var uow = tenant.Get<IUnitOfWork>();
        await uow.BeginTransactionAsync();

        world.Product.ReorderLevel = 999;
        tenant.Db.Products.Update(world.Product);
        await uow.SaveChangesAsync();

        var during = await CountLogsAsync(tenant);
        Assert.Equal(before, during);   // buffered, not written

        await uow.RollbackTransactionAsync();

        // The regression: audit rows used to be written per SaveChanges, so a rollback left the log
        // describing changes that never happened.
        Assert.Equal(before, await CountLogsAsync(tenant));
    }

    [Fact]
    public async Task A_committed_transaction_does_write_audit_rows()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var before = await CountLogsAsync(tenant);

        var uow = tenant.Get<IUnitOfWork>();
        await uow.BeginTransactionAsync();
        world.Product.ReorderLevel = 42;
        tenant.Db.Products.Update(world.Product);
        await uow.SaveChangesAsync();
        await uow.CommitTransactionAsync();

        Assert.True(await CountLogsAsync(tenant) > before,
            "Deferred audit rows were not flushed on commit — auditing is silently broken.");
    }

    private static async Task<int> CountLogsAsync(TenantScope tenant)
    {
        // A fresh context each time so the count is read from the database, not a cached one.
        var options = new DbContextOptionsBuilder<PharmaCare.Infrastructure.LogDbContext>()
            .UseSqlServer(tenant.Get<PharmaCare.Infrastructure.LogDbContext>().Database.GetConnectionString())
            .Options;
        using var fresh = new PharmaCare.Infrastructure.LogDbContext(options);
        return await fresh.ActivityLogs.CountAsync();
    }

    // ------------------------------------------------------------------------- M-1: credit limit

    [Fact]
    public async Task Credit_sale_beyond_the_limit_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);
        var supplier = await tenant.SeedSupplierAsync();
        var customer = await tenant.SeedCustomerAsync("Capped Customer", creditLimit: 100m);
        await tenant.ReceiveStockAsync(supplier, product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<CreditLimitExceededException>(() =>
            tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = customer.PartyID,
                TransactionDate = AppTime.Now,
                PaidAmount = 0,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = product.ProductID, Quantity = 20, UnitPrice = 20m }
                }
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task An_authorised_override_lets_the_credit_sale_through_and_records_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);
        var supplier = await tenant.SeedSupplierAsync();
        var customer = await tenant.SeedCustomerAsync("Capped Customer", creditLimit: 100m);
        await tenant.ReceiveStockAsync(supplier, product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = 20, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, paymentAccountId: null, overrideCreditLimit: true);

        Assert.Contains("Credit limit override", sale.Remarks ?? string.Empty);
    }

    [Fact]
    public async Task A_fully_paid_sale_is_not_subject_to_the_credit_limit()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);
        var supplier = await tenant.SeedSupplierAsync();
        var customer = await tenant.SeedCustomerAsync("Capped Customer", creditLimit: 100m);
        var cash = await tenant.CashAccountAsync();
        await tenant.ReceiveStockAsync(supplier, product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 400m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = 20, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, cash.AccountID);

        Assert.Equal(PaymentStatus.Paid.ToString(), sale.PaymentStatus);
    }
}
