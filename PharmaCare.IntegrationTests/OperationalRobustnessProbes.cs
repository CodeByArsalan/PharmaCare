using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Everything a real day throws at the application that no earlier sweep has: date ranges typed in
/// the wrong order, dropdowns offering choices that are not actually valid, a purchase order edited
/// after it was approved, an opening balance changed once the party has already traded.
///
/// <para>
/// The reports have thirty tests, all of which feed them a sensible range. Nobody has asked what
/// happens when a user swaps the two date boxes, and every report screen posts both dates straight
/// from the form. The dropdowns have none at all, yet they are the list of things a cashier is
/// allowed to pick — a stale or over-wide dropdown is how an invalid document gets created in the
/// first place, and the services behind them then have to refuse what their own UI offered.
/// </para>
///
/// <para>Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.</para>
/// </summary>
[Collection(Collections.Database)]
public class OperationalRobustnessProbes
{
    private readonly DatabaseFixture _fixture;

    public OperationalRobustnessProbes(DatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------------------------------
    // Date ranges typed in the wrong order.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_inverted_date_range_is_reported_as_empty_and_not_as_a_crash()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.SellAsync(world, qty: 2, unitPrice: 30m, paid: 60m);

        // From LATER than To — one mis-click on a date picker.
        var inverted = new DateRangeFilter
        {
            FromDate = AppTime.Today,
            ToDate = AppTime.Today.AddDays(-30)
        };

        var failures = new List<string>();

        async Task Check(string name, Func<Task> run)
        {
            var error = await Record.ExceptionAsync(run);
            if (error != null) failures.Add($"{name} -> {error.GetType().Name}: {error.Message}");
        }

        await Check("Sales report", () => tenant.Get<ISalesReportService>().GetSalesReportAsync(inverted));
        await Check("Sales by product", () => tenant.Get<ISalesReportService>().GetSalesByProductAsync(inverted));
        await Check("Sales by customer", () => tenant.Get<ISalesReportService>().GetSalesByCustomerAsync(inverted));
        await Check("Purchase report", () => tenant.Get<IPurchaseReportService>().GetPurchaseReportAsync(inverted));
        await Check("Purchase by supplier", () => tenant.Get<IPurchaseReportService>().GetPurchaseBySupplierAsync(inverted));
        await Check("Current stock", () => tenant.Get<IInventoryReportService>().GetCurrentStockReportAsync(inverted));
        await Check("Product movement", () => tenant.Get<IInventoryReportService>().GetProductMovementReportAsync(inverted));
        await Check("Dead stock", () => tenant.Get<IInventoryReportService>().GetDeadStockReportAsync(inverted));
        await Check("Low stock", () => tenant.Get<IInventoryReportService>().GetLowStockReportAsync(inverted));
        await Check("Profit and loss", () => tenant.Get<IFinancialReportService>().GetProfitLossAsync(inverted));
        await Check("Cash flow", () => tenant.Get<IFinancialReportService>().GetCashFlowReportAsync(inverted));
        await Check("Expense report", () => tenant.Get<IFinancialReportService>().GetExpenseReportAsync(inverted));
        await Check("General ledger", () => tenant.Get<IFinancialReportService>().GetGeneralLedgerAsync(inverted));
        await Check("Party ledger", () => tenant.Get<IFinancialReportService>().GetPartyLedgerAsync(inverted, "Customer"));

        Assert.True(failures.Count == 0,
            "A report screen with its two date boxes the wrong way round should return nothing, not " +
            "throw:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public async Task An_inverted_range_reports_no_activity_rather_than_all_of_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.SellAsync(world, qty: 2, unitPrice: 30m, paid: 60m);

        var report = await tenant.Get<ISalesReportService>().GetSalesReportAsync(new DateRangeFilter
        {
            FromDate = AppTime.Today.AddDays(1),
            ToDate = AppTime.Today.AddDays(-1)
        });

        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task A_report_run_as_of_a_future_date_matches_one_run_today()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.SellAsync(world, qty: 3, unitPrice: 30m, paid: 0m);

        var financial = tenant.Get<IFinancialReportService>();

        var today = await financial.GetTrialBalanceAsync(AppTime.Today);
        var future = await financial.GetTrialBalanceAsync(AppTime.Today.AddYears(5));

        // Nothing is posted in the future, so the two must agree exactly.
        Assert.Equal(today.TotalDebit, future.TotalDebit);
        Assert.Equal(today.TotalCredit, future.TotalCredit);
    }

    [Fact]
    public async Task A_trial_balance_always_balances()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 12.35m);
        await tenant.SellAsync(world, qty: 7, unitPrice: 29.99m, paid: 100m);
        await tenant.SellAsync(world, qty: 3, unitPrice: 31.50m, paid: 0m);

