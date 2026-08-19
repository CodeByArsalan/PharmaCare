using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Probes from the 2026-08-19 exhaustive sweep. Every test here asserts behavior the system is
/// SUPPOSED to have; a failing test is a confirmed defect, not a broken test. Each test names the
/// finding it probes.
/// </summary>
[Collection(Collections.Database)]
public class DefectProbeTests
{
    private readonly DatabaseFixture _fixture;

    public DefectProbeTests(DatabaseFixture fixture) => _fixture = fixture;

    // ---------------- Stock: aggregate availability ----------------

    /// <summary>
    /// STK-1: two write-off lines for the same product, each within stock but jointly above it,
    /// must not drive derived stock negative. The controller deserializes client JSON, so
    /// duplicate lines are attacker-reachable.
    /// </summary>
    [Fact]
    public async Task Duplicate_write_off_lines_cannot_take_stock_negative()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Record.ExceptionAsync(() => tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
        {
            TransactionDate = AppTime.Now,
            AdjustmentType = "Write-off",
            AdjustmentReason = "Damaged",
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 60 },
                new() { Product_ID = world.Product.ProductID, Quantity = 60 }
            }
        }, TenantData.TestUserId));

        var stock = await tenant.StockOnHandAsync(world.Product.ProductID);
        Assert.True(stock >= 0, $"duplicate write-off lines drove stock to {stock}");
    }

    /// <summary>STK-2: the same duplicate-line shape must not let a sale oversell past zero.</summary>
    [Fact]
    public async Task Duplicate_sale_lines_cannot_oversell_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m);

        await Record.ExceptionAsync(() => tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 320m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 8, UnitPrice = 20m },
                new() { Product_ID = world.Product.ProductID, Quantity = 8, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID));

        var stock = await tenant.StockOnHandAsync(world.Product.ProductID);
        Assert.True(stock >= 0, $"duplicate sale lines drove stock to {stock}");
    }

    // ---------------- Purchases / payables ----------------

    /// <summary>
    /// PUR-1: after a paid GRN is voided (payment becomes an on-account advance) and that payment
    /// is then itself voided, the supplier must have NO advance left — otherwise the voided money
    /// can be refunded as real cash.
    /// </summary>
    [Fact]
    public async Task A_voided_supplier_payment_no_longer_counts_toward_the_supplier_advance()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var payment = await tenant.Get<IPaymentService>().CreatePaymentAsync(new Payment
        {
            Party_ID = world.Supplier.PartyID,
            StockMain_ID = grn.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 1000m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));
        Assert.True(await tenant.Get<IPaymentService>().VoidPaymentAsync(payment.PaymentID, "cash returned", TenantData.TestUserId));

        var advance = await tenant.Get<IPurchaseService>().GetSupplierAdvanceAsync(world.Supplier.PartyID);
        Assert.Equal(0m, advance);
    }

    /// <summary>
    /// PUR-3: a GRN carrying a header discount must still post a balanced voucher whose header
    /// agrees with its own lines.
    /// </summary>
    [Fact]
    public async Task A_purchase_with_a_header_discount_posts_a_balanced_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var grn = await tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            DiscountPercent = 10m,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 100, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        var voucher = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .FirstAsync(v => v.SourceTable == "StockMain" && v.SourceID == grn.StockMainID);

        Assert.Equal(voucher.VoucherDetails.Sum(d => d.DebitAmount), voucher.VoucherDetails.Sum(d => d.CreditAmount));
        Assert.Equal(voucher.TotalDebit, voucher.VoucherDetails.Sum(d => d.DebitAmount));
        Assert.Equal(voucher.TotalCredit, voucher.VoucherDetails.Sum(d => d.CreditAmount));
    }

    /// <summary>
    /// PUR-5: the money credited back by a purchase return is set by the GRN's own cost, not by
    /// whatever CostPrice the client posts on the return lines.
    /// </summary>
    [Fact]
    public async Task A_purchase_return_is_valued_at_the_GRN_cost_not_the_posted_price()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 50m);

        var attempt = await Record.ExceptionAsync(async () =>
        {
            var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
            {
                Party_ID = world.Supplier.PartyID,
                ReferenceStockMain_ID = grn.StockMainID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 10, CostPrice = 5000m }
                }
            }, TenantData.TestUserId);

            Assert.Equal(500m, ret.TotalAmount);
        });

        Assert.True(attempt is null || attempt is InvalidOperationException,
            $"inflated return price was neither corrected nor rejected: {attempt?.Message}");
    }

    // ---------------- Sales / receivables ----------------

    /// <summary>
    /// SAL-1: returning goods from a discounted sale must credit the customer what they were
    /// actually charged — not the gross price — and must never mint a credit note for an unpaid sale.
    /// </summary>
    [Fact]
    public async Task Returning_a_discounted_sale_credits_the_discounted_amount_not_the_gross()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 50m);

        // Credit sale: 10 x 100 with a 10% header discount => customer owes 900.
        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            DiscountPercent = 10m,
            PaidAmount = 0m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 100m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        Assert.Equal(900m, sale.TotalAmount);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
        {
            ReferenceStockMain_ID = sale.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 100m }
            }
        }, TenantData.TestUserId);

        Assert.True(ret.TotalAmount <= 900m,
            $"full return of a 900 sale credited {ret.TotalAmount}");
        Assert.False(await tenant.Db.Set<CreditNote>().AnyAsync(),
            "a credit note was minted for a sale the customer never paid");
    }

    /// <summary>SAL-5: a return with a header discount over 100% must not go negative.</summary>
    [Fact]
    public async Task A_sale_return_discount_over_100_percent_cannot_produce_a_negative_total()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var attempt = await Record.ExceptionAsync(async () =>
        {
            var ret = await tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
            {
                ReferenceStockMain_ID = sale.StockMainID,
                TransactionDate = AppTime.Now,
                DiscountPercent = 150m,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 4, UnitPrice = 20m }
                }
            }, TenantData.TestUserId);

            Assert.True(ret.TotalAmount >= 0, $"return total went negative: {ret.TotalAmount}");
        });

        Assert.True(attempt is null || attempt is InvalidOperationException,
            $"an over-100% discount was neither clamped nor rejected: {attempt?.Message}");
    }

    /// <summary>
    /// SAL-2: refunding a credit note must consume it. A second refund of the same note must fail.
    /// </summary>
    [Fact]
    public async Task A_credit_note_cannot_be_refunded_twice()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        // Fully-paid sale, then a partial return => the overpaid slice becomes a credit note.
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);
        await tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
        {
            ReferenceStockMain_ID = sale.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 20m }
            }
        }, TenantData.TestUserId);

        var note = await tenant.Db.Set<CreditNote>().SingleAsync();
        Assert.Equal(100m, note.TotalAmount);

        Payment Refund() => new()
        {
            Party_ID = world.Customer.PartyID,
            Account_ID = world.Cash.AccountID,
            Amount = 100m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        };

        var service = tenant.Get<ICustomerPaymentService>();
        await service.CreateRefundAsync(Refund(), TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRefundAsync(Refund(), TenantData.TestUserId));
    }

    /// <summary>
    /// SAL-3: voiding a sale that has a live return must not leave the return's stock re-entry
    /// behind — that would inflate on-hand with units that were never re-delivered.
    /// </summary>
    [Fact]
    public async Task Voiding_a_sale_with_an_active_return_does_not_create_phantom_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        await tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
        {
            ReferenceStockMain_ID = sale.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 4, UnitPrice = 20m }
            }
        }, TenantData.TestUserId);

        await Record.ExceptionAsync(() =>
            tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "cancelled", TenantData.TestUserId));

        var stock = await tenant.StockOnHandAsync(world.Product.ProductID);
        Assert.True(stock <= 100m, $"void left phantom stock: {stock} on hand from 100 received");
    }

    // ---------------- Accounting engine ----------------

    /// <summary>
    /// ACC-1: after a JV is voided, the per-account trial balance must show no residue. The
    /// aggregate stays balanced either way, so this asserts the individual account rows.
    /// </summary>
    [Fact]
    public async Task Voiding_a_journal_voucher_leaves_no_residue_in_the_per_account_trial_balance()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var jvType = await tenant.Db.VoucherTypes.FirstAsync(t => t.Code == "JV");
        var cash = await tenant.CashAccountAsync();
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");

        var voucher = await tenant.Get<IJournalVoucherService>().CreateJournalVoucherAsync(new JournalVoucherDto
        {
            VoucherType_ID = jvType.VoucherTypeID,
            VoucherDate = AppTime.Now,
            Narration = "probe",
            VoucherDetails = new List<JournalVoucherDetailDto>
            {
                new() { Account_ID = cash.AccountID, DebitAmount = 500m, CreditAmount = 0 },
                new() { Account_ID = revenue.AccountID, DebitAmount = 0, CreditAmount = 500m }
            }
        }, TenantData.TestUserId);

        Assert.True(await tenant.Get<IJournalVoucherService>()
            .VoidVoucherAsync(voucher.VoucherID, "keyed in error", TenantData.TestUserId));

        var tb = await tenant.Get<IFinancialReportService>().GetTrialBalanceAsync(AppTime.Today.AddDays(1));
        foreach (var row in tb.Rows.Where(r => r.AccountId == cash.AccountID || r.AccountId == revenue.AccountID))
        {
            Assert.True(row.Balance == 0m,
                $"account '{row.AccountName}' shows {row.Balance} {row.BalanceType} after the JV was voided");
        }
    }

    /// <summary>
    /// ACC-2: a closed period must stay locked for the whole of its last calendar day, even though
    /// the period's EndDate is stored at midnight and real postings carry a time of day.
    /// </summary>
    [Fact]
    public async Task The_period_lock_holds_on_the_closed_periods_last_day()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();

        var period = await tenant.Db.FinancialPeriods
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);
        await tenant.Get<IFinancialPeriodService>().ClosePeriodAsync(period.PeriodID, "year end", TenantData.TestUserId);

        var lastDayAfternoon = period.EndDate.Date.AddHours(14);
        Assert.True(await tenant.Get<IFinancialPeriodService>().IsPeriodLockedAsync(lastDayAfternoon),
            $"{lastDayAfternoon:s} falls inside the closed period but the lock reports it open");
    }

    /// <summary>
    /// ACC-3: the JV reversal screen must refuse vouchers that belong to source documents (sales,
    /// GRNs...). Reversing those in the GL alone divorces the ledger from the subledger.
    /// </summary>
    [Fact]
    public async Task A_source_document_voucher_cannot_be_reversed_through_the_JV_screen()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 100m);

        var saleVoucher = await tenant.Db.Vouchers
            .FirstAsync(v => v.SourceTable == "StockMain" && v.SourceID == sale.StockMainID);

        var attempt = await Record.ExceptionAsync(async () =>
        {
            var result = await tenant.Get<IJournalVoucherService>()
                .VoidVoucherAsync(saleVoucher.VoucherID, "probe", TenantData.TestUserId);
            Assert.False(result, "the JV screen reversed a sale voucher");
        });

        var reversalExists = await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == saleVoucher.VoucherID);
        Assert.False(reversalExists && sale.Status == "Approved",
            "a GL-only reversal was posted while the sale itself stayed approved");
    }

    /// <summary>
    /// ACC-4: line amounts finer than 2dp must not be able to post a voucher whose STORED lines
    /// are unbalanced once SQL Server rounds each column to decimal(18,2).
    /// </summary>
    [Fact]
    public async Task A_journal_voucher_with_sub_cent_amounts_stores_balanced_lines()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var jvType = await tenant.Db.VoucherTypes.FirstAsync(t => t.Code == "JV");
        var cash = await tenant.CashAccountAsync();
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");

        var attempt = await Record.ExceptionAsync(async () =>
        {
            var voucher = await tenant.Get<IJournalVoucherService>().CreateJournalVoucherAsync(new JournalVoucherDto
            {
                VoucherType_ID = jvType.VoucherTypeID,
                VoucherDate = AppTime.Now,
                Narration = "sub-cent probe",
                VoucherDetails = new List<JournalVoucherDetailDto>
                {
                    new() { Account_ID = cash.AccountID, DebitAmount = 10.005m, CreditAmount = 0 },
                    new() { Account_ID = revenue.AccountID, DebitAmount = 0, CreditAmount = 5.0025m },
                    new() { Account_ID = revenue.AccountID, DebitAmount = 0, CreditAmount = 5.0025m }
                }
            }, TenantData.TestUserId);

            var stored = await tenant.Db.VoucherDetails.AsNoTracking()
                .Where(d => d.Voucher_ID == voucher.VoucherID)
                .ToListAsync();
            Assert.Equal(stored.Sum(d => d.DebitAmount), stored.Sum(d => d.CreditAmount));
        });

        Assert.True(attempt is null or InvalidOperationException,
            $"sub-cent JV was neither normalized nor rejected: {attempt?.Message}");
    }

    // ---------------- Multi-tenancy ----------------

    /// <summary>
    /// SEC-1: an insert carrying another pharmacy's id must not be honoured. Five controllers bind
    /// tenant entities without an allowlist, so the DbContext stamp is the only backstop.
    /// </summary>
    [Fact]
    public async Task An_insert_stamped_with_a_foreign_pharmacy_id_is_not_honoured()
    {
        using var alpha = await _fixture.NewTenantAsync("Alpha");
        using var beta = await _fixture.NewTenantAsync("Beta");

        var (category, _) = await alpha.SeedCategoryAsync();

        var smuggled = new SubCategory
        {
            Name = "Smuggled row",
            Category_ID = category.CategoryID,
            Pharmacy_ID = beta.PharmacyId,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantData.TestUserId
        };

        var attempt = await Record.ExceptionAsync(async () =>
        {
            alpha.Db.SubCategories.Add(smuggled);
            await alpha.Db.SaveChangesAsync();
        });

        if (attempt is null)
        {
            var landedInBeta = await beta.Db.SubCategories.AsNoTracking()
                .AnyAsync(s => s.Name == "Smuggled row");
            Assert.False(landedInBeta, "a row created inside tenant Alpha was stored as owned by tenant Beta");
        }
    }

    // ---------------- Reports ----------------

    /// <summary>RPT-1: the P&amp;L must net returned cost out of COGS the way the GL does.</summary>
    [Fact]
    public async Task The_profit_and_loss_report_nets_returned_cost_out_of_COGS()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        await tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
        {
            ReferenceStockMain_ID = sale.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 20m }
            }
        }, TenantData.TestUserId);

        var pl = await tenant.Get<IFinancialReportService>().GetProfitLossAsync(new DateRangeFilter
        {
            FromDate = AppTime.Today.AddDays(-1),
            ToDate = AppTime.Today.AddDays(1)
        });

        Assert.Equal(100m, pl.NetRevenue); // 200 sold - 100 returned
        Assert.Equal(50m, pl.COGS);        // cost 100 out, 50 came back
        Assert.Equal(50m, pl.GrossProfit);
    }

    /// <summary>RPT-5: a reversed date range must not crash the cash flow report.</summary>
    [Fact]
    public async Task A_reversed_date_range_does_not_crash_the_cash_flow_report()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();

        var attempt = await Record.ExceptionAsync(() =>
            tenant.Get<IFinancialReportService>().GetCashFlowReportAsync(new DateRangeFilter
            {
                FromDate = AppTime.Today,
                ToDate = AppTime.Today.AddDays(-5)
            }));

        Assert.Null(attempt);
    }

    /// <summary>
    /// RPT-3: a cash refund is a real cash movement; the cash flow report must include it or its
    /// closing balance stops reconciling with the cash account.
    /// </summary>
    [Fact]
    public async Task The_cash_flow_report_includes_customer_refunds()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        // Paid sale + full return => 200 of refundable credit; refund it in cash.
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);
        await tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
        {
            ReferenceStockMain_ID = sale.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 20m }
            }
        }, TenantData.TestUserId);

        await tenant.Get<ICustomerPaymentService>().CreateRefundAsync(new Payment
        {
            Party_ID = world.Customer.PartyID,
            Account_ID = world.Cash.AccountID,
            Amount = 200m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        var cashFlow = await tenant.Get<IFinancialReportService>().GetCashFlowReportAsync(new DateRangeFilter
        {
            FromDate = AppTime.Today.AddDays(-1),
            ToDate = AppTime.Today.AddDays(1)
        });

        Assert.True(cashFlow.TotalCashOut >= 200m,
            $"a 200 cash refund left TotalCashOut at {cashFlow.TotalCashOut}");
    }

    /// <summary>
    /// STK-3: a zero-cost GRN line (bonus stock) must not become the product's authoritative cost —
    /// that would disable the below-cost gate and let goods sell with no COGS.
    /// </summary>
    [Fact]
    public async Task A_zero_cost_GRN_does_not_poison_the_authoritative_cost()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        // Free/bonus goods at zero cost; services must keep valuing at the last REAL cost.
        var bonus = await Record.ExceptionAsync(() =>
            tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 0m));

        if (bonus is not null) return; // rejecting zero-cost GRNs outright is also acceptable

        // Selling below the true cost of 10 must still be blocked.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.SellAsync(world, qty: 5, unitPrice: 1m, paid: 5m));
    }
}
