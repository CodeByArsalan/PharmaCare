using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Application.Exceptions;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Regression probes for the 2026-08-21 sweep: each test pins a defect found and fixed in that
/// sweep, so the failure mode cannot silently return.
/// </summary>
[Collection(Collections.Database)]
public class Sweep3RegressionTests
{
    private readonly DatabaseFixture _fixture;
    public Sweep3RegressionTests(DatabaseFixture fixture) => _fixture = fixture;

    private static StockMain Return(int saleId, int productId, decimal qty, decimal unitPrice) => new()
    {
        ReferenceStockMain_ID = saleId,
        TransactionDate = AppTime.Now,
        StockDetails = new List<StockDetail>
        {
            new() { Product_ID = productId, Quantity = qty, UnitPrice = unitPrice }
        }
    };

    private static StockMain GrnDoc(TenantWorld world, decimal qty, decimal cost, decimal paid = 0, int? poId = null) => new()
    {
        Party_ID = world.Supplier.PartyID,
        ReferenceStockMain_ID = poId,
        TransactionDate = AppTime.Now,
        PaidAmount = paid,
        StockDetails = new List<StockDetail>
        {
            new() { Product_ID = world.Product.ProductID, Quantity = qty, UnitPrice = cost, CostPrice = cost }
        }
    };

    private static Payment Pay(int partyId, int? stockMainId, int accountId, decimal amount) => new()
    {
        Party_ID = partyId,
        StockMain_ID = stockMainId,
        Account_ID = accountId,
        Amount = amount,
        PaymentDate = AppTime.Now,
        PaymentMethod = "Cash"
    };

