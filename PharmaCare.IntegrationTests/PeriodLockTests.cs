using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Closing a financial period is the control that stops the books being rewritten after they have
/// been reported on. It is only worth anything if EVERY path that can reach the general ledger
/// honours it — one unguarded posting route makes the lock decorative.
///
/// <para>
/// Each test seeds and transacts BEFORE closing the period, then closes it and attempts the
/// post-close operation, so the only thing under test is the lock itself.
/// </para>
/// </summary>
[Collection(Collections.Database)]
public class PeriodLockTests
{
    private readonly DatabaseFixture _fixture;

    public PeriodLockTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Sale_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now,
                PaidAmount = 100m,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 20m }
                }
            }, TenantData.TestUserId, world.Cash.AccountID));
    }

    [Fact]
    public async Task Goods_receipt_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m));
    }

    [Fact]
    public async Task Stock_adjustment_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
            {
                TransactionDate = AppTime.Now,
                AdjustmentType = "Write-off",
                AdjustmentReason = "Expired",
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1 }
                }
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Sale_return_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 100m);
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
            {
                ReferenceStockMain_ID = sale.StockMainID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1, UnitPrice = 20m }
                }
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Purchase_return_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
            {
                Party_ID = world.Supplier.PartyID,
                ReferenceStockMain_ID = grn.StockMainID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1, CostPrice = 10m }
                }
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Customer_receipt_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 0m);
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
            {
                StockMain_ID = sale.StockMainID,
                Account_ID = world.Cash.AccountID,
                Amount = 50m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Expense_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var category = await tenant.SeedExpenseCategoryAsync();
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>().CreateAsync(new Expense
            {
                ExpenseCategory_ID = category.ExpenseCategoryID,
                SourceAccount_ID = world.Cash.AccountID,
                Amount = 500m,
                ExpenseDate = AppTime.Now,
                Description = "Rent"
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Approving_a_draft_expense_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var category = await tenant.SeedExpenseCategoryAsync();

        var expense = await tenant.Get<IExpenseService>().CreateAsync(new Expense
        {
            ExpenseCategory_ID = category.ExpenseCategoryID,
            SourceAccount_ID = world.Cash.AccountID,
            Amount = 500m,
            ExpenseDate = AppTime.Now,
            Description = "Rent"
        }, TenantData.TestUserId);

        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_a_sale_out_of_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 100m);
        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "mistake", TenantData.TestUserId));
    }

    /// <summary>
    /// The manual journal voucher is the most direct route into the general ledger there is, and
    /// the one an operator would reach for precisely BECAUSE the transaction screens refuse a
    /// closed period. If this posts, the period lock can be walked around at will.
    /// </summary>
    [Fact]
    public async Task Journal_voucher_into_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");
        var jvType = await tenant.Db.VoucherTypes.FirstAsync(t => t.Code == "JV");

        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IJournalVoucherService>().CreateJournalVoucherAsync(new JournalVoucherDto
            {
                VoucherType_ID = jvType.VoucherTypeID,
                VoucherDate = AppTime.Now,
                Narration = "Backdated into a closed period",
                VoucherDetails = new List<JournalVoucherDetailDto>
                {
                    new() { Account_ID = world.Cash.AccountID, DebitAmount = 10_000m, CreditAmount = 0 },
                    new() { Account_ID = revenue.AccountID, DebitAmount = 0, CreditAmount = 10_000m }
                }
            }, TenantData.TestUserId));
    }

    /// <summary>
    /// The mirror of the above: reversing a voucher writes new GL lines, so it is a posting too.
    /// </summary>
    [Fact]
    public async Task Voiding_a_journal_voucher_out_of_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");
        var jvType = await tenant.Db.VoucherTypes.FirstAsync(t => t.Code == "JV");

        var voucher = await tenant.Get<IJournalVoucherService>().CreateJournalVoucherAsync(new JournalVoucherDto
        {
            VoucherType_ID = jvType.VoucherTypeID,
            VoucherDate = AppTime.Now,
            Narration = "Opening adjustment",
            VoucherDetails = new List<JournalVoucherDetailDto>
            {
                new() { Account_ID = world.Cash.AccountID, DebitAmount = 1_000m, CreditAmount = 0 },
                new() { Account_ID = revenue.AccountID, DebitAmount = 0, CreditAmount = 1_000m }
            }
        }, TenantData.TestUserId);

        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IJournalVoucherService>().VoidVoucherAsync(voucher.VoucherID, "reopened", TenantData.TestUserId));
    }

    /// <summary>
    /// An opening-balance edit posts a voucher dated today, so it reaches the ledger exactly like
    /// a journal does and must be refused the same way.
    /// </summary>
    [Fact]
    public async Task Changing_a_party_opening_balance_in_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var customer = await tenant.SeedCustomerAsync("Ledger Customer", openingBalance: 100m);

        await tenant.CloseCurrentPeriodAsync();

        // Revise the balance 100 -> 250, which is what a party edit would post as a 150 delta.
        var tracked = await tenant.Db.Parties.FirstAsync(p => p.PartyID == customer.PartyID);
        tracked.OpeningBalance = 250m;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IOpeningBalanceService>()
                .PostOpeningBalanceDeltaAsync(tracked, previousBalance: 100m, TenantData.TestUserId));
    }

    /// <summary>
    /// Saving a party without touching its opening balance posts nothing, so a closed period must
    /// not block ordinary edits like a phone-number change.
    /// </summary>
    [Fact]
    public async Task Saving_a_party_with_an_unchanged_opening_balance_is_allowed_in_a_closed_period()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var customer = await tenant.SeedCustomerAsync("Ledger Customer", openingBalance: 100m);

        await tenant.CloseCurrentPeriodAsync();

        var tracked = await tenant.Db.Parties.FirstAsync(p => p.PartyID == customer.PartyID);
        var exception = await Record.ExceptionAsync(() =>
            tenant.Get<IOpeningBalanceService>()
                .PostOpeningBalanceDeltaAsync(tracked, previousBalance: tracked.OpeningBalance, TenantData.TestUserId));

        Assert.Null(exception);
    }

    /// <summary>
    /// Re-opening a period must let work resume — a one-way lock would be a support burden.
    /// </summary>
    [Fact]
    public async Task Re_opening_the_period_lets_the_sale_through_again()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.CloseCurrentPeriodAsync();

        var period = await tenant.Db.FinancialPeriods
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);
        await tenant.Get<IFinancialPeriodService>().OpenPeriodAsync(period.PeriodID, TenantData.TestUserId);

        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 100m);
        Assert.Equal("Approved", sale.Status);
    }
}
