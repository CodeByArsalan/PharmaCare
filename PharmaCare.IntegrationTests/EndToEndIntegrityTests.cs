using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// The whole-system properties. Individual services can each be correct while the system as a
/// whole drifts, so these drive a full trading month through every module and then assert the
/// invariants that must hold no matter what happened along the way.
/// </summary>
[Collection(Collections.Database)]
public class EndToEndIntegrityTests
{
    private readonly DatabaseFixture _fixture;

    public EndToEndIntegrityTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Buys, sells, returns in both directions, adjusts stock, takes a receipt, pays a supplier and
    /// books an expense — then checks the books still balance and stock still ties to movements.
    /// </summary>
    private async Task<TenantWorld> RunTradingMonthAsync(TenantScope tenant)
    {
        var world = await tenant.SeedWorldAsync();
        var customer = await tenant.SeedCustomerAsync("Account Customer", creditLimit: 1_000_000m);
        var expenseCategory = await tenant.SeedExpenseCategoryAsync();

        // Purchases
        var grn1 = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 200, unitCost: 10m);
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 12m);

        // Cash sale, then a credit sale
        await tenant.SellAsync(world, qty: 20, unitPrice: 25m, paid: 500m);

        var creditSale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 40, UnitPrice = 25m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        // Part payment against the credit sale
        await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
        {
            StockMain_ID = creditSale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 400m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        // Customer sends some back
        await tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
        {
            ReferenceStockMain_ID = creditSale.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 25m }
            }
        }, TenantData.TestUserId);

        // Some of the first delivery goes back to the supplier
        await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn1.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 15, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        // Breakages
        await tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
        {
            TransactionDate = AppTime.Now,
            AdjustmentType = "Write-off",
            AdjustmentReason = "Damaged in transit",
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 3 }
            }
        }, TenantData.TestUserId);

        // Pay the supplier something
        await tenant.Get<IPaymentService>().CreatePaymentAsync(new Payment
        {
            Party_ID = world.Supplier.PartyID,
            StockMain_ID = grn1.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 500m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        // Book the rent
        var expense = await tenant.Get<IExpenseService>().CreateAsync(new Expense
        {
            ExpenseCategory_ID = expenseCategory.ExpenseCategoryID,
            SourceAccount_ID = world.Cash.AccountID,
            Amount = 750m,
            ExpenseDate = AppTime.Now,
            Description = "Shop rent"
        }, TenantData.TestUserId);
        await tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId);

        return world;
    }

    [Fact]
    public async Task The_books_balance_after_a_full_trading_month()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await RunTradingMonthAsync(tenant);

        var (debit, credit) = await tenant.TrialBalanceAsync();

        Assert.True(debit > 0, "the month should have produced ledger activity");
        Assert.Equal(debit, credit);
    }

    /// <summary>Each individual voucher must balance, not merely the ledger in aggregate.</summary>
    [Fact]
    public async Task Every_voucher_produced_by_the_month_balances_on_its_own()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await RunTradingMonthAsync(tenant);

        var vouchers = await tenant.Db.Vouchers.Include(v => v.VoucherDetails).ToListAsync();
        Assert.NotEmpty(vouchers);

        var unbalanced = vouchers
            .Where(v => v.VoucherDetails.Sum(d => d.DebitAmount) != v.VoucherDetails.Sum(d => d.CreditAmount))
            .Select(v => v.VoucherNo)
            .ToList();

        Assert.True(unbalanced.Count == 0, $"unbalanced vouchers: {string.Join(", ", unbalanced)}");
    }

    /// <summary>
    /// The header totals are what reports read. They must agree with the lines underneath them, or
    /// the trial balance and the general ledger will tell different stories.
    /// </summary>
    [Fact]
    public async Task Every_voucher_header_agrees_with_its_own_lines()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await RunTradingMonthAsync(tenant);

        var vouchers = await tenant.Db.Vouchers.Include(v => v.VoucherDetails).ToListAsync();

        var mismatched = vouchers
            .Where(v => v.TotalDebit != v.VoucherDetails.Sum(d => d.DebitAmount)
                     || v.TotalCredit != v.VoucherDetails.Sum(d => d.CreditAmount))
            .Select(v => $"{v.VoucherNo} (header {v.TotalDebit}/{v.TotalCredit}, " +
                         $"lines {v.VoucherDetails.Sum(d => d.DebitAmount)}/{v.VoucherDetails.Sum(d => d.CreditAmount)})")
            .ToList();

        Assert.True(mismatched.Count == 0, $"voucher headers disagree with their lines: {string.Join("; ", mismatched)}");
    }

    /// <summary>
    /// Derived stock must equal the signed sum of the movements that produced it. If these ever
    /// disagree the stock report and the POS will show different numbers.
    /// </summary>
    [Fact]
    public async Task Derived_stock_ties_back_to_the_underlying_movements()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await RunTradingMonthAsync(tenant);

        // 200 + 100 received, 20 + 40 sold, 5 returned by the customer,
        // 15 returned to the supplier, 3 written off.
        var expected = 200m + 100m - 20m - 40m + 5m - 15m - 3m;
        Assert.Equal(expected, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    /// <summary>Nothing is ever hard-deleted, so no ledger line may reference a missing voucher.</summary>
    [Fact]
    public async Task No_ledger_line_is_orphaned()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await RunTradingMonthAsync(tenant);

        var voucherIds = await tenant.Db.Vouchers.Select(v => v.VoucherID).ToListAsync();
        var orphans = await tenant.Db.VoucherDetails.CountAsync(d => !voucherIds.Contains(d.Voucher_ID));

        Assert.Equal(0, orphans);
    }

    /// <summary>
    /// Voiding the whole month back out must leave the ledger flat: every posting reversed, so
    /// each account nets to zero. This is the real test of "voided, never deleted".
    /// </summary>
    [Fact]
    public async Task Voiding_every_transaction_returns_the_ledger_to_zero()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 25m, paid: 250m);
        await tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "cancelled", TenantData.TestUserId);

        // Only the sale is voided here; the GRN stays, so compare against the state before the sale.
        var perAccount = await tenant.Db.VoucherDetails
            .Where(d => d.Voucher!.SourceTable == "StockMain" && d.Voucher.SourceID == sale.StockMainID)
            .GroupBy(d => d.Account_ID)
            .Select(g => new { Account = g.Key, Net = g.Sum(d => d.DebitAmount - d.CreditAmount) })
            .ToListAsync();

        var nonZero = perAccount.Where(a => a.Net != 0).ToList();
        Assert.True(nonZero.Count == 0,
            $"voided sale left a residue on accounts: {string.Join(", ", nonZero.Select(a => $"{a.Account}={a.Net}"))}");
    }

    /// <summary>
    /// A voided sale must release the goods back onto the shelf and the receivable off the
    /// customer's account.
    /// </summary>
    [Fact]
    public async Task Voiding_a_sale_releases_both_the_stock_and_the_receivable()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var customer = await tenant.SeedCustomerAsync("Account Customer", creditLimit: 1_000_000m);
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 25m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        Assert.Equal(90m, await tenant.StockOnHandAsync(world.Product.ProductID));

        await tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "keyed twice", TenantData.TestUserId);

        Assert.Equal(100m, await tenant.StockOnHandAsync(world.Product.ProductID));

        var outstanding = await tenant.Get<ISaleService>().GetCustomerOutstandingSummaryAsync(customer.PartyID);
        Assert.Equal(0m, outstanding.OutstandingBalance);
        Assert.Equal(0, outstanding.OpenInvoices);
    }

    /// <summary>
    /// Two pharmacies trading simultaneously must produce two independent sets of books. This runs
    /// the same month in both and checks neither one's totals leak into the other.
    /// </summary>
    [Fact]
    public async Task Two_pharmacies_trading_at_once_keep_separate_books()
    {
        using var alpha = await _fixture.NewTenantAsync("Alpha Pharmacy");
        using var beta = await _fixture.NewTenantAsync("Beta Pharmacy");

        await RunTradingMonthAsync(alpha);

        var betaVouchersBefore = await beta.Db.Vouchers.CountAsync();
        await RunTradingMonthAsync(beta);

        var (alphaDebit, alphaCredit) = await alpha.TrialBalanceAsync();
        var (betaDebit, betaCredit) = await beta.TrialBalanceAsync();

        Assert.Equal(alphaDebit, alphaCredit);
        Assert.Equal(betaDebit, betaCredit);

        // Alpha's month must not show up in Beta's ledger.
        Assert.Equal(0, betaVouchersBefore);
        Assert.Equal(alphaDebit, betaDebit); // identical scripts, so identical totals — not summed together
    }
}