    private static async Task<StockMain> ApprovedPoAsync(
        TenantScope tenant, TenantWorld world, decimal qty = 10, decimal price = 10m)
    {
        var po = await tenant.Get<IPurchaseOrderService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = qty, UnitPrice = price, CostPrice = price }
            }
        }, TenantData.TestUserId);

        Assert.True(await tenant.Get<IPurchaseOrderService>().ApproveAsync(po.StockMainID, TenantData.TestUserId));
        return po;
    }

    // ------------------------------------------------------------------ reports

    /// <summary>
    /// SaleReturnService already nets a return off the referenced sale's BalanceAmount, so the
    /// by-customer report must NOT deduct the return's own balance a second time.
    /// </summary>
    [Fact]
    public async Task Sales_by_customer_does_not_double_deduct_returns_from_balance_due()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 0m); // 200 credit
        await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m), TenantData.TestUserId); // 80

        var vm = await tenant.Get<ISalesReportService>().GetSalesByCustomerAsync(new DateRangeFilter
        {
            FromDate = AppTime.Today.AddDays(-1),
            ToDate = AppTime.Today
        });

        var row = Assert.Single(vm.Rows, r => r.PartyId == world.Customer.PartyID);
        Assert.Equal(120m, row.BalanceDue);
        Assert.Equal(120m, vm.TotalBalance);
    }

    [Fact]
    public async Task Purchase_by_supplier_does_not_double_deduct_returns_from_balance_due()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m); // 1000 unpaid

        await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 20, CostPrice = 10m }
            }
        }, TenantData.TestUserId); // 200 returned

        var vm = await tenant.Get<IPurchaseReportService>().GetPurchaseBySupplierAsync(new DateRangeFilter
        {
            FromDate = AppTime.Today.AddDays(-1),
            ToDate = AppTime.Today
        });

        var row = Assert.Single(vm.Rows, r => r.PartyId == world.Supplier.PartyID);
        Assert.Equal(800m, row.BalanceDue);
    }

    /// <summary>
    /// Deactivating a debtor is a catalogue gesture — the money they owe must keep appearing in
    /// the balance summary, exactly as the receivables aging keeps reporting it.
    /// </summary>
    [Fact]
    public async Task An_inactive_customer_with_a_balance_stays_in_the_balance_summary()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 0m); // owes 200

        var customer = await tenant.Db.Parties.FirstAsync(p => p.PartyID == world.Customer.PartyID);
        customer.IsActive = false;
        await tenant.Db.SaveChangesAsync();

        var vm = await tenant.Get<ISalesReportService>().GetCustomerBalanceSummaryAsync(AppTime.Today);

        var row = Assert.Single(vm.Rows, r => r.PartyId == world.Customer.PartyID);
        Assert.Equal(200m, row.BalanceDue);
    }

    /// <summary>
    /// "Cash collected" on a daily summary means cash received THAT day — from Payment events,
    /// not from the PaidAmount of invoices dated that day (which later receipts rewrite).
    /// </summary>
    [Fact]
    public async Task Daily_cash_collected_reflects_the_receipt_date_not_the_invoice_date()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var saleDate = AppTime.Now.AddDays(-5);
        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = saleDate,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
        {
            StockMain_ID = sale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 200m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        var reports = tenant.Get<ISalesReportService>();
        Assert.Equal(200m, (await reports.GetDailySalesSummaryAsync(AppTime.Today)).CashCollected);
        Assert.Equal(0m, (await reports.GetDailySalesSummaryAsync(saleDate.Date)).CashCollected);
    }

    /// <summary>A stock write-off must reduce Net Profit on the P&amp;L, matching the GL.</summary>
    [Fact]
    public async Task Profit_and_loss_includes_stock_write_offs_as_an_expense()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
        {
            TransactionDate = AppTime.Now,
            AdjustmentType = "Write-off",
            AdjustmentReason = "Expired",
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 5 } // 50 at GRN cost
            }
        }, TenantData.TestUserId);

        var vm = await tenant.Get<IFinancialReportService>().GetProfitLossAsync(new DateRangeFilter
        {
            FromDate = AppTime.Today.AddDays(-1),
            ToDate = AppTime.Today
        });

        var writeOffLine = Assert.Single(vm.ExpensesByCategory, e => e.CategoryName == "Stock Write-offs (net)");
        Assert.Equal(50m, writeOffLine.Amount);
        Assert.Equal(50m, vm.TotalExpenses);
    }

    // --------------------------------------------------------- stock & documents

    /// <summary>
    /// The stock-adjustment void endpoint only voids adjustments. Handing it another document
    /// type's id (a sale, a GRN) must be a no-op — otherwise it voids that document while
    /// skipping every type-specific guard (negative stock, active returns, advance conversion).
    /// </summary>
    [Fact]
    public async Task The_adjustment_void_endpoint_refuses_documents_of_other_types()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 100, unitPrice: 20m, paid: 2000m); // stock now 0

        // Voiding the GRN through the adjustment endpoint would drive stock to -100.
        Assert.False(await tenant.Get<IStockAdjustmentService>().VoidAsync(grn.StockMainID, "probe", TenantData.TestUserId));
        Assert.False(await tenant.Get<IStockAdjustmentService>().VoidAsync(sale.StockMainID, "probe", TenantData.TestUserId));

        Assert.Equal("Approved", (await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID)).Status);
        Assert.Equal("Approved", (await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == sale.StockMainID)).Status);
        Assert.Equal(0m, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    /// <summary>
    /// Return-line costs are never client input: the service must overwrite a posted CostPrice
    /// with the reference sale's authoritative cost, or the ledger's inventory/COGS amounts (and
    /// reported profit) are steerable from the browser.
    /// </summary>
    [Fact]
    public async Task A_sale_return_ignores_client_posted_costs()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var doc = Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m);
        doc.StockDetails.First().CostPrice = 99_999m;
        doc.StockDetails.First().LineCost = 1m;

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(doc, TenantData.TestUserId);

        var stored = await tenant.Db.StockDetails.AsNoTracking().FirstAsync(d => d.StockMain_ID == ret.StockMainID);
        Assert.Equal(10m, stored.CostPrice);
        Assert.Equal(40m, stored.LineCost);
    }

    /// <summary>A sale may carry duplicate lines of one product; the returnable quantity is their SUM.</summary>
    [Fact]
    public async Task A_return_can_span_duplicate_sale_lines_of_the_same_product()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 200m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 20m },
                new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        // 8 exceeds either single line (5) but not the sold total (10) — must be accepted.
        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 8, unitPrice: 20m), TenantData.TestUserId);

        Assert.Equal("Approved", ret.Status);
        Assert.Equal(98m, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    /// <summary>
    /// Editing a PO-linked GRN must not count the GRN's own persisted lines as "already
    /// received" — a plain re-save of a fully-received GRN used to be rejected as over-receiving.
    /// </summary>
    [Fact]
    public async Task A_PO_linked_GRN_can_be_edited_without_tripping_the_over_receive_check()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m);
        var grn = await tenant.Get<IPurchaseService>().CreateAsync(
            GrnDoc(world, 10, 10m, poId: po.StockMainID), TenantData.TestUserId);

        var edit = GrnDoc(world, 10, 10m, poId: po.StockMainID);
        edit.StockMainID = grn.StockMainID;
        edit.RowVersion = grn.RowVersion;
        edit.Remarks = "corrected remarks";

        var updated = await tenant.Get<IPurchaseService>().UpdateAsync(edit, TenantData.TestUserId);
        Assert.Equal("corrected remarks", updated.Remarks);
    }

    /// <summary>An edit cannot re-point a GRN at a party that could never have been its supplier.</summary>
    [Fact]
    public async Task A_GRN_edit_cannot_repoint_the_document_at_a_customer()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m);

        var edit = GrnDoc(world, 10, 10m);
        edit.StockMainID = grn.StockMainID;
        edit.RowVersion = grn.RowVersion;
        edit.Party_ID = world.Customer.PartyID;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(edit, TenantData.TestUserId));
    }

    /// <summary>
    /// A GRN line discount changes what the goods cost: the authoritative cost must be the net
    /// rate, and a full return must credit exactly what was charged — never the gross price.
    /// </summary>
    [Fact]
    public async Task A_GRN_line_discount_is_folded_into_the_authoritative_cost()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var doc = GrnDoc(world, 10, 10m);
        doc.StockDetails.First().DiscountPercent = 10m; // pays 90 for 10 units
        var grn = await tenant.Get<IPurchaseService>().CreateAsync(doc, TenantData.TestUserId);

        var costs = await tenant.Get<IProductService>().GetLastGrnCostPricesAsync(new[] { world.Product.ProductID });
        Assert.Equal(9m, costs[world.Product.ProductID]);

        var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10 }
            }
        }, TenantData.TestUserId);

        Assert.Equal(90m, ret.TotalAmount); // credits what was paid, not the gross 100
    }

    /// <summary>
    /// Per-unit prices round to 2dp, so qty × rounded-rate can exceed what was charged. The
    /// credited value must be capped at the charged value — no penny-mining into credit notes.
    /// </summary>
    [Fact]
    public async Task A_full_return_never_credits_more_than_the_customer_paid()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 0.30m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 1.01m,
            StockDetails = new List<StockDetail>
            {
                // 3 × 0.34 = 1.02 gross, 0.01 line discount → charged 1.01. The naive
                // per-unit round-trip credits 3 × round(1.01/3) = 1.02.
                new() { Product_ID = world.Product.ProductID, Quantity = 3, UnitPrice = 0.34m, DiscountAmount = 0.01m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 3, unitPrice: 0.34m), TenantData.TestUserId);

        Assert.Equal(1.01m, ret.TotalAmount);
    }

    /// <summary>Documents record what has happened — a future-dated sale must be rejected.</summary>
    [Fact]
    public async Task A_future_dated_sale_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now.AddDays(3),
                PaidAmount = 20m,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1, UnitPrice = 20m }
                }
            }, TenantData.TestUserId, world.Cash.AccountID));
    }

    /// <summary>Deactivated products stay sellable-looking only in stale UIs — the service must refuse.</summary>
    [Fact]
    public async Task An_inactive_product_cannot_be_sold_or_purchased()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var product = await tenant.Db.Products.FirstAsync(p => p.ProductID == world.Product.ProductID);
        product.IsActive = false;
        await tenant.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.SellAsync(world, qty: 1, unitPrice: 20m, paid: 20m));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m));
    }

    [Fact]
    public async Task A_product_with_a_negative_opening_quantity_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await Assert.ThrowsAsync<PricingValidationException>(() =>
            tenant.Get<IProductService>().CreateAsync(new PharmaCare.Domain.Entities.Configuration.Product
            {
                Name = "Backwards stock",
                Category_ID = world.Category.CategoryID,
                SubCategory_ID = world.SubCategory.SubCategoryID,
                OpeningPrice = 10m,
                OpeningQuantity = -5
            }, TenantData.TestUserId));
    }

    // ----------------------------------------------------------- pricing gates

    /// <summary>
    /// The unit-price floor alone is bypassable: a 100% line or header discount pulls the
    /// realized amount under cost while UnitPrice stays at it. The service must gate on the net.
    /// </summary>
    [Fact]
    public async Task Below_cost_sales_via_discounts_are_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        // Line discount pulls a cost-priced line to zero.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1, UnitPrice = 10m, DiscountPercent = 100m }
                }
            }, TenantData.TestUserId, world.Cash.AccountID));

        // Header discount does the same at header level.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now,
                DiscountPercent = 60m, // 20 → 8, below the 10 cost
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1, UnitPrice = 20m }
                }
            }, TenantData.TestUserId, world.Cash.AccountID));
    }

    /// <summary>A header discount percent beyond 100 would drive the total negative — rejected at the service.</summary>
    [Fact]
    public async Task A_header_discount_over_100_percent_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now,
                DiscountPercent = 150m,
                PaidAmount = 0m,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1, UnitPrice = 20m }
                }
            }, TenantData.TestUserId, world.Cash.AccountID));
    }

    // ----------------------------------------------------- credit notes & voids

    /// <summary>
    /// Voiding a sale must hand back any customer credit applied to it — the purchase side has
    /// always done this for supplier credit notes; the sale side silently destroyed the credit.
    /// </summary>
    [Fact]
    public async Task Voiding_a_sale_restores_the_credit_note_applied_to_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        // Paid sale + full return mints a credit note for the overpayment.
        var saleA = await tenant.SellAsync(world, qty: 2, unitPrice: 20m, paid: 40m);
        await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(saleA.StockMainID, world.Product.ProductID, qty: 2, unitPrice: 20m), TenantData.TestUserId);

        var note = await tenant.Db.CreditNotes.AsNoTracking()
            .FirstAsync(c => c.Party_ID == world.Customer.PartyID && c.Status == "Open");
        Assert.Equal(40m, note.BalanceAmount);

        // Apply it to a credit sale, then void that sale.
        var saleB = await tenant.SellAsync(world, qty: 2, unitPrice: 20m, paid: 0m);
        await tenant.Get<ICustomerPaymentService>().ApplyCreditNoteAsync(
            note.CreditNoteID, saleB.StockMainID, 40m, TenantData.TestUserId);
        Assert.True(await tenant.Get<ISaleService>().VoidAsync(saleB.StockMainID, "cancelled", TenantData.TestUserId));

        var restored = await tenant.Db.CreditNotes.AsNoTracking().FirstAsync(c => c.CreditNoteID == note.CreditNoteID);
        Assert.Equal("Open", restored.Status);
        Assert.Equal(40m, restored.BalanceAmount);
        Assert.Equal(0m, restored.AppliedAmount);
        Assert.False(await tenant.Db.PaymentAllocations.AnyAsync(
            a => a.StockMain_ID == saleB.StockMainID && a.SourceType == "CreditNote"));
    }

    /// <summary>
    /// A receipt whose overpayment already funded a credit note cannot be voided while that
    /// credit is outstanding — the credit would become phantom value with no cash behind it.
    /// </summary>
    [Fact]
    public async Task A_receipt_funding_an_outstanding_credit_note_cannot_be_voided()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.SellAsync(world, qty: 2, unitPrice: 20m, paid: 40m);
        await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 2, unitPrice: 20m), TenantData.TestUserId);

        var receipt = await tenant.Db.Payments.AsNoTracking()
            .FirstAsync(p => p.StockMain_ID == sale.StockMainID && p.PaymentType == "RECEIPT" && !p.IsVoided);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ICustomerPaymentService>().VoidReceiptAsync(receipt.PaymentID, "probe", TenantData.TestUserId));
    }

    /// <summary>
    /// Voiding a supplier payment must recompute the GRN balance NET of purchase returns —
    /// the stale Total−Paid formula resurrected returned value that credit notes then over-applied.
    /// </summary>
    [Fact]
    public async Task Voiding_a_supplier_payment_recomputes_the_balance_net_of_returns()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m); // 1000

        await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 40, CostPrice = 10m }
            }
        }, TenantData.TestUserId); // net owed 600

        var payment = await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn.StockMainID, world.Cash.AccountID, 600m), TenantData.TestUserId);

        Assert.True(await tenant.Get<IPaymentService>().VoidPaymentAsync(payment.PaymentID, "probe", TenantData.TestUserId));

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID);
        Assert.Equal(600m, reloaded.BalanceAmount); // not the stale 1000
    }

    /// <summary>
    /// One PO advance, two GRNs: the auto-adjust rule must never spend advance that the explicit
    /// transfer can still move — only the transfer consumes PO-linked money.
    /// </summary>
    [Fact]
    public async Task A_PO_advance_cannot_be_consumed_twice_across_two_GRNs()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m); // 100
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 40m), TenantData.TestUserId);

        // GRN1 receives half with no transfer: the PO advance is reserved, so nothing lands here.
        var grn1 = await tenant.Get<IPurchaseService>().CreateAsync(
            GrnDoc(world, 5, 10m, poId: po.StockMainID), TenantData.TestUserId);
        Assert.Equal(0m, grn1.PaidAmount);

        // GRN2 transfers the whole 40 — the only consumption of that advance.
        var grn2 = await tenant.Get<IPurchaseService>().CreateAsync(
            GrnDoc(world, 5, 10m, paid: 40m, poId: po.StockMainID),
            TenantData.TestUserId, paymentAccountId: null, transferredAdvanceAmount: 40m);
        Assert.Equal(40m, grn2.PaidAmount);

        // Together the two GRNs owe exactly 60 — the truth the old behavior hid.
        Assert.Equal(60m, grn1.BalanceAmount + grn2.BalanceAmount);
    }

    // -------------------------------------------------------- accounts & tenancy

    /// <summary>Manual journals must resolve every line account inside the caller's own tenant.</summary>
    [Fact]
    public async Task A_journal_voucher_naming_another_tenants_account_is_rejected()
    {
        using var tenantA = await _fixture.NewTenantAsync();
        await tenantA.SeedWorldAsync();
        var foreignAccount = await tenantA.Db.Accounts.AsNoTracking().FirstAsync(a => a.Name == "Sales Revenue");

        using var tenantB = await _fixture.NewTenantAsync();
        await tenantB.SeedWorldAsync();
        var jvType = await tenantB.Db.VoucherTypes.FirstAsync(t => t.Code == "JV");
        var cash = await tenantB.Db.Accounts.FirstAsync(a => a.Name == "Cash in Hand");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenantB.Get<IJournalVoucherService>().CreateJournalVoucherAsync(new JournalVoucherDto
            {
                VoucherType_ID = jvType.VoucherTypeID,
                VoucherDate = AppTime.Now,
                Narration = "cross-tenant probe",
                VoucherDetails = new List<JournalVoucherDetailDto>
                {
                    new() { Account_ID = cash.AccountID, DebitAmount = 100m, CreditAmount = 0 },
                    new() { Account_ID = foreignAccount.AccountID, DebitAmount = 0, CreditAmount = 100m }
                }
            }, TenantData.TestUserId));
    }

    /// <summary>An expense's source account is the credit side of its voucher — Cash/Bank only.</summary>
    [Fact]
    public async Task An_expense_cannot_credit_a_revenue_account()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var category = await tenant.SeedExpenseCategoryAsync();
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>().CreateAsync(new Expense
            {
                ExpenseCategory_ID = category.ExpenseCategoryID,
                SourceAccount_ID = revenue.AccountID,
                Amount = 500m,
                ExpenseDate = AppTime.Now,
                Description = "fabricated income probe"
            }, TenantData.TestUserId));
    }

    /// <summary>A category's posting accounts must belong to the tenant that owns the category.</summary>
    [Fact]
    public async Task A_category_naming_another_tenants_account_is_rejected()
    {
        using var tenantA = await _fixture.NewTenantAsync();
        await tenantA.SeedWorldAsync();
        var foreignStock = await tenantA.Db.Accounts.AsNoTracking().FirstAsync(a => a.Name == "Inventory / Stock");

        using var tenantB = await _fixture.NewTenantAsync();
        await tenantB.SeedWorldAsync();
        var ownSale = await tenantB.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");
        var ownCogs = await tenantB.Db.Accounts.FirstAsync(a => a.Name == "Cost of Goods Sold");
        var ownDamage = await tenantB.Db.Accounts.FirstAsync(a => a.Name == "Damage & Loss");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenantB.Get<ICategoryService>().CreateAsync(new PharmaCare.Domain.Entities.Configuration.Category
            {
                Name = "Injected",
                StockAccount_ID = foreignStock.AccountID,
                SaleAccount_ID = ownSale.AccountID,
                COGSAccount_ID = ownCogs.AccountID,
                DamageAccount_ID = ownDamage.AccountID
            }, TenantData.TestUserId));
    }

    /// <summary>
    /// PriceTypes are per-tenant identity rows: the second pharmacy's Retail/Wholesale ids are
    /// NOT 1/2, and its explicitly configured prices must still be honored end-to-end.
    /// </summary>
    [Fact]
    public async Task Price_types_resolve_per_tenant_and_second_tenant_prices_are_honored()
    {
        using var tenantA = await _fixture.NewTenantAsync();
        await tenantA.SeedWorldAsync();
        var retailA = await tenantA.Get<IProductService>().GetRetailPriceTypeIdAsync();

        using var tenantB = await _fixture.NewTenantAsync();
        var worldB = await tenantB.SeedWorldAsync();
        await tenantB.ReceiveStockAsync(worldB.Supplier, worldB.Product, 10, unitCost: 10m);

        var products = tenantB.Get<IProductService>();
        var retailB = await products.GetRetailPriceTypeIdAsync();
        var wholesaleB = await products.GetWholesalePriceTypeIdAsync();

        Assert.NotNull(retailA);
        Assert.NotNull(retailB);
        Assert.NotNull(wholesaleB);
        Assert.NotEqual(retailA, retailB);

        await products.SaveProductPricesAsync(worldB.Product.ProductID, new List<ProductPriceDto>
        {
            new() { PriceTypeId = retailB!.Value, PriceTypeName = "Retail", Price = 55m }
        }, TenantData.TestUserId);

        var withStock = await products.GetProductsWithStockAsync(retailB);
        var row = Assert.Single(withStock, p => p.Product.ProductID == worldB.Product.ProductID);
        Assert.Equal(55m, row.SpecificPrice);

        // And another tenant's PriceType id is rejected outright.
        await Assert.ThrowsAsync<PricingValidationException>(() =>
            products.SaveProductPricesAsync(worldB.Product.ProductID, new List<ProductPriceDto>
            {
                new() { PriceTypeId = retailA!.Value, PriceTypeName = "Retail", Price = 55m }
            }, TenantData.TestUserId));
    }

    // ------------------------------------------------------------------ periods

    /// <summary>Mirror of close-in-order: an earlier period cannot reopen under a closed later one.</summary>
    [Fact]
    public async Task Reopening_an_earlier_period_under_a_closed_later_one_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var periods = tenant.Get<IFinancialPeriodService>();

        var lastYear = AppTime.Now.Year - 1;
        var earlier = await periods.CreateAsync(new FinancialPeriod
        {
            Name = $"FY {lastYear}",
            StartDate = new DateTime(lastYear, 1, 1),
            EndDate = new DateTime(lastYear, 12, 31)
        }, TenantData.TestUserId);

        var current = await tenant.Db.FinancialPeriods
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);

        Assert.True(await periods.ClosePeriodAsync(earlier.PeriodID, null, TenantData.TestUserId));
        Assert.True(await periods.ClosePeriodAsync(current.PeriodID, null, TenantData.TestUserId));

        // Out of order: the later period is still closed.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            periods.OpenPeriodAsync(earlier.PeriodID, TenantData.TestUserId));

        // Newest first works.
        Assert.True(await periods.OpenPeriodAsync(current.PeriodID, TenantData.TestUserId));
        Assert.True(await periods.OpenPeriodAsync(earlier.PeriodID, TenantData.TestUserId));
    }

    // ------------------------------------------------------------- numbering

    /// <summary>
    /// Document numbers must keep counting past 9999 in a day: a plain string sort put
    /// "-10000" below "-9999" and re-issued 10000 forever into the unique index.
    /// </summary>
    [Fact]
    public async Task Document_numbering_survives_the_five_digit_rollover()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var saleType = await tenant.Db.TransactionTypes.FirstAsync(t => t.Code == "SALE");
        var datePrefix = $"SALE-{AppTime.Now:yyyyMMdd}-";
        foreach (var number in new[] { $"{datePrefix}9999", $"{datePrefix}10000" })
        {
            tenant.Db.StockMains.Add(new StockMain
            {
                TransactionNo = number,
                TransactionDate = AppTime.Now,
                TransactionType_ID = saleType.TransactionTypeID,
                Party_ID = world.Customer.PartyID,
                Status = "Approved",
                CreatedAt = AppTime.Now,
                CreatedBy = TenantData.TestUserId
            });
        }
        await tenant.Db.SaveChangesAsync();

        var sale = await tenant.SellAsync(world, qty: 1, unitPrice: 20m, paid: 20m);
        Assert.Equal($"{datePrefix}10001", sale.TransactionNo);
    }
}
