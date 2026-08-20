using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Purchase-side lifecycle audit (2026-08-19 sweep, "purchases" auditor): PO advances, the
/// GRN-void-to-advance rule, supplier credit notes, GRN edits, PO edges, and the payment-account
/// gate. Every test asserts behavior the system is SUPPOSED to have; a failing test is a
/// confirmed defect, not a broken test.
/// </summary>
[Collection(Collections.Database)]
public class PurchaseFlowAuditTests
{
    private readonly DatabaseFixture _fixture;

    public PurchaseFlowAuditTests(DatabaseFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------- helpers

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

    /// <summary>A one-line PO through the real service, approved so it can take advances/GRNs.</summary>
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

    private static async Task<SupplierCreditNote> CreditNoteAsync(
        TenantScope tenant, int supplierId, decimal amount)
    {
        var adjustment = await tenant.Db.Accounts.AsNoTracking().FirstAsync(a => a.Name == "Damage & Loss");
        return await tenant.Get<ISupplierCreditNoteService>().CreateAsync(new SupplierCreditNote
        {
            Party_ID = supplierId,
            TotalAmount = amount,
            CreditDate = AppTime.Now,
            AdjustmentAccount_ID = adjustment.AccountID,
            Remarks = "audit probe"
        }, TenantData.TestUserId);
    }

    private static async Task<int> SupplierAccountIdAsync(TenantScope tenant, int partyId)
        => (await tenant.Db.Parties.AsNoTracking().FirstAsync(p => p.PartyID == partyId)).Account_ID!.Value;

    /// <summary>Net posted GL movement (debits − credits) on one account.</summary>
    private static async Task<decimal> NetDebitAsync(TenantScope tenant, int accountId)
    {
        var lines = await tenant.Db.VoucherDetails.AsNoTracking()
            .Where(d => d.Account_ID == accountId && d.Voucher!.Status == "Posted")
            .ToListAsync();
        return lines.Sum(d => d.DebitAmount - d.CreditAmount);
    }

    private static Task<StockMain> ReloadAsync(TenantScope tenant, int stockMainId)
        => tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == stockMainId);

    /// <summary>The row's current concurrency token — what a real edit form round-trips.</summary>
    private static async Task<byte[]> RowVersionOfAsync(TenantScope tenant, int stockMainId)
        => (await ReloadAsync(tenant, stockMainId)).RowVersion;

    // ---------------------------------------------------------------- 1. PO advances

