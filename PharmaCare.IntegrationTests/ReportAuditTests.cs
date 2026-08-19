using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Report-layer audit probes (2026-08-19). Every test builds its scenario through the REAL
/// transaction services, computes the expected numbers by hand from those inputs, and asserts the
/// report agrees. A failing test is a confirmed report defect, not a broken test.
/// </summary>
[Collection(Collections.Database)]
public class ReportAuditTests
{
    private readonly DatabaseFixture _fixture;

    public ReportAuditTests(DatabaseFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------- helpers

    private static DateRangeFilter Range(DateTime from, DateTime to) => new() { FromDate = from, ToDate = to };

    /// <summary>A sale with an explicit transaction date (services allow any date in the open FY).</summary>
    private static Task<StockMain> SaleOn(
        TenantScope tenant, TenantWorld world, DateTime date, decimal qty, decimal price, decimal paid,
        decimal discountPercent = 0)
        => tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = date,
            PaidAmount = paid,
            DiscountPercent = discountPercent,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = qty, UnitPrice = price }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

    /// <summary>A GRN with an explicit transaction date.</summary>
    private static Task<StockMain> GrnOn(
        TenantScope tenant, TenantWorld world, DateTime date, decimal qty, decimal cost, decimal paid = 0)
        => tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = date,
            PaidAmount = paid,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = qty, UnitPrice = cost, CostPrice = cost }
            }
        }, TenantData.TestUserId, paid > 0 ? world.Cash.AccountID : null);

    private static Task<StockMain> SaleReturnOf(TenantScope tenant, TenantWorld world, StockMain sale, decimal qty)
        => tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
        {
            ReferenceStockMain_ID = sale.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = qty, UnitPrice = 20m }
            }
        }, TenantData.TestUserId);

    private static Task<StockMain> PurchaseReturnOf(
        TenantScope tenant, TenantWorld world, StockMain grn, decimal qty, decimal cost)
        => tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = qty, CostPrice = cost }
            }
        }, TenantData.TestUserId);

    // ================================================================ A. AR / AP AGING

    /// <summary>
    /// RPT-1: an aging report run "as of today" must include a credit sale made earlier today.
    /// GetReceivablesAgingAsync filters TransactionDate &lt;= asOfDate; the natural argument
    /// (AppTime.Today, midnight) silently excludes everything posted today.
    /// </summary>
    [Fact]
    public async Task Receivables_aging_run_as_of_today_includes_a_credit_sale_made_today()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 0);

        var aging = await tenant.Get<IFinancialReportService>().GetReceivablesAgingAsync(AppTime.Today);

        Assert.Equal(200m, aging.GrandTotal);
    }

    /// <summary>
    /// RPT-2: receivables buckets follow calendar-day age: 30 days old is Current, 31 days is
    /// 31-60, 91 days is 90+. Uses backdated credit sales with a time-of-day component, exactly
    /// as the POS writes them.
    /// </summary>
    [Fact]
    public async Task Receivables_aging_buckets_by_calendar_day_age()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        // Stock arrives long before the sales, fully paid so payables stay out of the picture.
        await GrnOn(tenant, world, AppTime.Today.AddDays(-100), qty: 100, cost: 10m, paid: 1000m);

        await SaleOn(tenant, world, AppTime.Today.AddDays(-30).AddHours(20), qty: 15, price: 20m, paid: 0); // 300, Current
        await SaleOn(tenant, world, AppTime.Today.AddDays(-31).AddHours(20), qty: 10, price: 20m, paid: 0); // 200, 31-60
        await SaleOn(tenant, world, AppTime.Today.AddDays(-91).AddHours(20), qty: 25, price: 20m, paid: 0); // 500, 90+

        var aging = await tenant.Get<IFinancialReportService>().GetReceivablesAgingAsync(AppTime.Today);
        var row = Assert.Single(aging.Rows);

        Assert.Equal(300m, row.Current);
        Assert.Equal(200m, row.Days31_60);
        Assert.Equal(0m, row.Days61_90);
        Assert.Equal(500m, row.Days90Plus);
        Assert.Equal(1000m, aging.GrandTotal);
    }

    /// <summary>
    /// RPT-3: payables aging must age an invoice exactly like receivables aging does. A GRN dated
    /// 31 calendar days ago (at 20:00) is 31 days old and belongs in the 31-60 bucket — the same
    /// bucket RPT-2 proves the AR side uses. GetPayablesAgingAsync instead truncates
    /// (asOf - date).TotalDays, so the time-of-day shaves the invoice back into Current.
    /// </summary>
    [Fact]
    public async Task Payables_aging_buckets_a_31_day_old_invoice_in_the_same_bucket_as_receivables()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await GrnOn(tenant, world, AppTime.Today.AddDays(-31).AddHours(20), qty: 100, cost: 10m, paid: 0);

        var aging = await tenant.Get<IFinancialReportService>().GetPayablesAgingAsync(AppTime.Today);
        var row = Assert.Single(aging.Rows);

        Assert.Equal(1000m, row.Total);
        Assert.Equal(1000m, row.Days31_60);
    }

    /// <summary>RPT-4: voided transactions must vanish from both aging reports.</summary>
    [Fact]
    public async Task Aging_reports_exclude_voided_transactions()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await GrnOn(tenant, world, AppTime.Now, qty: 100, cost: 10m, paid: 0);
        var sale = await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 0);

        Assert.True(await tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "keyed wrong", TenantData.TestUserId));
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        var asOf = AppTime.Today.AddDays(1); // beyond the midnight boundary so exclusion can only mean the void worked
        var ar = await tenant.Get<IFinancialReportService>().GetReceivablesAgingAsync(asOf);
        var ap = await tenant.Get<IFinancialReportService>().GetPayablesAgingAsync(asOf);

        Assert.Equal(0m, ar.GrandTotal);
        Assert.Equal(0m, ap.GrandTotal);
    }

    // ================================================================ B. AS-OF "TODAY" FAMILY

    /// <summary>
    /// RPT-5: the customer balance summary run as of today must include a credit sale made today
    /// (same midnight boundary as RPT-1, different report).
    /// </summary>
    [Fact]
    public async Task Customer_balance_summary_as_of_today_includes_todays_sale()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 0);

        var summary = await tenant.Get<ISalesReportService>().GetCustomerBalanceSummaryAsync(AppTime.Today);

        Assert.Equal(200m, summary.TotalBalance);
    }

    /// <summary>
    /// RPT-6: a trial balance "as of today" must include today's postings. Vouchers carry the
    /// transaction time, so the midnight asOfDate excludes the entire current day.
    /// </summary>
    [Fact]
    public async Task Trial_balance_as_of_today_includes_todays_postings()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var tb = await tenant.Get<IFinancialReportService>().GetTrialBalanceAsync(AppTime.Today);

        Assert.True(tb.TotalDebit > 0,
            "trial balance as of today returned no postings — today's vouchers are excluded by the midnight asOfDate");
    }

    // ================================================================ C. PARTY LEDGER

    /// <summary>
    /// RPT-7: customer ledger over sale + receipt + return. Truth: 200 sold − 50 received − 80
    /// returned = 70 still owed.
    /// </summary>
    [Fact]
    public async Task Customer_ledger_reconciles_sale_receipt_and_return()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 0);

        await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
        {
            StockMain_ID = sale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 50m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        await SaleReturnOf(tenant, world, sale, qty: 4); // credits 80 at the enforced sale price

        var ledger = await tenant.Get<IFinancialReportService>().GetPartyLedgerAsync(
            new DateRangeFilter { FromDate = AppTime.Today.AddDays(-1), ToDate = AppTime.Today, PartyId = world.Customer.PartyID },
            "Customer");

        Assert.Equal(0m, ledger.OpeningBalance);
        Assert.Equal(200m, ledger.TotalDebit);
        Assert.Equal(130m, ledger.TotalCredit);
        Assert.Equal(70m, ledger.ClosingBalance);
    }

    /// <summary>
    /// RPT-8: cash refunds are real ledger events. Fully paid sale (200), return (100) creates a
    /// credit note, note refunded in cash (100). The customer and the pharmacy are square: the
    /// ledger must close at 0. GetPartyLedgerAsync only reads RECEIPT payments, so the REFUND
    /// never appears and the customer looks permanently in credit.
    /// </summary>
    [Fact]
    public async Task Customer_ledger_includes_cash_refunds_of_credit_notes()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 200m);
        await SaleReturnOf(tenant, world, sale, qty: 5); // 100 back -> credit note (sale was fully paid)

        await tenant.Get<ICustomerPaymentService>().CreateRefundAsync(new Payment
        {
            Party_ID = world.Customer.PartyID,
            Account_ID = world.Cash.AccountID,
            Amount = 100m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        var ledger = await tenant.Get<IFinancialReportService>().GetPartyLedgerAsync(
            new DateRangeFilter { FromDate = AppTime.Today.AddDays(-1), ToDate = AppTime.Today, PartyId = world.Customer.PartyID },
            "Customer");

        Assert.Equal(0m, ledger.ClosingBalance);
    }

    /// <summary>
    /// RPT-9: supplier ledger over purchase + part-payment + purchase return.
    /// Truth: 1000 payable − 300 paid − 200 returned = 500 still owed to the supplier.
    /// </summary>
    [Fact]
    public async Task Supplier_ledger_reconciles_purchase_payment_and_return()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await GrnOn(tenant, world, AppTime.Now, qty: 100, cost: 10m, paid: 0);

        await tenant.Get<IPaymentService>().CreatePaymentAsync(new Payment
        {
            Party_ID = world.Supplier.PartyID,
            StockMain_ID = grn.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 300m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        await PurchaseReturnOf(tenant, world, grn, qty: 20, cost: 10m);

        var ledger = await tenant.Get<IFinancialReportService>().GetPartyLedgerAsync(
            new DateRangeFilter { FromDate = AppTime.Today.AddDays(-1), ToDate = AppTime.Today, PartyId = world.Supplier.PartyID },
            "Supplier");

        Assert.Equal(0m, ledger.OpeningBalance);
        Assert.Equal(1000m, ledger.TotalCredit);
        Assert.Equal(500m, ledger.TotalDebit);
        Assert.Equal(500m, ledger.ClosingBalance);
    }

    /// <summary>
    /// RPT-10: activity before the report range must roll into the opening balance, on top of the
    /// party's stated opening balance.
    /// </summary>
    [Fact]
    public async Task Party_ledger_rolls_prior_activity_into_opening_balance()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var customer = await tenant.SeedCustomerAsync("Opening Balance Customer", openingBalance: 50m);
        world = world with { Customer = customer };
        await GrnOn(tenant, world, AppTime.Today.AddDays(-20), qty: 100, cost: 10m, paid: 1000m);
        await SaleOn(tenant, world, AppTime.Today.AddDays(-10), qty: 10, price: 20m, paid: 0);

        var ledger = await tenant.Get<IFinancialReportService>().GetPartyLedgerAsync(
            new DateRangeFilter { FromDate = AppTime.Today.AddDays(-5), ToDate = AppTime.Today, PartyId = customer.PartyID },
            "Customer");

        Assert.Equal(250m, ledger.OpeningBalance); // 50 stated + 200 backdated credit sale
        Assert.Empty(ledger.Rows);
        Assert.Equal(250m, ledger.ClosingBalance);
    }

    // ================================================================ D. GENERAL LEDGER

    /// <summary>
    /// RPT-11: the general ledger for the cash account must close at exactly the sum of every
    /// posted voucher line on that account — and here that sum is known by hand: +200 cash sale,
    /// −50 approved expense.
    /// </summary>
    [Fact]
    public async Task General_ledger_closing_balance_matches_raw_posted_lines()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m); // unpaid: no cash
        await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var category = await tenant.SeedExpenseCategoryAsync();
        var expense = await tenant.Get<IExpenseService>().CreateAsync(new Expense
        {
            ExpenseCategory_ID = category.ExpenseCategoryID,
            SourceAccount_ID = world.Cash.AccountID,
            Amount = 50m,
            ExpenseDate = AppTime.Now,
            Description = "Electricity"
        }, TenantData.TestUserId);
        Assert.True(await tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId));

        var ledger = await tenant.Get<IFinancialReportService>().GetGeneralLedgerAsync(
            new DateRangeFilter { FromDate = AppTime.Today.AddYears(-1), ToDate = AppTime.Today, AccountId = world.Cash.AccountID });

        var rawBalance = await tenant.Db.VoucherDetails
            .Where(d => d.Account_ID == world.Cash.AccountID && d.Voucher!.Status == "Posted")
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        Assert.Equal(150m, rawBalance);
        Assert.Equal(rawBalance, ledger.ClosingBalance);
        Assert.Equal(ledger.OpeningBalance + ledger.TotalDebit - ledger.TotalCredit, ledger.ClosingBalance);
    }

    /// <summary>RPT-12: no account selected is a no-op, never a crash.</summary>
    [Fact]
    public async Task General_ledger_with_no_account_selected_returns_empty()
    {
        using var tenant = await _fixture.NewTenantAsync();

        var ledger = await tenant.Get<IFinancialReportService>().GetGeneralLedgerAsync(
            Range(AppTime.Today.AddDays(-30), AppTime.Today));

        Assert.Empty(ledger.Rows);
        Assert.Equal(0m, ledger.ClosingBalance);
    }

    /// <summary>
    /// RPT-13: a voided sale's revenue must net to zero in the general ledger (original + reversal
    /// are both posted and cancel).
    /// </summary>
    [Fact]
    public async Task Voided_sale_nets_to_zero_in_general_ledger_revenue()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);
        Assert.True(await tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "keyed wrong", TenantData.TestUserId));

        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");
        var ledger = await tenant.Get<IFinancialReportService>().GetGeneralLedgerAsync(
            new DateRangeFilter { FromDate = AppTime.Today.AddYears(-1), ToDate = AppTime.Today.AddDays(1), AccountId = revenue.AccountID });

        Assert.Equal(0m, ledger.ClosingBalance);
    }

    // ================================================================ E. SALES REPORTS

    /// <summary>
    /// RPT-14: after a sale (200) and a return (80), every sales-side report must agree on net
    /// revenue 120 and the P&amp;L on COGS 60 (100 out at cost 10, 40 back).
    /// </summary>
    [Fact]
    public async Task Sales_reports_agree_on_net_revenue_after_a_return()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 100m);
        await SaleReturnOf(tenant, world, sale, qty: 4);

        var range = Range(AppTime.Today, AppTime.Today);
        var pnl = await tenant.Get<IFinancialReportService>().GetProfitLossAsync(range);
        var salesReport = await tenant.Get<ISalesReportService>().GetSalesReportAsync(range);
        var byProduct = await tenant.Get<ISalesReportService>().GetSalesByProductAsync(range);
        var byCustomer = await tenant.Get<ISalesReportService>().GetSalesByCustomerAsync(range);
        var daily = await tenant.Get<ISalesReportService>().GetDailySalesSummaryAsync(AppTime.Today);

        Assert.Equal(120m, pnl.NetRevenue);
        Assert.Equal(60m, pnl.COGS);
        Assert.Equal(60m, pnl.GrossProfit);
        Assert.Equal(120m, salesReport.GrandTotal);
        Assert.Equal(120m, byProduct.TotalRevenue);
        Assert.Equal(120m, byCustomer.TotalSales);
        Assert.Equal(120m, daily.NetSales);
    }

    /// <summary>
    /// RPT-15: a header discount reduces what was actually charged, so the product-level report
    /// must agree with the invoice-level reports. Sale of 10 x 20 with a 10% header discount is
    /// 180 of revenue everywhere. GetSalesByProductAsync sums StockDetail.LineTotal, which is
    /// gross of the header discount, so it reports 200 and overstates revenue and profit.
    /// </summary>
    [Fact]
    public async Task Sales_by_product_reflects_header_discounts_in_revenue()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 0, discountPercent: 10m);

        var range = Range(AppTime.Today, AppTime.Today);
        var pnl = await tenant.Get<IFinancialReportService>().GetProfitLossAsync(range);
        var salesReport = await tenant.Get<ISalesReportService>().GetSalesReportAsync(range);
        var byProduct = await tenant.Get<ISalesReportService>().GetSalesByProductAsync(range);

        Assert.Equal(180m, pnl.NetRevenue);
        Assert.Equal(180m, salesReport.GrandTotal);
        Assert.Equal(180m, byProduct.TotalRevenue);
    }

    /// <summary>
    /// RPT-16: the sales report's balance column must state what the customer still owes. Credit
    /// sale 200, return 80: the service already writes the truth into the sale row
    /// (BalanceAmount 120). But the report ALSO subtracts the return's own BalanceAmount (80),
    /// double-counting the return and reporting 40.
    /// </summary>
    [Fact]
    public async Task Sales_report_balance_column_reflects_outstanding_after_return()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 0);
        await SaleReturnOf(tenant, world, sale, qty: 4);

        // Ground truth straight from the transaction row the return service maintains.
        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == sale.StockMainID);
        Assert.Equal(120m, reloaded.BalanceAmount);

        var report = await tenant.Get<ISalesReportService>().GetSalesReportAsync(Range(AppTime.Today, AppTime.Today));
        Assert.Equal(120m, report.GrandBalance);
    }

    /// <summary>RPT-17: a voided sale must disappear from every sales report.</summary>
    [Fact]
    public async Task Voided_sale_disappears_from_sales_reports()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);
        Assert.True(await tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "keyed wrong", TenantData.TestUserId));

        var range = Range(AppTime.Today, AppTime.Today);
        var salesReport = await tenant.Get<ISalesReportService>().GetSalesReportAsync(range);
        var byProduct = await tenant.Get<ISalesReportService>().GetSalesByProductAsync(range);
        var daily = await tenant.Get<ISalesReportService>().GetDailySalesSummaryAsync(AppTime.Today);

        Assert.Empty(salesReport.Rows);
        Assert.Empty(byProduct.Rows);
        Assert.Equal(0m, daily.TotalSales);
        Assert.Equal(0m, daily.TotalCOGS);
    }

    /// <summary>
    /// RPT-18: both range boundaries are inclusive — a sale dated exactly on FromDate appears,
    /// one dated the day after ToDate does not.
    /// </summary>
    [Fact]
    public async Task Sales_report_range_boundaries_are_inclusive_of_both_ends()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await GrnOn(tenant, world, AppTime.Today.AddDays(-20), qty: 100, cost: 10m, paid: 1000m);

        var onFrom = await SaleOn(tenant, world, AppTime.Today.AddDays(-5), qty: 1, price: 20m, paid: 0); // midnight, on the boundary
        await SaleOn(tenant, world, AppTime.Today.AddDays(-4), qty: 2, price: 20m, paid: 0);              // day after ToDate

        var report = await tenant.Get<ISalesReportService>().GetSalesReportAsync(
            Range(AppTime.Today.AddDays(-5), AppTime.Today.AddDays(-5)));

        var row = Assert.Single(report.Rows);
        Assert.Equal(onFrom.StockMainID, row.StockMainId);
        Assert.Equal(20m, report.GrandTotal);
    }

    /// <summary>
    /// RPT-19: the customer balance summary must reflect sale returns. Credit sale 200, return 80:
    /// the customer owes 120 — which is exactly what receivables aging reports from the same data.
    /// The summary computes sales minus receipts only, so the return never reduces the balance and
    /// it reports 200.
    /// </summary>
    [Fact]
    public async Task Customer_balance_summary_reflects_sale_returns()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 0);
        await SaleReturnOf(tenant, world, sale, qty: 4);

        var asOf = AppTime.Today.AddDays(1); // past the midnight boundary: only return-handling is under test
        var aging = await tenant.Get<IFinancialReportService>().GetReceivablesAgingAsync(asOf);
        Assert.Equal(120m, aging.GrandTotal); // ground truth, agreed by the AR aging over the same rows

        var summary = await tenant.Get<ISalesReportService>().GetCustomerBalanceSummaryAsync(asOf);
        var row = Assert.Single(summary.Rows);
        Assert.Equal(120m, row.BalanceDue);
    }

    // ================================================================ F. PURCHASE REPORTS

    /// <summary>RPT-20: purchase report and purchase-by-supplier agree on net purchases after a return.</summary>
    [Fact]
    public async Task Purchase_reports_net_returns_consistently()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await GrnOn(tenant, world, AppTime.Now, qty: 100, cost: 10m, paid: 0);
        await PurchaseReturnOf(tenant, world, grn, qty: 20, cost: 10m);

        var range = Range(AppTime.Today, AppTime.Today);
        var report = await tenant.Get<IPurchaseReportService>().GetPurchaseReportAsync(range);
        var bySupplier = await tenant.Get<IPurchaseReportService>().GetPurchaseBySupplierAsync(range);

        Assert.Equal(800m, report.GrandTotal);
        Assert.Equal(800m, bySupplier.TotalPurchases);
        Assert.Equal(1, Assert.Single(bySupplier.Rows).PurchaseCount);
    }

    /// <summary>
    /// RPT-21: the purchase report's balance column must state what is still owed to the supplier.
    /// Unpaid GRN 1000, return 200: the GRN row already carries the truth (BalanceAmount 800), but
    /// the report also subtracts the return's own BalanceAmount (200), reporting 600.
    /// </summary>
    [Fact]
    public async Task Purchase_report_balance_column_reflects_payable_after_return()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await GrnOn(tenant, world, AppTime.Now, qty: 100, cost: 10m, paid: 0);
        await PurchaseReturnOf(tenant, world, grn, qty: 20, cost: 10m);

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID);
        Assert.Equal(800m, reloaded.BalanceAmount);

        var report = await tenant.Get<IPurchaseReportService>().GetPurchaseReportAsync(Range(AppTime.Today, AppTime.Today));
        Assert.Equal(800m, report.GrandBalance);
    }

    /// <summary>RPT-22: a voided GRN must disappear from both purchase reports.</summary>
    [Fact]
    public async Task Voided_grn_disappears_from_purchase_reports()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await GrnOn(tenant, world, AppTime.Now, qty: 100, cost: 10m, paid: 0);
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        var range = Range(AppTime.Today, AppTime.Today);
        var report = await tenant.Get<IPurchaseReportService>().GetPurchaseReportAsync(range);
        var bySupplier = await tenant.Get<IPurchaseReportService>().GetPurchaseBySupplierAsync(range);

        Assert.Empty(report.Rows);
        Assert.Empty(bySupplier.Rows);
    }

    /// <summary>RPT-23: LastPurchaseDate tracks the latest GRN, not the latest return.</summary>
    [Fact]
    public async Task Purchase_by_supplier_last_purchase_date_ignores_returns()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await GrnOn(tenant, world, AppTime.Today.AddDays(-5), qty: 100, cost: 10m, paid: 0);
        await PurchaseReturnOf(tenant, world, grn, qty: 20, cost: 10m); // dated today

        var bySupplier = await tenant.Get<IPurchaseReportService>().GetPurchaseBySupplierAsync(
            Range(AppTime.Today.AddDays(-10), AppTime.Today));

        var row = Assert.Single(bySupplier.Rows);
        Assert.NotNull(row.LastPurchaseDate);
        Assert.Equal(AppTime.Today.AddDays(-5).Date, row.LastPurchaseDate!.Value.Date);
    }

    // ================================================================ G. INVENTORY REPORTS

    /// <summary>
    /// RPT-24: every movement column of the current-stock report, and the derived total, against
    /// a hand-built history: opening 20, +100 GRN, −30 sold, +5 sale return, −10 purchase return,
    /// −3 write-off = 82 — which must equal the POS's own stock-on-hand.
    /// </summary>
    [Fact]
    public async Task Current_stock_report_reconciles_every_movement_column()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory, name: "Reconcile-Me", openingQuantity: 20);
        var customer = await tenant.SeedCustomerAsync();
        var supplier = await tenant.SeedSupplierAsync();
        var cash = await tenant.CashAccountAsync();
        var world = new TenantWorld(category, subCategory, product, customer, supplier, cash);

        var grn = await GrnOn(tenant, world, AppTime.Now, qty: 100, cost: 10m, paid: 0);
        var sale = await SaleOn(tenant, world, AppTime.Now, qty: 30, price: 20m, paid: 600m);
        await SaleReturnOf(tenant, world, sale, qty: 5);
        await PurchaseReturnOf(tenant, world, grn, qty: 10, cost: 10m);
        await tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
        {
            TransactionDate = AppTime.Now,
            AdjustmentType = "Write-off",
            AdjustmentReason = "Damaged",
            StockDetails = new List<StockDetail> { new() { Product_ID = product.ProductID, Quantity = 3 } }
        }, TenantData.TestUserId);

        var report = await tenant.Get<IInventoryReportService>().GetCurrentStockReportAsync(new DateRangeFilter());
        var row = report.Rows.Single(r => r.ProductId == product.ProductID);

        Assert.Equal(20m, row.OpeningQty);
        Assert.Equal(100m, row.PurchasedQty);
        Assert.Equal(30m, row.SoldQty);
        Assert.Equal(5m, row.ReturnedInQty);
        Assert.Equal(10m, row.ReturnedOutQty);
        Assert.Equal(-3m, row.AdjustedQty);
        Assert.Equal(82m, row.CurrentStock);
        Assert.Equal(await tenant.StockOnHandAsync(product.ProductID), row.CurrentStock);
    }

    /// <summary>RPT-25: stock is valued at the LATEST approved GRN cost, not the first or the opening price.</summary>
    [Fact]
    public async Task Current_stock_is_valued_at_latest_grn_cost()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await GrnOn(tenant, world, AppTime.Today.AddDays(-2), qty: 50, cost: 10m, paid: 0);
        await GrnOn(tenant, world, AppTime.Now, qty: 50, cost: 12m, paid: 0);

        var report = await tenant.Get<IInventoryReportService>().GetCurrentStockReportAsync(new DateRangeFilter());
        var row = report.Rows.Single(r => r.ProductId == world.Product.ProductID);

        Assert.Equal(100m, row.CurrentStock);
        Assert.Equal(12m, row.CostPrice);
        Assert.Equal(1200m, row.StockValue);
    }

    /// <summary>
    /// RPT-26: product movement over a window: prior GRN rolls into the opening balance, the
    /// in-window sale is one out-row, and closing = opening + in − out = the POS's stock-on-hand.
    /// </summary>
    [Fact]
    public async Task Product_movement_reconciles_opening_plus_in_minus_out()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await GrnOn(tenant, world, AppTime.Today.AddDays(-10), qty: 50, cost: 10m, paid: 0);
        await SaleOn(tenant, world, AppTime.Now, qty: 10, price: 20m, paid: 200m);

        var report = await tenant.Get<IInventoryReportService>().GetProductMovementReportAsync(
            new DateRangeFilter { FromDate = AppTime.Today.AddDays(-5), ToDate = AppTime.Today, ProductId = world.Product.ProductID });

        Assert.Equal(50m, report.OpeningBalance);
        var row = Assert.Single(report.Rows);
        Assert.Equal(10m, row.QtyOut);
        Assert.Equal(40m, report.ClosingBalance);
        Assert.Equal(await tenant.StockOnHandAsync(world.Product.ProductID), report.ClosingBalance);
    }

    /// <summary>RPT-27: low-stock flags at-or-below reorder level, and only that.</summary>
    [Fact]
    public async Task Low_stock_report_flags_at_or_below_reorder_level()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var atLevel = await tenant.SeedProductAsync(world.Category, world.SubCategory, name: "At-Reorder", reorderLevel: 5);
        var aboveLevel = await tenant.SeedProductAsync(world.Category, world.SubCategory, name: "Above-Reorder", reorderLevel: 5);

        await tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = atLevel.ProductID, Quantity = 5, UnitPrice = 10m, CostPrice = 10m },
                new() { Product_ID = aboveLevel.ProductID, Quantity = 6, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        var report = await tenant.Get<IInventoryReportService>().GetLowStockReportAsync(new DateRangeFilter());

        var flagged = report.Rows.SingleOrDefault(r => r.ProductId == atLevel.ProductID);
        Assert.NotNull(flagged);
        Assert.Equal(0m, flagged!.Shortfall);
        Assert.Equal(5m, flagged.SuggestedReorderQty); // 2 x reorder − stock
        Assert.DoesNotContain(report.Rows, r => r.ProductId == aboveLevel.ProductID);
    }

    /// <summary>RPT-28: dead stock lists stocked-but-never-sold items and spares anything sold recently.</summary>
    [Fact]
    public async Task Dead_stock_report_separates_never_sold_from_recently_sold()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var neverSold = await tenant.SeedProductAsync(world.Category, world.SubCategory, name: "Shelf-Warmer");
        var brisk = await tenant.SeedProductAsync(world.Category, world.SubCategory, name: "Fast-Mover");

        await tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = neverSold.ProductID, Quantity = 10, UnitPrice = 10m, CostPrice = 10m },
                new() { Product_ID = brisk.ProductID, Quantity = 10, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 40m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = brisk.ProductID, Quantity = 2, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        var report = await tenant.Get<IInventoryReportService>().GetDeadStockReportAsync(new DateRangeFilter());

        var deadRow = report.Rows.SingleOrDefault(r => r.ProductId == neverSold.ProductID);
        Assert.NotNull(deadRow);
        Assert.Null(deadRow!.LastSaleDate);
        Assert.Equal(100m, deadRow.StockValue);
        Assert.DoesNotContain(report.Rows, r => r.ProductId == brisk.ProductID);
    }

    // ================================================================ H. EXPENSES + EMPTY TENANT

    /// <summary>RPT-29: the expense report contains approved spend only — never drafts, never voids.</summary>
    [Fact]
    public async Task Expense_report_includes_only_approved_expenses()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var category = await tenant.SeedExpenseCategoryAsync();
        var expenses = tenant.Get<IExpenseService>();

        Expense NewExpense(decimal amount, string description) => new()
        {
            ExpenseCategory_ID = category.ExpenseCategoryID,
            SourceAccount_ID = world.Cash.AccountID,
            Amount = amount,
            ExpenseDate = AppTime.Now,
            Description = description
        };

        await expenses.CreateAsync(NewExpense(100m, "still a draft"), TenantData.TestUserId);

        var approved = await expenses.CreateAsync(NewExpense(200m, "approved"), TenantData.TestUserId);
        Assert.True(await expenses.ApproveAsync(approved.ExpenseID, TenantData.TestUserId));

        var voided = await expenses.CreateAsync(NewExpense(300m, "approved then voided"), TenantData.TestUserId);
        Assert.True(await expenses.ApproveAsync(voided.ExpenseID, TenantData.TestUserId));
        Assert.True(await expenses.VoidAsync(voided.ExpenseID, "entered twice", TenantData.TestUserId));

        var range = Range(AppTime.Today, AppTime.Today);
        var report = await tenant.Get<IFinancialReportService>().GetExpenseReportAsync(range);
        var pnl = await tenant.Get<IFinancialReportService>().GetProfitLossAsync(range);

        var row = Assert.Single(report.Rows);
        Assert.Equal(200m, row.Amount);
        Assert.Equal(200m, report.GrandTotal);
        Assert.Equal(200m, pnl.TotalExpenses);
    }

    /// <summary>
    /// RPT-30: every report method must run clean on a brand-new tenant with zero data — no
    /// division by zero, no null blowups, no crash on empty groupings.
    /// </summary>
    [Fact]
    public async Task Every_report_survives_a_brand_new_tenant_with_no_data()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var range = Range(AppTime.Today.AddDays(-30), AppTime.Today);
        var financial = tenant.Get<IFinancialReportService>();
        var sales = tenant.Get<ISalesReportService>();
        var inventory = tenant.Get<IInventoryReportService>();
        var purchases = tenant.Get<IPurchaseReportService>();

        var pnl = await financial.GetProfitLossAsync(range);
        Assert.Equal(0m, pnl.NetProfit);
        Assert.Equal(0m, pnl.GrossProfitMargin);

        await financial.GetCashFlowReportAsync(range);
        Assert.Equal(0m, (await financial.GetReceivablesAgingAsync(AppTime.Today)).GrandTotal);
        Assert.Equal(0m, (await financial.GetPayablesAgingAsync(AppTime.Today)).GrandTotal);
        Assert.Empty((await financial.GetExpenseReportAsync(range)).Rows);
        Assert.True((await financial.GetTrialBalanceAsync(AppTime.Today)).IsBalanced);
        Assert.Empty((await financial.GetGeneralLedgerAsync(range)).Rows);
        Assert.Empty((await financial.GetPartyLedgerAsync(range, "Customer")).Rows);
        Assert.Empty((await financial.GetPartyLedgerAsync(range, "Supplier")).Rows);

        Assert.Equal(0m, (await sales.GetDailySalesSummaryAsync(AppTime.Today)).NetSales);
        Assert.Empty((await sales.GetSalesReportAsync(range)).Rows);
        Assert.Empty((await sales.GetSalesByProductAsync(range)).Rows);
        Assert.Empty((await sales.GetSalesByCustomerAsync(range)).Rows);
        Assert.Empty((await sales.GetCustomerBalanceSummaryAsync(AppTime.Today)).Rows);

        Assert.Empty((await inventory.GetCurrentStockReportAsync(new DateRangeFilter())).Rows);
        Assert.Empty((await inventory.GetLowStockReportAsync(new DateRangeFilter())).Rows);
        Assert.Empty((await inventory.GetProductMovementReportAsync(range)).Rows);
        Assert.Empty((await inventory.GetDeadStockReportAsync(new DateRangeFilter())).Rows);

        Assert.Empty((await purchases.GetPurchaseReportAsync(range)).Rows);
        Assert.Empty((await purchases.GetPurchaseBySupplierAsync(range)).Rows);
    }
}