        var trial = await tenant.Get<IFinancialReportService>().GetTrialBalanceAsync(AppTime.Today);

        Assert.Equal(trial.TotalDebit, trial.TotalCredit);
    }

    // ------------------------------------------------------------------------------------------
    // The dropdowns a cashier picks from.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_credit_note_source_dropdown_does_not_offer_a_voided_goods_receipt()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "cancelled", TenantData.TestUserId);

        // AddSupplierCreditNote.cshtml binds its "against which purchase" select to this list.
        var offered = tenant.Get<IComboboxRepository>().GetPurchases()
            .Select(i => i.Value)
            .ToList();

        Assert.DoesNotContain(grn.StockMainID.ToString(), offered);
    }

    [Fact]
    public async Task The_activity_log_entity_dropdown_shows_only_this_pharmacys_entities()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        // Give the FIRST pharmacy an entity type the second one has never touched.
        await first.Get<IActivityLogService>().LogActivityAsync(
            7, "first@test.local", Domain.Enums.ActivityType.Create,
            "SecretPharmacyOnlyEntity", "1");

        var offered = second.Get<IComboboxRepository>().GetEntityNamesForLog()
            .Select(i => i.Value)
            .ToList();

        Assert.DoesNotContain("SecretPharmacyOnlyEntity", offered);
    }

    [Fact]
    public async Task A_supplier_dropdown_never_offers_a_customer()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedCustomerAsync("Retail Buyer");
        var supplier = await tenant.SeedSupplierAsync("Acme Distributors");

        var offered = tenant.Get<IComboboxRepository>().GetActivePartiesByType("Supplier")
            .Select(i => i.Value)
            .ToList();

        Assert.Contains(supplier.PartyID.ToString(), offered);
        Assert.Single(offered);
    }

    // ------------------------------------------------------------------------------------------
    // Document lifecycle edges.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_approved_purchase_order_cannot_be_deactivated_once_goods_arrived_against_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var orders = tenant.Get<IPurchaseOrderService>();

        var po = await orders.CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await orders.ApproveAsync(po.StockMainID, TenantData.TestUserId);

        await tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = po.StockMainID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 4, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        // Refusing outright is the correct outcome; so is quietly declining. Only actually voiding
        // the order while a live GRN still points at it is a defect.
        await Record.ExceptionAsync(() => orders.ToggleStatusAsync(po.StockMainID, TenantData.TestUserId));

        var status = await tenant.Db.StockMains.AsNoTracking()
            .Where(s => s.StockMainID == po.StockMainID)
            .Select(s => s.Status)
            .FirstAsync();

        Assert.True(status != "Void",
            "A purchase order was deactivated after goods had already been received against it. The " +
            "GRN keeps pointing at it, so the receipt's own order is gone from every list that " +
            "filters on IsActive and the outstanding-quantity check has nothing left to read.");
    }

    [Fact]
    public async Task A_purchase_order_cannot_be_raised_with_no_lines_at_all()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        try
        {
            await tenant.Get<IPurchaseOrderService>().CreateAsync(new StockMain
            {
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>()
            }, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return; // Refusing an empty order is the correct outcome.
        }

        Assert.Fail("An empty purchase order was created — a document ordering nothing, which can " +
                    "then be approved and sits forever as an outstanding commitment of zero.");
    }

    [Fact]
    public async Task A_credit_note_cannot_be_raised_against_a_voided_goods_receipt()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "cancelled", TenantData.TestUserId);

        var adjustmentAccount = await tenant.Db.Accounts.AsNoTracking()
            .Where(a => a.Name == "Damage & Loss").Select(a => a.AccountID).FirstAsync();

        try
        {
            await tenant.Get<ISupplierCreditNoteService>().CreateAsync(new SupplierCreditNote
            {
                Party_ID = world.Supplier.PartyID,
                SourceStockMain_ID = grn.StockMainID,
                AdjustmentAccount_ID = adjustmentAccount,
                TotalAmount = 50m,
                CreditDate = AppTime.Now,
                Remarks = "against a purchase that no longer exists"
            }, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        Assert.Fail("A supplier credit note was raised against a VOIDED goods receipt. The void " +
                    "already reversed the payable the credit note now reduces again, so the " +
                    "supplier's balance is understated by the credit amount.");
    }

    // ------------------------------------------------------------------------------------------
    // Opening balances after the fact.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Changing_an_opening_balance_after_trading_keeps_the_ledger_and_the_report_agreed()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var parties = tenant.Get<IPartyService>();

        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.SellAsync(world, qty: 4, unitPrice: 25m, paid: 0m);

        // The owner remembers this customer already owed 300 from the old paper ledger.
        var stored = await tenant.Db.Parties.AsNoTracking()
            .FirstAsync(p => p.PartyID == world.Customer.PartyID);

        await parties.UpdateAsync(new Party
        {
            PartyID = stored.PartyID,
            Name = stored.Name,
            PartyType = stored.PartyType,
            CreditLimit = stored.CreditLimit,
            OpeningBalance = 300m,
            Account_ID = stored.Account_ID,
            IsActive = true
        }, TenantData.TestUserId);

        var accountBalance = await tenant.Db.VoucherDetails.AsNoTracking()
            .Where(d => d.Account_ID == stored.Account_ID && d.Voucher!.Status == "Posted")
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        var summary = await tenant.Get<ISalesReportService>()
            .GetCustomerBalanceSummaryAsync(AppTime.Today);

        var reported = summary.Rows
            .Where(r => r.PartyId == world.Customer.PartyID)
            .Sum(r => r.BalanceDue);

        Assert.Equal(accountBalance, reported);
    }

    [Fact]
    public async Task A_partys_opening_balance_cannot_be_changed_into_a_period_that_is_closed()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var customer = await tenant.SeedCustomerAsync("Late Adjustment");

        await tenant.CloseCurrentPeriodAsync();

        var stored = await tenant.Db.Parties.AsNoTracking()
            .FirstAsync(p => p.PartyID == customer.PartyID);

        var error = await Record.ExceptionAsync(() => tenant.Get<IPartyService>().UpdateAsync(new Party
        {
            PartyID = stored.PartyID,
            Name = stored.Name,
            PartyType = stored.PartyType,
            OpeningBalance = 500m,
            Account_ID = stored.Account_ID,
            IsActive = true
        }, TenantData.TestUserId));

        Assert.IsType<InvalidOperationException>(error);
    }

    // ------------------------------------------------------------------------------------------
    // Financial period hygiene.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_period_cannot_be_closed_while_an_earlier_one_is_still_open()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var periods = tenant.Get<IFinancialPeriodService>();

        var current = await tenant.Db.FinancialPeriods.AsNoTracking()
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);

        var next = await periods.CreateAsync(new FinancialPeriod
        {
            Name = "Next Year",
            StartDate = current.EndDate.AddDays(1),
            EndDate = current.EndDate.AddDays(365)
        }, TenantData.TestUserId);

        // Refusal-by-exception is a legitimate outcome; a later period actually closing while an
        // earlier one stays open is the defect.
        var closed = false;
        await Record.ExceptionAsync(async () =>
            closed = await periods.ClosePeriodAsync(next.PeriodID, "closed early", TenantData.TestUserId));

        var earlierStillOpen = await tenant.Db.FinancialPeriods.AsNoTracking()
            .AnyAsync(p => !p.IsClosed && p.EndDate < next.StartDate);

        Assert.False(closed && earlierStillOpen,
            "A later financial period was closed while an earlier one is still open. Closing a " +
            "period is what declares its figures final; doing it out of order means the earlier " +
            "period can still be posted into after the later one was signed off, and the two will " +
            "never reconcile.");
    }

    [Fact]
    public async Task A_period_with_no_name_is_refused()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var periods = tenant.Get<IFinancialPeriodService>();

        var current = await tenant.Db.FinancialPeriods.AsNoTracking()
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);

        var error = await Record.ExceptionAsync(() => periods.CreateAsync(new FinancialPeriod
        {
            Name = "   ",
            StartDate = current.EndDate.AddDays(1),
            EndDate = current.EndDate.AddDays(30)
        }, TenantData.TestUserId));

        Assert.True(error is InvalidOperationException or ArgumentException,
            $"A financial period was created with a blank name (result: {error?.GetType().Name ?? "accepted"}). " +
            "The period name is the only label the close screen shows.");
    }
}