    /// <summary>An advance against an approved PO is capped at the PO's not-yet-received value.</summary>
    [Fact]
    public async Task An_advance_against_an_approved_PO_is_bounded_by_the_PO_value()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m); // worth 100
        var payments = tenant.Get<IPaymentService>();

        await payments.CreatePaymentAsync(Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 40m), TenantData.TestUserId);

        // 61 would take the advance past the PO's 100.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            payments.CreatePaymentAsync(Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 61m), TenantData.TestUserId));

        await payments.CreatePaymentAsync(Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 60m), TenantData.TestUserId);

        // Fully advanced — nothing further can be paid against it.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            payments.CreatePaymentAsync(Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 0.01m), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_advance_on_a_draft_PO_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var draftPo = await tenant.Get<IPurchaseOrderService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreatePaymentAsync(
                Pay(world.Supplier.PartyID, draftPo.StockMainID, world.Cash.AccountID, 10m), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_advance_on_a_voided_PO_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world);

        Assert.True(await tenant.Get<IPurchaseOrderService>().ToggleStatusAsync(po.StockMainID, TenantData.TestUserId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreatePaymentAsync(
                Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 10m), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_advance_on_a_completed_PO_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m);

        // Receiving everything completes the PO.
        await tenant.Get<IPurchaseService>().CreateAsync(GrnDoc(world, 10, 10m, poId: po.StockMainID), TenantData.TestUserId);
        Assert.Equal("Completed", (await ReloadAsync(tenant, po.StockMainID)).Status);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreatePaymentAsync(
                Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 10m), TenantData.TestUserId));
    }

    [Fact]
    public async Task A_PO_with_a_live_advance_cannot_be_voided_until_the_advance_is_voided()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world);

        var advance = await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 40m), TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseOrderService>().ToggleStatusAsync(po.StockMainID, TenantData.TestUserId));

        Assert.True(await tenant.Get<IPaymentService>().VoidPaymentAsync(advance.PaymentID, "order cancelled", TenantData.TestUserId));
        Assert.True(await tenant.Get<IPurchaseOrderService>().ToggleStatusAsync(po.StockMainID, TenantData.TestUserId));
        Assert.Equal("Void", (await ReloadAsync(tenant, po.StockMainID)).Status);
    }

    /// <summary>
    /// Transferring the whole PO advance onto the GRN must move the SAME money — one payment,
    /// one cash movement — never a second one.
    /// </summary>
    [Fact]
    public async Task A_full_PO_advance_transfer_lands_on_the_GRN_without_double_counting()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m); // worth 100

        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 40m), TenantData.TestUserId);

        var grn = await tenant.Get<IPurchaseService>().CreateAsync(
            GrnDoc(world, 10, 10m, paid: 40m, poId: po.StockMainID),
            TenantData.TestUserId, paymentAccountId: null, transferredAdvanceAmount: 40m);

        Assert.Equal(40m, grn.PaidAmount);
        Assert.Equal(60m, grn.BalanceAmount);

        // The payment row itself moved from the PO to the GRN.
        var supplierPayments = await tenant.Db.Payments.AsNoTracking()
            .Where(p => p.Party_ID == world.Supplier.PartyID && !p.IsVoided && p.PaymentType == "PAYMENT")
            .ToListAsync();
        Assert.Single(supplierPayments);
        Assert.Equal(grn.StockMainID, supplierPayments[0].StockMain_ID);

        // The PO gave its money up and completed on full receipt.
        var reloadedPo = await ReloadAsync(tenant, po.StockMainID);
        Assert.Equal(0m, reloadedPo.PaidAmount);
        Assert.Equal("Completed", reloadedPo.Status);

        // Exactly 40 ever left cash, and the supplier is owed exactly 60.
        Assert.Equal(-40m, await NetDebitAsync(tenant, world.Cash.AccountID));
        var supplierAccountId = await SupplierAccountIdAsync(tenant, world.Supplier.PartyID);
        Assert.Equal(-60m, await NetDebitAsync(tenant, supplierAccountId));
    }

    /// <summary>A partial transfer splits both the payment row and its voucher, to the cent.</summary>
    [Fact]
    public async Task A_partial_PO_advance_transfer_splits_the_payment_and_its_voucher_exactly()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m);

        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 40m), TenantData.TestUserId);

        var grn = await tenant.Get<IPurchaseService>().CreateAsync(
            GrnDoc(world, 10, 10m, paid: 25m, poId: po.StockMainID),
            TenantData.TestUserId, paymentAccountId: null, transferredAdvanceAmount: 25m);

        // 25 is transferred off the PO; the 15 still sitting with the supplier is a free advance,
        // which the standing auto-adjust rule sweeps onto this GRN (same rule asserted by
        // A_converted_advance_is_auto_applied_to_the_next_GRN_exactly_once). Only 40 ever left
        // cash, so the GRN shows 40 paid and 60 owed — asserted below.
        Assert.Equal(40m, grn.PaidAmount);
        Assert.Equal(60m, grn.BalanceAmount);

        var supplierPayments = await tenant.Db.Payments.AsNoTracking()
            .Where(p => p.Party_ID == world.Supplier.PartyID && !p.IsVoided && p.PaymentType == "PAYMENT")
            .OrderBy(p => p.Amount)
            .ToListAsync();

        Assert.Equal(2, supplierPayments.Count);
        Assert.Equal(15m, supplierPayments[0].Amount);
        Assert.Equal(po.StockMainID, supplierPayments[0].StockMain_ID);
        Assert.Equal(25m, supplierPayments[1].Amount);
        Assert.Equal(grn.StockMainID, supplierPayments[1].StockMain_ID);

        // Both halves carry balanced vouchers whose sizes add back up to the original 40.
        foreach (var p in supplierPayments)
        {
            var lines = await tenant.Db.VoucherDetails.AsNoTracking()
                .Where(d => d.Voucher_ID == p.Voucher_ID)
                .ToListAsync();
            Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
            Assert.Equal(p.Amount, lines.Sum(l => l.DebitAmount));
        }

        Assert.Equal(-40m, await NetDebitAsync(tenant, world.Cash.AccountID));
        Assert.Equal(15m, (await ReloadAsync(tenant, po.StockMainID)).PaidAmount);

        // The decisive check: only the 40 that actually left cash may reach the supplier account.
        // The pre-fix double-count credited the transferred 25 twice and showed 65 paid here.
        var supplierAccountId = await SupplierAccountIdAsync(tenant, world.Supplier.PartyID);
        Assert.Equal(-60m, await NetDebitAsync(tenant, supplierAccountId));
    }

    [Fact]
    public async Task Transferring_more_than_the_available_PO_advance_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m);

        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, po.StockMainID, world.Cash.AccountID, 40m), TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().CreateAsync(
                GrnDoc(world, 10, 10m, paid: 50m, poId: po.StockMainID),
                TenantData.TestUserId, paymentAccountId: null, transferredAdvanceAmount: 50m));
    }

    [Fact]
    public async Task An_advance_transfer_without_a_reference_PO_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().CreateAsync(
                GrnDoc(world, 10, 10m, paid: 10m),
                TenantData.TestUserId, paymentAccountId: null, transferredAdvanceAmount: 10m));
    }

    // ---------------------------------------------------------------- 2. GRN void -> advance

    /// <summary>
    /// Voiding a paid GRN keeps the money as a supplier-level advance: the purchase voucher is
    /// reversed, the payment voucher is NOT, and the payment detaches from the dead GRN.
    /// </summary>
    [Fact]
    public async Task Voiding_a_paid_GRN_converts_the_payment_into_a_supplier_advance()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m); // 1000
        var purchaseVoucherId = (await ReloadAsync(tenant, grn.StockMainID)).Voucher_ID!.Value;

        var payment = await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn.StockMainID, world.Cash.AccountID, 600m), TenantData.TestUserId);

        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        Assert.Equal(600m, await tenant.Get<IPurchaseService>().GetSupplierAdvanceAsync(world.Supplier.PartyID));

        var storedPayment = await tenant.Db.Payments.AsNoTracking().FirstAsync(p => p.PaymentID == payment.PaymentID);
        Assert.False(storedPayment.IsVoided);
        Assert.Null(storedPayment.StockMain_ID);

        // PV reversed, CP left standing.
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == purchaseVoucherId));
        Assert.False(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == payment.Voucher_ID));

        // GL agrees: the supplier account holds a 600 debit (our money with them).
        var supplierAccountId = await SupplierAccountIdAsync(tenant, world.Supplier.PartyID);
        Assert.Equal(600m, await NetDebitAsync(tenant, supplierAccountId));
        Assert.Equal(0m, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    /// <summary>The converted advance funds the next GRN once — and only once.</summary>
    [Fact]
    public async Task A_converted_advance_is_auto_applied_to_the_next_GRN_exactly_once()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn1 = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn1.StockMainID, world.Cash.AccountID, 600m), TenantData.TestUserId);
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn1.StockMainID, "wrong goods", TenantData.TestUserId));

        // GRN 2 (1000) should consume the whole 600 advance automatically.
        var grn2 = await tenant.Get<IPurchaseService>().CreateAsync(GrnDoc(world, 100, 10m), TenantData.TestUserId);
        var reloaded2 = await ReloadAsync(tenant, grn2.StockMainID);
        Assert.Equal(600m, reloaded2.PaidAmount);
        Assert.Equal(400m, reloaded2.BalanceAmount);
        Assert.True(await tenant.Db.Payments.AnyAsync(
            p => p.StockMain_ID == grn2.StockMainID && p.PaymentType == "ADJUSTMENT" && !p.IsVoided && p.Amount == 600m));

        Assert.Equal(0m, await tenant.Get<IPurchaseService>().GetSupplierAdvanceAsync(world.Supplier.PartyID));

        // GRN 3 must get nothing — the advance is spent.
        var grn3 = await tenant.Get<IPurchaseService>().CreateAsync(GrnDoc(world, 50, 10m), TenantData.TestUserId);
        Assert.Equal(0m, (await ReloadAsync(tenant, grn3.StockMainID)).PaidAmount);

        // And it can no longer be refunded either.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreateSupplierRefundAsync(
                Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 1m), TenantData.TestUserId));
    }

    /// <summary>The converted advance is refundable — bounded by, and consumed by, the refund.</summary>
    [Fact]
    public async Task A_converted_advance_can_be_refunded_but_not_over_refunded_or_refunded_twice()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn.StockMainID, world.Cash.AccountID, 600m), TenantData.TestUserId);
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        var payments = tenant.Get<IPaymentService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            payments.CreateSupplierRefundAsync(Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 600.01m), TenantData.TestUserId));

        await payments.CreateSupplierRefundAsync(Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 600m), TenantData.TestUserId);
        Assert.Equal(0m, await tenant.Get<IPurchaseService>().GetSupplierAdvanceAsync(world.Supplier.PartyID));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            payments.CreateSupplierRefundAsync(Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 600m), TenantData.TestUserId));

        // Cash round-trip: 600 out, 600 back.
        Assert.Equal(0m, await NetDebitAsync(tenant, world.Cash.AccountID));
    }

    [Fact]
    public async Task A_supplier_refund_into_a_non_cash_account_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn.StockMainID, world.Cash.AccountID, 600m), TenantData.TestUserId);
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        var revenue = await tenant.Db.Accounts.AsNoTracking().FirstAsync(a => a.Name == "Sales Revenue");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreateSupplierRefundAsync(
                Pay(world.Supplier.PartyID, null, revenue.AccountID, 100m), TenantData.TestUserId));
    }

    /// <summary>
    /// Once a converted advance has been refunded in cash, the original payment is settled money.
    /// Voiding it afterwards would reverse the cash payment a SECOND time — the till would show
    /// the same 1000 coming back twice, and the supplier balance would swing to "we owe them"
    /// out of nothing. The void must be refused.
    /// </summary>
    [Fact]
    public async Task Voiding_a_payment_whose_advance_was_already_refunded_is_blocked()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var payment = await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn.StockMainID, world.Cash.AccountID, 1000m), TenantData.TestUserId);
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        await tenant.Get<IPaymentService>().CreateSupplierRefundAsync(
            Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 1000m), TenantData.TestUserId);

        // The money is settled; the payment must no longer be voidable.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().VoidPaymentAsync(payment.PaymentID, "probe", TenantData.TestUserId));
    }

    /// <summary>
    /// The "supplier owes us" figure shown to users must drop back to zero once the supplier has
    /// actually refunded the advance in cash.
    /// </summary>
    [Fact]
    public async Task The_supplier_payable_to_me_figure_reflects_advance_refunds()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn.StockMainID, world.Cash.AccountID, 1000m), TenantData.TestUserId);
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        Assert.Equal(1000m, await tenant.Get<IPaymentService>().GetSupplierPayableToMeAsync(world.Supplier.PartyID));

        await tenant.Get<IPaymentService>().CreateSupplierRefundAsync(
            Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 1000m), TenantData.TestUserId);

        Assert.Equal(0m, await tenant.Get<IPaymentService>().GetSupplierPayableToMeAsync(world.Supplier.PartyID));
    }

    /// <summary>
    /// A purchase return against a fully-paid GRN leaves the supplier holding our money; that
    /// over-payment must surface as a refundable advance, refundable exactly once.
    /// </summary>
    [Fact]
    public async Task A_return_against_a_paid_GRN_creates_a_refundable_advance()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn.StockMainID, world.Cash.AccountID, 1000m), TenantData.TestUserId);

        await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 40, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        Assert.Equal(400m, await tenant.Get<IPurchaseService>().GetSupplierAdvanceAsync(world.Supplier.PartyID));

        await tenant.Get<IPaymentService>().CreateSupplierRefundAsync(
            Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 400m), TenantData.TestUserId);

        Assert.Equal(0m, await tenant.Get<IPurchaseService>().GetSupplierAdvanceAsync(world.Supplier.PartyID));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreateSupplierRefundAsync(
                Pay(world.Supplier.PartyID, null, world.Cash.AccountID, 0.01m), TenantData.TestUserId));
    }

    // ---------------------------------------------------------------- 3. Supplier credit notes

    [Fact]
    public async Task A_supplier_credit_note_posts_a_balanced_voucher_debiting_the_supplier()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var cn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);

        Assert.Equal("Open", cn.Status);
        Assert.Equal(300m, cn.BalanceAmount);
        Assert.NotNull(cn.Voucher_ID);

        var supplierAccountId = await SupplierAccountIdAsync(tenant, world.Supplier.PartyID);
        var lines = await tenant.Db.VoucherDetails.AsNoTracking()
            .Where(d => d.Voucher_ID == cn.Voucher_ID)
            .ToListAsync();

        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
        Assert.Equal(300m, lines.Where(l => l.Account_ID == supplierAccountId).Sum(l => l.DebitAmount));
    }

    [Fact]
    public async Task Applying_a_credit_note_reduces_the_GRN_balance_and_consumes_the_note()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m); // 1000
        var cn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);
        var service = tenant.Get<ISupplierCreditNoteService>();

        Assert.True(await service.ApplyToGrnAsync(cn.SupplierCreditNoteID, grn.StockMainID, 300m, TenantData.TestUserId));

        var reloadedGrn = await ReloadAsync(tenant, grn.StockMainID);
        Assert.Equal(300m, reloadedGrn.PaidAmount);
        Assert.Equal(700m, reloadedGrn.BalanceAmount);

        var reloadedCn = await tenant.Db.SupplierCreditNotes.AsNoTracking()
            .FirstAsync(c => c.SupplierCreditNoteID == cn.SupplierCreditNoteID);
        Assert.Equal("Applied", reloadedCn.Status);
        Assert.Equal(0m, reloadedCn.BalanceAmount);

        // A fully-applied note is spent — it cannot be applied again.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyToGrnAsync(cn.SupplierCreditNoteID, grn.StockMainID, 0.01m, TenantData.TestUserId));
    }

    [Fact]
    public async Task Applying_more_than_the_note_balance_or_the_GRN_outstanding_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var service = tenant.Get<ISupplierCreditNoteService>();

        // More than the note itself holds.
        var grn1 = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var cn1 = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyToGrnAsync(cn1.SupplierCreditNoteID, grn1.StockMainID, 400m, TenantData.TestUserId));

        // More than the GRN still owes (900 already paid, 100 outstanding).
        var grn2 = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, grn2.StockMainID, world.Cash.AccountID, 900m), TenantData.TestUserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyToGrnAsync(cn1.SupplierCreditNoteID, grn2.StockMainID, 200m, TenantData.TestUserId));
    }

    [Fact]
    public async Task A_credit_note_cannot_be_applied_to_another_suppliers_GRN()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var otherSupplier = await tenant.SeedSupplierAsync("Beta Wholesale");
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var cn = await CreditNoteAsync(tenant, otherSupplier.PartyID, 300m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISupplierCreditNoteService>().ApplyToGrnAsync(
                cn.SupplierCreditNoteID, grn.StockMainID, 100m, TenantData.TestUserId));
    }

    [Fact]
    public async Task A_credit_note_cannot_be_applied_to_a_voided_GRN()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var cn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);
        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISupplierCreditNoteService>().ApplyToGrnAsync(
                cn.SupplierCreditNoteID, grn.StockMainID, 100m, TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_an_open_credit_note_reverses_its_voucher_but_an_applied_one_is_blocked()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var service = tenant.Get<ISupplierCreditNoteService>();

        // Open note: void works and reverses the JV.
        var openCn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 200m);
        Assert.True(await service.VoidAsync(openCn.SupplierCreditNoteID, "keyed in error", TenantData.TestUserId));
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == openCn.Voucher_ID));

        var supplierAccountId = await SupplierAccountIdAsync(tenant, world.Supplier.PartyID);
        Assert.Equal(0m, await NetDebitAsync(tenant, supplierAccountId));

        // Applied note: void must be refused.
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var appliedCn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);
        Assert.True(await service.ApplyToGrnAsync(appliedCn.SupplierCreditNoteID, grn.StockMainID, 300m, TenantData.TestUserId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VoidAsync(appliedCn.SupplierCreditNoteID, "probe", TenantData.TestUserId));
    }

    /// <summary>
    /// A credit note applied to a GRN that is later voided must get its value back (or the void
    /// must be blocked). Otherwise the applied slice is destroyed: the note stays consumed, the
    /// GL still shows the supplier debited, and the pharmacy silently loses the credit.
    /// </summary>
    [Fact]
    public async Task Voiding_a_GRN_restores_any_credit_note_value_applied_to_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var cn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);
        Assert.True(await tenant.Get<ISupplierCreditNoteService>()
            .ApplyToGrnAsync(cn.SupplierCreditNoteID, grn.StockMainID, 300m, TenantData.TestUserId));

        var voidAttempt = await Record.ExceptionAsync(() =>
            tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "wrong goods", TenantData.TestUserId));

        if (voidAttempt is null)
        {
            var reloadedCn = await tenant.Db.SupplierCreditNotes.AsNoTracking()
                .FirstAsync(c => c.SupplierCreditNoteID == cn.SupplierCreditNoteID);
            Assert.True(reloadedCn.BalanceAmount == 300m && reloadedCn.Status == "Open",
                $"the GRN was voided but the applied credit note was not restored " +
                $"(status {reloadedCn.Status}, balance {reloadedCn.BalanceAmount}) — 300 of supplier credit is lost");
        }
    }

    /// <summary>
    /// Editing a GRN below the credit already applied to it must be blocked, exactly as edits are
    /// blocked when payments exist — otherwise PaidAmount exceeds the new total and the applied
    /// credit silently evaporates.
    /// </summary>
    [Fact]
    public async Task Editing_a_GRN_below_the_credit_note_value_applied_to_it_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m); // 1000
        var cn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);
        Assert.True(await tenant.Get<ISupplierCreditNoteService>()
            .ApplyToGrnAsync(cn.SupplierCreditNoteID, grn.StockMainID, 300m, TenantData.TestUserId));

        // Shrink the GRN to 100 — less than the 300 already applied.
        var rowVersion = await RowVersionOfAsync(tenant, grn.StockMainID);
        var attempt = await Record.ExceptionAsync(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(new StockMain
            {
                StockMainID = grn.StockMainID,
                RowVersion = rowVersion,
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 10m, CostPrice = 10m }
                }
            }, TenantData.TestUserId));

        if (attempt is null)
        {
            var reloaded = await ReloadAsync(tenant, grn.StockMainID);
            Assert.True(reloaded.PaidAmount <= reloaded.TotalAmount,
                $"the edit went through and left PaidAmount {reloaded.PaidAmount} above TotalAmount {reloaded.TotalAmount}");
        }
    }

    /// <summary>
    /// Every other posting in the system respects the period lock; applying a credit note moves
    /// a GRN's payment state and must respect it too.
    /// </summary>
    [Fact]
    public async Task Applying_a_credit_note_in_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var cn = await CreditNoteAsync(tenant, world.Supplier.PartyID, 300m);

        await tenant.CloseCurrentPeriodAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISupplierCreditNoteService>().ApplyToGrnAsync(
                cn.SupplierCreditNoteID, grn.StockMainID, 300m, TenantData.TestUserId));
    }

    // ---------------------------------------------------------------- 4. GRN edit

    [Fact]
    public async Task Editing_a_GRN_reposts_the_purchase_voucher_and_adjusts_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m); // 1000
        var originalVoucherId = (await ReloadAsync(tenant, grn.StockMainID)).Voucher_ID!.Value;

        var rowVersion = await RowVersionOfAsync(tenant, grn.StockMainID);
        // 80 units at 12 => 960.
        var edited = await tenant.Get<IPurchaseService>().UpdateAsync(new StockMain
        {
            StockMainID = grn.StockMainID,
            RowVersion = rowVersion,
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 80, UnitPrice = 12m, CostPrice = 12m }
            }
        }, TenantData.TestUserId);

        Assert.Equal(960m, edited.TotalAmount);
        Assert.Equal(80m, await tenant.StockOnHandAsync(world.Product.ProductID));

        // Old PV reversed, a fresh PV posted for the new amount.
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == originalVoucherId));
        var newVoucherId = (await ReloadAsync(tenant, grn.StockMainID)).Voucher_ID!.Value;
        Assert.NotEqual(originalVoucherId, newVoucherId);
        var newVoucher = await tenant.Db.Vouchers.AsNoTracking().FirstAsync(v => v.VoucherID == newVoucherId);
        Assert.Equal(960m, newVoucher.TotalDebit);

        // Supplier is now owed exactly the edited amount, and the books still balance.
        var supplierAccountId = await SupplierAccountIdAsync(tenant, world.Supplier.PartyID);
        Assert.Equal(-960m, await NetDebitAsync(tenant, supplierAccountId));
        var (debit, credit) = await tenant.TrialBalanceAsync();
        Assert.Equal(debit, credit);
    }

    [Fact]
    public async Task Editing_a_GRN_below_the_quantity_already_sold_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.SellAsync(world, qty: 60, unitPrice: 20m, paid: 1200m); // 40 left on hand

        // Cutting the GRN to 30 removes 70 units, but only 40 remain.
        var rowVersion = await RowVersionOfAsync(tenant, grn.StockMainID);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(new StockMain
            {
                StockMainID = grn.StockMainID,
                RowVersion = rowVersion,
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 30, UnitPrice = 10m, CostPrice = 10m }
                }
            }, TenantData.TestUserId));

        Assert.Equal(40m, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    /// <summary>
    /// A MISSING concurrency token is a failure in its own right. Treating absence as "no check to
    /// run" let any caller opt out of concurrency control by simply leaving the field off.
    /// </summary>
    [Fact]
    public async Task Editing_a_GRN_without_a_RowVersion_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(new StockMain
            {
                StockMainID = grn.StockMainID,
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 90, UnitPrice = 10m, CostPrice = 10m }
                }
            }, TenantData.TestUserId));

        // Untouched: still the original 100 units.
        Assert.Equal(100m, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    [Fact]
    public async Task Editing_a_GRN_with_a_stale_RowVersion_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(new StockMain
            {
                StockMainID = grn.StockMainID,
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                RowVersion = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 },
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 90, UnitPrice = 10m, CostPrice = 10m }
                }
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Editing_a_GRN_with_payments_or_active_returns_is_blocked()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        StockMain Edit(int id, byte[] rowVersion) => new()
        {
            StockMainID = id,
            RowVersion = rowVersion,
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 90, UnitPrice = 10m, CostPrice = 10m }
            }
        };

        // Paid GRN.
        var paidGrn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPaymentService>().CreatePaymentAsync(
            Pay(world.Supplier.PartyID, paidGrn.StockMainID, world.Cash.AccountID, 100m), TenantData.TestUserId);
        var paidGrnVersion = await RowVersionOfAsync(tenant, paidGrn.StockMainID);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(Edit(paidGrn.StockMainID, paidGrnVersion), TenantData.TestUserId));

        // GRN with a live purchase return.
        var returnedGrn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = returnedGrn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, CostPrice = 10m }
            }
        }, TenantData.TestUserId);
        var returnedGrnVersion = await RowVersionOfAsync(tenant, returnedGrn.StockMainID);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(Edit(returnedGrn.StockMainID, returnedGrnVersion), TenantData.TestUserId));
    }

    [Fact]
    public async Task Editing_a_GRN_in_a_closed_period_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await tenant.CloseCurrentPeriodAsync();

        var rowVersion = await RowVersionOfAsync(tenant, grn.StockMainID);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().UpdateAsync(new StockMain
            {
                StockMainID = grn.StockMainID,
                RowVersion = rowVersion,
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 90, UnitPrice = 10m, CostPrice = 10m }
                }
            }, TenantData.TestUserId));
    }

    // ---------------------------------------------------------------- 5. PO lifecycle edges

    [Fact]
    public async Task A_GRN_against_a_draft_or_voided_PO_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var draftPo = await tenant.Get<IPurchaseOrderService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().CreateAsync(
                GrnDoc(world, 10, 10m, poId: draftPo.StockMainID), TenantData.TestUserId));

        var voidedPo = await ApprovedPoAsync(tenant, world);
        Assert.True(await tenant.Get<IPurchaseOrderService>().ToggleStatusAsync(voidedPo.StockMainID, TenantData.TestUserId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().CreateAsync(
                GrnDoc(world, 10, 10m, poId: voidedPo.StockMainID), TenantData.TestUserId));
    }

    [Fact]
    public async Task Receiving_more_than_the_PO_quantity_is_rejected_in_aggregate()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m);
        var purchases = tenant.Get<IPurchaseService>();

        // A single over-sized GRN.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            purchases.CreateAsync(GrnDoc(world, 12, 10m, poId: po.StockMainID), TenantData.TestUserId));

        // And stacking two GRNs past the ordered quantity.
        await purchases.CreateAsync(GrnDoc(world, 6, 10m, poId: po.StockMainID), TenantData.TestUserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            purchases.CreateAsync(GrnDoc(world, 5, 10m, poId: po.StockMainID), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_approved_PO_cannot_be_edited()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseOrderService>().UpdateAsync(new StockMain
            {
                StockMainID = po.StockMainID,
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 99, UnitPrice = 10m, CostPrice = 10m }
                }
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task A_fully_received_PO_completes_and_reopens_when_its_GRN_is_voided()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var po = await ApprovedPoAsync(tenant, world, qty: 10, price: 10m);

        var grn = await tenant.Get<IPurchaseService>().CreateAsync(
            GrnDoc(world, 10, 10m, poId: po.StockMainID), TenantData.TestUserId);
        Assert.Equal("Completed", (await ReloadAsync(tenant, po.StockMainID)).Status);

        Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "damaged in transit", TenantData.TestUserId));
        Assert.Equal("Approved", (await ReloadAsync(tenant, po.StockMainID)).Status);
    }

    // ---------------------------------------------------------------- 6. Payment account gate

    /// <summary>
    /// Customer receipts refuse non-cash/bank accounts; supplier payments must too. A payment
    /// credited to a revenue account fakes income and never touches cash.
    /// </summary>
    [Fact]
    public async Task A_supplier_payment_into_a_revenue_account_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var revenue = await tenant.Db.Accounts.AsNoTracking().FirstAsync(a => a.Name == "Sales Revenue");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreatePaymentAsync(
                Pay(world.Supplier.PartyID, grn.StockMainID, revenue.AccountID, 100m), TenantData.TestUserId));
    }

    /// <summary>The same gate must hold for the payment taken while creating the GRN.</summary>
    [Fact]
    public async Task A_GRN_initial_payment_into_a_revenue_account_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var revenue = await tenant.Db.Accounts.AsNoTracking().FirstAsync(a => a.Name == "Sales Revenue");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseService>().CreateAsync(
                GrnDoc(world, 100, 10m, paid: 100m), TenantData.TestUserId, paymentAccountId: revenue.AccountID));
    }
}
