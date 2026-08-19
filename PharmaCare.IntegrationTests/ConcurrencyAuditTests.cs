using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Concurrency audit probes (2026-08-19). Every test here hammers ONE suspected race window with
/// genuinely parallel DI scopes (one DbContext per simulated request) and then asserts the state
/// invariant the system is supposed to keep. Races are probabilistic, so each probe loops several
/// times — a single bad final state in any iteration is a confirmed defect, not flakiness.
///
/// <para>
/// "Loser behavior" conventions: it is ACCEPTABLE for the losing request to fail with an exception
/// (DbUpdateConcurrencyException, applock timeout, unique-index violation...) as long as the final
/// state is consistent. What these probes hunt is silent corruption: doubled reversal vouchers,
/// negative stock, over-consumed credit, over-received POs, partial commits.
/// </para>
/// </summary>
[Collection(Collections.Database)]
public class ConcurrencyAuditTests
{
    private readonly DatabaseFixture _fixture;

    public ConcurrencyAuditTests(DatabaseFixture fixture) => _fixture = fixture;

    private const int UserId = TenantData.TestUserId;
    private const string Reason = "concurrency probe";

    // ------------------------------------------------------------------
    // Plumbing
    // ------------------------------------------------------------------

    /// <summary>
    /// Runs the given operations at the same time (all released by one gate) and captures each
    /// result or exception. This is the race trigger for every probe below.
    /// </summary>
    private static async Task<(object? Value, Exception? Error)[]> RaceAsync(params Func<Task<object?>>[] operations)
    {
        var gate = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = operations.Select(op => Task.Run(async () =>
        {
            await gate.Task;
            try
            {
                return (Value: await op(), Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Value: (object?)null, Error: ex);
            }
        })).ToArray();

        gate.SetResult(null);
        var results = await Task.WhenAll(tasks);
        return results.Select(r => (r.Value, r.Error)).ToArray();
    }

    private static Func<Task<object?>> Op<T>(Func<Task<T>> f) => async () => (object?)await f();

    private static int TrueCount((object? Value, Exception? Error)[] results)
        => results.Count(r => r.Error is null && r.Value is true);

    private static int OkCount((object? Value, Exception? Error)[] results)
        => results.Count(r => r.Error is null);

    /// <summary>Fresh product + customer + supplier inside an existing tenant, so one tenant can
    /// host many independent probe iterations without cross-contamination.</summary>
    private static async Task<(Product Product, Party Customer, Party Supplier)> FreshTradersAsync(
        TenantScope tenant, TenantWorld world, string tag)
    {
        var product = await tenant.SeedProductAsync(world.Category, world.SubCategory, name: $"Probe-{tag}");
        var customer = await tenant.SeedCustomerAsync($"Cust-{tag}");
        var supplier = await tenant.SeedSupplierAsync($"Supp-{tag}");
        return (product, customer, supplier);
    }

    private static StockMain PurchaseDoc(Party supplier, Product product, decimal qty, decimal cost, int? referencePoId = null)
        => new()
        {
            Party_ID = supplier.PartyID,
            ReferenceStockMain_ID = referencePoId,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = qty, UnitPrice = cost, CostPrice = cost }
            }
        };

    private static StockMain SaleDoc(Party customer, Product product, decimal qty, decimal price, decimal paid)
        => new()
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = paid,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = qty, UnitPrice = price }
            }
        };

    private static StockMain SaleReturnDoc(int saleId, Product product, decimal qty, decimal price)
        => new()
        {
            ReferenceStockMain_ID = saleId,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = qty, UnitPrice = price }
            }
        };

    private static StockMain PurchaseReturnDoc(Party supplier, int grnId, Product product, decimal qty)
        => new()
        {
            Party_ID = supplier.PartyID,
            ReferenceStockMain_ID = grnId,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = qty }
            }
        };

    /// <summary>Count of reversal vouchers pointing at ONE original voucher. More than one means
    /// the ledger unwound the same posting twice — silent corruption.</summary>
    private static Task<int> ReversalCountAsync(TenantScope tenant, int originalVoucherId)
        => tenant.Db.Vouchers.AsNoTracking().CountAsync(v => v.ReversesVoucher_ID == originalVoucherId);

    /// <summary>Every original (non-reversal) voucher of a source document must have at most one
    /// reversal. Returns the max reversal count across the document's vouchers.</summary>
    private static async Task<int> MaxReversalsPerVoucherAsync(TenantScope tenant, int stockMainId)
    {
        var originals = await tenant.Db.Vouchers.AsNoTracking()
            .Where(v => v.SourceTable == "StockMain" && v.SourceID == stockMainId && v.ReversesVoucher_ID == null)
            .Select(v => v.VoucherID)
            .ToListAsync();

        var max = 0;
        foreach (var id in originals)
        {
            max = Math.Max(max, await ReversalCountAsync(tenant, id));
        }
        return max;
    }

    private static Task<StockMain> ReloadAsync(TenantScope tenant, int stockMainId)
        => tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == stockMainId);

    // ==================================================================
    // 1. Concurrent GRN-vs-PO over-receive
    // ==================================================================

    /// <summary>
    /// TARGET #1: ValidateGrnAgainstPurchaseOrderAsync reads already-received quantities BEFORE any
    /// serialization point (the doc-no applock is only taken later, and the PO row itself is not
    /// touched when its status does not change). Two GRNs each within remaining quantity but
    /// together beyond it can therefore both pass validation and both commit.
    /// </summary>
    [Fact]
    public async Task Parallel_GRNs_cannot_jointly_over_receive_a_purchase_order()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 8; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"po{i}");

            var poService = tenant.Get<IPurchaseOrderService>();
            var po = await poService.CreateAsync(PurchaseDoc(supplier, product, 10, 10m), UserId);
            Assert.True(await poService.ApproveAsync(po.StockMainID, UserId));

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<IPurchaseService>().CreateAsync(
                    PurchaseDoc(supplier, product, 8, 10m, po.StockMainID), UserId)),
                Op(() => scopeB.Get<IPurchaseService>().CreateAsync(
                    PurchaseDoc(supplier, product, 8, 10m, po.StockMainID), UserId)));

            var received = await tenant.Db.StockMains.AsNoTracking()
                .Where(s => s.ReferenceStockMain_ID == po.StockMainID
                            && s.TransactionType!.Code == "GRN"
                            && s.Status != "Void")
                .SelectMany(s => s.StockDetails.Select(d => d.Quantity))
                .SumAsync();

            Assert.True(received <= 10,
                $"iteration {i}: PO ordered 10 but {received} units were received " +
                $"({OkCount(results)} of 2 parallel GRNs succeeded) — the PO over-receive gate was raced past.");
        }
    }

    // ==================================================================
    // 3. Double-void races (parallel window)
    // ==================================================================

    /// <summary>Two tills voiding the same sale at once: exactly one may win, and each of the
    /// sale's vouchers (invoice + receipt) must be reversed exactly once.</summary>
    [Fact]
    public async Task Parallel_double_void_of_a_sale_reverses_each_voucher_exactly_once()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, customer, supplier) = await FreshTradersAsync(tenant, world, $"sv{i}");
            await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 20, 10m), UserId);
            var sale = await tenant.Get<ISaleService>().CreateAsync(
                SaleDoc(customer, product, 5, 20m, paid: 100m), UserId, world.Cash.AccountID);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<ISaleService>().VoidAsync(sale.StockMainID, Reason, UserId)),
                Op(() => scopeB.Get<ISaleService>().VoidAsync(sale.StockMainID, Reason, UserId)));

            var maxReversals = await MaxReversalsPerVoucherAsync(tenant, sale.StockMainID);
            Assert.True(maxReversals <= 1,
                $"iteration {i}: a sale voucher was reversed {maxReversals} times by a parallel double-void.");

            Assert.Equal(20m, await tenant.StockOnHandAsync(product.ProductID));

            Assert.True(TrueCount(results) == 1,
                $"iteration {i}: {TrueCount(results)} of 2 parallel voids reported success (state was consistent).");
        }
    }

    /// <summary>Same window for a GRN (which also has the payment-conversion step in its void).</summary>
    [Fact]
    public async Task Parallel_double_void_of_a_GRN_reverses_the_purchase_voucher_exactly_once()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"gv{i}");
            var grn = await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 10, 10m), UserId);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<IPurchaseService>().VoidAsync(grn.StockMainID, Reason, UserId)),
                Op(() => scopeB.Get<IPurchaseService>().VoidAsync(grn.StockMainID, Reason, UserId)));

            var maxReversals = await MaxReversalsPerVoucherAsync(tenant, grn.StockMainID);
            Assert.True(maxReversals <= 1,
                $"iteration {i}: the purchase voucher was reversed {maxReversals} times by a parallel double-void.");

            Assert.Equal(0m, await tenant.StockOnHandAsync(product.ProductID));

            Assert.True(TrueCount(results) == 1,
                $"iteration {i}: {TrueCount(results)} of 2 parallel GRN voids reported success (state was consistent).");
        }
    }

    /// <summary>
    /// TARGET #2 (part 1): PurchaseReturnService.VoidAsync runs with NO wrapping transaction.
    /// The parallel double-void must still end with one reversal and the stock restored once.
    /// </summary>
    [Fact]
    public async Task Parallel_double_void_of_a_purchase_return_restores_stock_exactly_once()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"prv{i}");
            var grn = await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 10, 10m), UserId);
            var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(
                PurchaseReturnDoc(supplier, grn.StockMainID, product, 4), UserId);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<IPurchaseReturnService>().VoidAsync(ret.StockMainID, Reason, UserId)),
                Op(() => scopeB.Get<IPurchaseReturnService>().VoidAsync(ret.StockMainID, Reason, UserId)));

            var maxReversals = await MaxReversalsPerVoucherAsync(tenant, ret.StockMainID);
            Assert.True(maxReversals <= 1,
                $"iteration {i}: the purchase-return voucher was reversed {maxReversals} times.");

            // Void of the return restores the returned 4 units: stock must be exactly 10, not 14.
            Assert.Equal(10m, await tenant.StockOnHandAsync(product.ProductID));

            Assert.True(TrueCount(results) == 1,
                $"iteration {i}: {TrueCount(results)} of 2 parallel return voids reported success (state was consistent).");
        }
    }

    /// <summary>Two parallel voids of one customer receipt: the sale's paid amount must fall back
    /// to zero exactly once, and the receipt voucher must be reversed exactly once.</summary>
    [Fact]
    public async Task Parallel_double_void_of_a_customer_receipt_unwinds_it_exactly_once()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, customer, supplier) = await FreshTradersAsync(tenant, world, $"rc{i}");
            await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 10, 10m), UserId);
            var sale = await tenant.Get<ISaleService>().CreateAsync(
                SaleDoc(customer, product, 10, 20m, paid: 0m), UserId, world.Cash.AccountID);

            var receipt = await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
            {
                StockMain_ID = sale.StockMainID,
                Party_ID = customer.PartyID,
                Account_ID = world.Cash.AccountID,
                Amount = 200m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, UserId);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<ICustomerPaymentService>().VoidReceiptAsync(receipt.PaymentID, Reason, UserId)),
                Op(() => scopeB.Get<ICustomerPaymentService>().VoidReceiptAsync(receipt.PaymentID, Reason, UserId)));

            var reloaded = await ReloadAsync(tenant, sale.StockMainID);
            Assert.True(reloaded.PaidAmount == 0m,
                $"iteration {i}: sale PaidAmount is {reloaded.PaidAmount} after the receipt was double-voided.");

            var receiptRow = await tenant.Db.Payments.AsNoTracking().FirstAsync(p => p.PaymentID == receipt.PaymentID);
            if (receiptRow.Voucher_ID.HasValue)
            {
                var reversals = await ReversalCountAsync(tenant, receiptRow.Voucher_ID.Value);
                Assert.True(reversals <= 1,
                    $"iteration {i}: the receipt voucher was reversed {reversals} times.");
            }

            Assert.True(TrueCount(results) == 1,
                $"iteration {i}: {TrueCount(results)} of 2 parallel receipt voids reported success (state was consistent).");
        }
    }

    /// <summary>
    /// Two parallel voids of one approved expense. The Expense entity carries no concurrency token
    /// and the expense reversal is minted with a FRESH JV number (so the unique voucher-number
    /// index cannot catch a duplicate). If both voids slip past each other, the expense voucher is
    /// reversed twice and the expense account is silently understated.
    /// </summary>
    [Fact]
    public async Task Parallel_double_void_of_an_expense_reverses_its_voucher_exactly_once()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var category = await tenant.SeedExpenseCategoryAsync("Probe Utilities");

        for (var i = 0; i < 5; i++)
        {
            var expenseService = tenant.Get<IExpenseService>();
            var expense = await expenseService.CreateAsync(new Expense
            {
                ExpenseCategory_ID = category.ExpenseCategoryID,
                SourceAccount_ID = world.Cash.AccountID,
                Amount = 500m,
                ExpenseDate = AppTime.Now,
                Description = $"probe {i}"
            }, UserId);
            Assert.True(await expenseService.ApproveAsync(expense.ExpenseID, UserId));

            var approved = await tenant.Db.Expenses.AsNoTracking().FirstAsync(e => e.ExpenseID == expense.ExpenseID);
            Assert.NotNull(approved.Voucher_ID);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<IExpenseService>().VoidAsync(expense.ExpenseID, Reason, UserId)),
                Op(() => scopeB.Get<IExpenseService>().VoidAsync(expense.ExpenseID, Reason, UserId)));

            // Count reversals by NARRATION, not by ReversesVoucher_ID: the reversal link is a
            // one-to-one FK, so when a second reversal is inserted EF's relationship fixup silently
            // STEALS the link from the first one (nulls it) — a link-based count hides the duplicate.
            var originalVoucherNo = (await tenant.Db.Vouchers.AsNoTracking()
                .FirstAsync(v => v.VoucherID == approved.Voucher_ID!.Value)).VoucherNo;
            var reversals = await tenant.Db.Vouchers.AsNoTracking()
                .CountAsync(v => v.Narration != null && v.Narration.StartsWith($"REVERSAL of {originalVoucherNo}."));
            Assert.True(reversals <= 1,
                $"iteration {i}: the expense voucher was reversed {reversals} times by a parallel double-void — " +
                "the cash and expense accounts are now misstated by one full expense amount.");

            Assert.True(TrueCount(results) == 1,
                $"iteration {i}: {TrueCount(results)} of 2 parallel expense voids reported success (state was consistent).");
        }
    }

    /// <summary>
    /// Two parallel voids of one manual journal voucher. VoidVoucherAsync loads the voucher BEFORE
    /// opening its transaction, the Voucher entity has no concurrency token, and each reversal gets
    /// a fresh JV number — so nothing stops both requests posting a reversal each.
    /// </summary>
    [Fact]
    public async Task Parallel_double_void_of_a_journal_voucher_posts_exactly_one_reversal()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var jvType = await tenant.Db.VoucherTypes.FirstAsync(t => t.Code == "JV");
        var cash = await tenant.CashAccountAsync();
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");

        for (var i = 0; i < 5; i++)
        {
            var jv = await tenant.Get<IJournalVoucherService>().CreateJournalVoucherAsync(new JournalVoucherDto
            {
                VoucherType_ID = jvType.VoucherTypeID,
                VoucherDate = AppTime.Now,
                Narration = $"double-void probe {i}",
                VoucherDetails = new List<JournalVoucherDetailDto>
                {
                    new() { Account_ID = cash.AccountID, DebitAmount = 500m, CreditAmount = 0 },
                    new() { Account_ID = revenue.AccountID, DebitAmount = 0, CreditAmount = 500m }
                }
            }, UserId);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<IJournalVoucherService>().VoidVoucherAsync(jv.VoucherID, Reason, UserId)),
                Op(() => scopeB.Get<IJournalVoucherService>().VoidVoucherAsync(jv.VoucherID, Reason, UserId)));

            // Count reversals by NARRATION, not by ReversesVoucher_ID: the reversal link is a
            // one-to-one FK, so when a second reversal is inserted EF's relationship fixup silently
            // STEALS the link from the first one (nulls it) — a link-based count hides the duplicate.
            var reversalPrefix = $"Reversal of {jv.VoucherNo} -";
            var reversals = await tenant.Db.Vouchers.AsNoTracking()
                .CountAsync(v => v.Narration != null && v.Narration.StartsWith(reversalPrefix));
            Assert.True(reversals <= 1,
                $"iteration {i}: the JV was reversed {reversals} times by a parallel double-void — " +
                "every account it touched now carries a one-sided residue.");

            // Per-account residue across the JV and its reversal(s) must net to zero.
            var lines = await tenant.Db.VoucherDetails.AsNoTracking()
                .Where(d => d.Voucher_ID == jv.VoucherID
                            || (d.Voucher!.Narration != null && d.Voucher.Narration.StartsWith(reversalPrefix)))
                .ToListAsync();
            var cashResidue = lines.Where(l => l.Account_ID == cash.AccountID).Sum(l => l.DebitAmount - l.CreditAmount);
            Assert.True(cashResidue == 0m,
                $"iteration {i}: cash account residue after JV double-void is {cashResidue}.");

            Assert.True(TrueCount(results) == 1,
                $"iteration {i}: {TrueCount(results)} of 2 parallel JV voids reported success (state was consistent).");
        }
    }

    /// <summary>
    /// Double-void of a supplier payment AFTER its GRN was voided (the payment is then a detached
    /// on-account advance with no StockMain row to serialize on). The reversal voucher's number is
    /// derived (REV-original), so the unique voucher-number index is the last line of defense.
    /// A doubled reversal would let the same voided money be refunded twice.
    /// </summary>
    [Fact]
    public async Task Parallel_double_void_of_a_detached_supplier_advance_payment_reverses_once()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"adv{i}");
            var grn = await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 100, 10m), UserId);

            var payment = await tenant.Get<IPaymentService>().CreatePaymentAsync(new Payment
            {
                Party_ID = supplier.PartyID,
                StockMain_ID = grn.StockMainID,
                Account_ID = world.Cash.AccountID,
                Amount = 1000m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, UserId);

            // Void the GRN: the payment becomes a supplier-level advance (StockMain_ID = null).
            Assert.True(await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, Reason, UserId));

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => scopeA.Get<IPaymentService>().VoidPaymentAsync(payment.PaymentID, Reason, UserId)),
                Op(() => scopeB.Get<IPaymentService>().VoidPaymentAsync(payment.PaymentID, Reason, UserId)));

            var paymentRow = await tenant.Db.Payments.AsNoTracking().FirstAsync(p => p.PaymentID == payment.PaymentID);
            if (paymentRow.Voucher_ID.HasValue)
            {
                var reversals = await ReversalCountAsync(tenant, paymentRow.Voucher_ID.Value);
                Assert.True(reversals <= 1,
                    $"iteration {i}: the payment voucher was reversed {reversals} times — cash is overstated.");
            }

            var advance = await tenant.Get<IPurchaseService>().GetSupplierAdvanceAsync(supplier.PartyID);
            Assert.True(advance == 0m,
                $"iteration {i}: supplier still shows an advance of {advance} after the payment was voided.");

            Assert.True(TrueCount(results) == 1,
                $"iteration {i}: {TrueCount(results)} of 2 parallel payment voids reported success (state was consistent).");
        }
    }

    // ==================================================================
    // 2. PurchaseReturn void atomicity vs concurrent supplier payment
    // ==================================================================

    /// <summary>
    /// TARGET #2 (part 2): PurchaseReturnService.VoidAsync issues TWO independent SaveChanges with
    /// no wrapping transaction (void + reversal first, GRN balance recalc second). A supplier
    /// payment against the same GRN racing between the two can make the second save fail after the
    /// first has already committed: the void then throws while the return IS voided, and the GRN's
    /// stored balance no longer matches what the books say.
    /// </summary>
    [Fact]
    public async Task Voiding_a_purchase_return_while_paying_its_GRN_stays_atomic_and_consistent()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 8; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"pv{i}");
            var grn = await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 10, 100m), UserId);
            var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(
                PurchaseReturnDoc(supplier, grn.StockMainID, product, 4), UserId);

            // GRN total 1000, active return 400 => outstanding 600.
            using var payScope = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var voidScope = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => payScope.Get<IPaymentService>().CreatePaymentAsync(new Payment
                {
                    Party_ID = supplier.PartyID,
                    StockMain_ID = grn.StockMainID,
                    Account_ID = world.Cash.AccountID,
                    Amount = 600m,
                    PaymentDate = AppTime.Now,
                    PaymentMethod = "Cash"
                }, UserId)),
                Op(() => voidScope.Get<IPurchaseReturnService>().VoidAsync(ret.StockMainID, Reason, UserId)));

            var voidOutcome = results[1];
            var returnRow = await ReloadAsync(tenant, ret.StockMainID);

            // Atomicity: a void that reports failure must not have half-committed.
            if (voidOutcome.Error is not null)
            {
                Assert.True(returnRow.Status != "Void",
                    $"iteration {i}: VoidAsync threw '{voidOutcome.Error.GetType().Name}' but the return WAS voided — " +
                    "the void committed its first SaveChanges and lost its second (no wrapping transaction).");
            }

            // Denormalized GRN balance must agree with a recomputation from the rows themselves.
            var grnRow = await ReloadAsync(tenant, grn.StockMainID);
            var activePayments = await tenant.Db.Payments.AsNoTracking()
                .Where(p => p.StockMain_ID == grn.StockMainID && !p.IsVoided && p.PaymentType == "PAYMENT")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var activeReturns = await tenant.Db.StockMains.AsNoTracking()
                .Where(s => s.ReferenceStockMain_ID == grn.StockMainID
                            && s.TransactionType!.Code == "PRTN"
                            && s.Status != "Void")
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;

            var expectedBalance = Math.Max(0, grnRow.TotalAmount - activeReturns - activePayments);

            Assert.True(grnRow.PaidAmount == activePayments,
                $"iteration {i}: GRN PaidAmount {grnRow.PaidAmount} but active payments total {activePayments}.");
            Assert.True(grnRow.BalanceAmount == expectedBalance,
                $"iteration {i}: GRN BalanceAmount {grnRow.BalanceAmount} but recomputed outstanding is {expectedBalance} " +
                $"(return voided: {returnRow.Status == "Void"}, paid: {activePayments}).");
        }
    }

    // ==================================================================
    // 4. Credit-note races
    // ==================================================================

    private async Task<(Product Product, Party Customer, CreditNote Note)> SeedCreditNoteAsync(
        TenantScope tenant, TenantWorld world, string tag)
    {
        var product = await tenant.SeedProductAsync(world.Category, world.SubCategory, name: $"CN-{tag}");
        var customer = await tenant.SeedCustomerAsync($"CN-Cust-{tag}");
        var supplier = await tenant.SeedSupplierAsync($"CN-Supp-{tag}");
        await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 40, 10m), UserId);

        // Fully paid sale (200) + return of 5 units => credit note of 100.
        var sale = await tenant.Get<ISaleService>().CreateAsync(
            SaleDoc(customer, product, 10, 20m, paid: 200m), UserId, world.Cash.AccountID);
        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            SaleReturnDoc(sale.StockMainID, product, 5, 20m), UserId);

        var note = await tenant.Db.CreditNotes.AsNoTracking()
            .SingleAsync(c => c.SourceStockMain_ID == ret.StockMainID);
        Assert.Equal(100m, note.TotalAmount);
        return (product, customer, note);
    }

    /// <summary>Applying the same credit note to two different sales in parallel must never
    /// consume more than the note's total.</summary>
    [Fact]
    public async Task Applying_one_credit_note_to_two_sales_in_parallel_cannot_overspend_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, customer, note) = await SeedCreditNoteAsync(tenant, world, $"ap{i}");

            // Two credit sales, each with an outstanding balance of exactly 100.
            var saleA = await tenant.Get<ISaleService>().CreateAsync(
                SaleDoc(customer, product, 5, 20m, paid: 0m), UserId, world.Cash.AccountID);
            var saleB = await tenant.Get<ISaleService>().CreateAsync(
                SaleDoc(customer, product, 5, 20m, paid: 0m), UserId, world.Cash.AccountID);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            await RaceAsync(
                Op<object?>(async () => { await scopeA.Get<ICustomerPaymentService>().ApplyCreditNoteAsync(note.CreditNoteID, saleA.StockMainID, 100m, UserId); return null; }),
                Op<object?>(async () => { await scopeB.Get<ICustomerPaymentService>().ApplyCreditNoteAsync(note.CreditNoteID, saleB.StockMainID, 100m, UserId); return null; }));

            var noteRow = await tenant.Db.CreditNotes.AsNoTracking().FirstAsync(c => c.CreditNoteID == note.CreditNoteID);
            Assert.True(noteRow.AppliedAmount <= noteRow.TotalAmount,
                $"iteration {i}: note applied {noteRow.AppliedAmount} of {noteRow.TotalAmount}.");

            var allocated = await tenant.Db.PaymentAllocations.AsNoTracking()
                .Where(a => a.CreditNote_ID == note.CreditNoteID)
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;
            Assert.True(allocated <= note.TotalAmount,
                $"iteration {i}: {allocated} was allocated from a {note.TotalAmount} credit note.");

            var paidA = (await ReloadAsync(tenant, saleA.StockMainID)).PaidAmount;
            var paidB = (await ReloadAsync(tenant, saleB.StockMainID)).PaidAmount;
            Assert.True(paidA + paidB <= note.TotalAmount,
                $"iteration {i}: the two sales absorbed {paidA + paidB} from a {note.TotalAmount} note.");
        }
    }

    /// <summary>Refunding a note while simultaneously applying it: total consumption must not
    /// exceed the note.</summary>
    [Fact]
    public async Task Refunding_and_applying_one_credit_note_in_parallel_cannot_overspend_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, customer, note) = await SeedCreditNoteAsync(tenant, world, $"ra{i}");
            var sale = await tenant.Get<ISaleService>().CreateAsync(
                SaleDoc(customer, product, 5, 20m, paid: 0m), UserId, world.Cash.AccountID);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            await RaceAsync(
                Op(() => scopeA.Get<ICustomerPaymentService>().CreateRefundAsync(new Payment
                {
                    Party_ID = customer.PartyID,
                    Account_ID = world.Cash.AccountID,
                    Amount = 100m,
                    PaymentDate = AppTime.Now,
                    PaymentMethod = "Cash"
                }, UserId)),
                Op<object?>(async () => { await scopeB.Get<ICustomerPaymentService>().ApplyCreditNoteAsync(note.CreditNoteID, sale.StockMainID, 100m, UserId); return null; }));

            var noteRow = await tenant.Db.CreditNotes.AsNoTracking().FirstAsync(c => c.CreditNoteID == note.CreditNoteID);
            Assert.True(noteRow.AppliedAmount <= noteRow.TotalAmount,
                $"iteration {i}: note applied {noteRow.AppliedAmount} of {noteRow.TotalAmount}.");

            var refunds = await tenant.Db.Payments.AsNoTracking()
                .Where(p => p.Party_ID == customer.PartyID && p.PaymentType == "REFUND" && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var applied = await tenant.Db.PaymentAllocations.AsNoTracking()
                .Where(a => a.CreditNote_ID == note.CreditNoteID && a.SourceType == "CreditNote")
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;

            Assert.True(refunds + applied <= note.TotalAmount,
                $"iteration {i}: refund {refunds} + application {applied} consumed more than the {note.TotalAmount} note.");
        }
    }

    /// <summary>Two parallel cash refunds of the same note: at most one may pay out.</summary>
    [Fact]
    public async Task Two_parallel_refunds_of_one_credit_note_cannot_both_pay_out()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (_, customer, note) = await SeedCreditNoteAsync(tenant, world, $"rr{i}");

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            Payment Refund() => new()
            {
                Party_ID = customer.PartyID,
                Account_ID = world.Cash.AccountID,
                Amount = 100m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            };

            var results = await RaceAsync(
                Op(() => scopeA.Get<ICustomerPaymentService>().CreateRefundAsync(Refund(), UserId)),
                Op(() => scopeB.Get<ICustomerPaymentService>().CreateRefundAsync(Refund(), UserId)));

            var refunds = await tenant.Db.Payments.AsNoTracking()
                .Where(p => p.Party_ID == customer.PartyID && p.PaymentType == "REFUND" && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            Assert.True(refunds <= note.TotalAmount,
                $"iteration {i}: {refunds} in cash refunds were paid out against a {note.TotalAmount} credit note " +
                $"({OkCount(results)} of 2 refunds succeeded).");
        }
    }

    // ==================================================================
    // 5 & 6. Over-pay races (receipts and supplier payments)
    // ==================================================================

    /// <summary>Two receipts, each within the sale's outstanding balance but jointly beyond it.</summary>
    [Fact]
    public async Task Parallel_receipts_cannot_jointly_overpay_a_sale()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, customer, supplier) = await FreshTradersAsync(tenant, world, $"op{i}");
            await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 10, 10m), UserId);
            var sale = await tenant.Get<ISaleService>().CreateAsync(
                SaleDoc(customer, product, 10, 20m, paid: 0m), UserId, world.Cash.AccountID);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            Payment Receipt() => new()
            {
                StockMain_ID = sale.StockMainID,
                Party_ID = customer.PartyID,
                Account_ID = world.Cash.AccountID,
                Amount = 150m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            };

            var results = await RaceAsync(
                Op(() => scopeA.Get<ICustomerPaymentService>().CreateReceiptAsync(Receipt(), UserId)),
                Op(() => scopeB.Get<ICustomerPaymentService>().CreateReceiptAsync(Receipt(), UserId)));

            var reloaded = await ReloadAsync(tenant, sale.StockMainID);
            var activeReceipts = await tenant.Db.Payments.AsNoTracking()
                .Where(p => p.StockMain_ID == sale.StockMainID && p.PaymentType == "RECEIPT" && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            Assert.True(activeReceipts <= reloaded.TotalAmount,
                $"iteration {i}: {activeReceipts} was receipted against a {reloaded.TotalAmount} sale " +
                $"({OkCount(results)} of 2 parallel receipts succeeded).");
            Assert.True(reloaded.PaidAmount == activeReceipts,
                $"iteration {i}: sale PaidAmount {reloaded.PaidAmount} disagrees with receipts total {activeReceipts}.");
        }
    }

    /// <summary>Two supplier payments, each within the GRN's balance but jointly beyond it.</summary>
    [Fact]
    public async Task Parallel_supplier_payments_cannot_jointly_overpay_a_GRN()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"sp{i}");
            var grn = await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 100, 10m), UserId);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            Payment Pay() => new()
            {
                Party_ID = supplier.PartyID,
                StockMain_ID = grn.StockMainID,
                Account_ID = world.Cash.AccountID,
                Amount = 700m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            };

            var results = await RaceAsync(
                Op(() => scopeA.Get<IPaymentService>().CreatePaymentAsync(Pay(), UserId)),
                Op(() => scopeB.Get<IPaymentService>().CreatePaymentAsync(Pay(), UserId)));

            var reloaded = await ReloadAsync(tenant, grn.StockMainID);
            var activePayments = await tenant.Db.Payments.AsNoTracking()
                .Where(p => p.StockMain_ID == grn.StockMainID && p.PaymentType == "PAYMENT" && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            Assert.True(activePayments <= reloaded.TotalAmount,
                $"iteration {i}: {activePayments} was paid against a {reloaded.TotalAmount} GRN " +
                $"({OkCount(results)} of 2 parallel payments succeeded).");
            Assert.True(reloaded.PaidAmount == activePayments,
                $"iteration {i}: GRN PaidAmount {reloaded.PaidAmount} disagrees with payments total {activePayments}.");
        }
    }

    /// <summary>Paying a GRN while another user voids it: a voided GRN must not keep live payments
    /// attached to it, whichever order the two commits land in.</summary>
    [Fact]
    public async Task Paying_a_GRN_while_it_is_being_voided_leaves_no_live_payment_on_a_void_GRN()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 6; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"pv2{i}");
            var grn = await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 100, 10m), UserId);

            using var payScope = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var voidScope = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => payScope.Get<IPaymentService>().CreatePaymentAsync(new Payment
                {
                    Party_ID = supplier.PartyID,
                    StockMain_ID = grn.StockMainID,
                    Account_ID = world.Cash.AccountID,
                    Amount = 600m,
                    PaymentDate = AppTime.Now,
                    PaymentMethod = "Cash"
                }, UserId)),
                Op(() => voidScope.Get<IPurchaseService>().VoidAsync(grn.StockMainID, Reason, UserId)));

            var grnRow = await ReloadAsync(tenant, grn.StockMainID);
            if (grnRow.Status == "Void")
            {
                var livePaymentsOnVoidGrn = await tenant.Db.Payments.AsNoTracking()
                    .CountAsync(p => p.StockMain_ID == grn.StockMainID && !p.IsVoided);
                Assert.True(livePaymentsOnVoidGrn == 0,
                    $"iteration {i}: the GRN is Void but {livePaymentsOnVoidGrn} live payment(s) still reference it — " +
                    "money attached to a document that no longer exists in the books.");

                Assert.Equal(0m, await tenant.StockOnHandAsync(product.ProductID));
            }
            else
            {
                var activePayments = await tenant.Db.Payments.AsNoTracking()
                    .Where(p => p.StockMain_ID == grn.StockMainID && p.PaymentType == "PAYMENT" && !p.IsVoided)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                Assert.True(grnRow.PaidAmount == activePayments,
                    $"iteration {i}: GRN PaidAmount {grnRow.PaidAmount} disagrees with live payments {activePayments}.");
            }
        }
    }

    // ==================================================================
    // 7. Sale-vs-return parallel window
    // ==================================================================

    /// <summary>
    /// Voiding a sale while a return against it is being created. The sequential guards exist in
    /// both directions; the probe hits the window where each side's guard read happens before the
    /// other side's commit. The forbidden end state is a VOIDED sale with an ACTIVE return — that
    /// return's stock re-entry is phantom stock.
    /// </summary>
    [Fact]
    public async Task Voiding_a_sale_while_a_return_is_created_never_leaves_a_live_return_on_a_void_sale()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 6; i++)
        {
            var (product, customer, supplier) = await FreshTradersAsync(tenant, world, $"vr{i}");
            await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 20, 10m), UserId);
            var sale = await tenant.Get<ISaleService>().CreateAsync(
                SaleDoc(customer, product, 10, 20m, paid: 200m), UserId, world.Cash.AccountID);

            using var voidScope = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var returnScope = await _fixture.ScopeForAsync(tenant.PharmacyId);

            await RaceAsync(
                Op(() => voidScope.Get<ISaleService>().VoidAsync(sale.StockMainID, Reason, UserId)),
                Op(() => returnScope.Get<ISaleReturnService>().CreateAsync(
                    SaleReturnDoc(sale.StockMainID, product, 4, 20m), UserId)));

            var saleRow = await ReloadAsync(tenant, sale.StockMainID);
            var activeReturns = await tenant.Db.StockMains.AsNoTracking()
                .CountAsync(s => s.ReferenceStockMain_ID == sale.StockMainID
                                 && s.TransactionType!.Code == "SRTN"
                                 && s.Status != "Void");

            Assert.False(saleRow.Status == "Void" && activeReturns > 0,
                $"iteration {i}: the sale is Void yet {activeReturns} active return(s) reference it — phantom stock.");

            var stock = await tenant.StockOnHandAsync(product.ProductID);
            Assert.True(stock <= 20m, $"iteration {i}: stock is {stock} from only 20 units ever received.");
        }
    }

    // ==================================================================
    // 8. Concurrent stock adjustments
    // ==================================================================

    /// <summary>Two write-offs of one product, each within stock but jointly beyond it.</summary>
    [Fact]
    public async Task Parallel_write_offs_cannot_jointly_drive_stock_negative()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        for (var i = 0; i < 5; i++)
        {
            var (product, _, supplier) = await FreshTradersAsync(tenant, world, $"wo{i}");
            await tenant.Get<IPurchaseService>().CreateAsync(PurchaseDoc(supplier, product, 10, 10m), UserId);

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            StockMain WriteOff() => new()
            {
                TransactionDate = AppTime.Now,
                AdjustmentType = "Write-off",
                AdjustmentReason = "Damaged",
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = product.ProductID, Quantity = 8 }
                }
            };

            var results = await RaceAsync(
                Op(() => scopeA.Get<IStockAdjustmentService>().CreateAsync(WriteOff(), UserId)),
                Op(() => scopeB.Get<IStockAdjustmentService>().CreateAsync(WriteOff(), UserId)));

            var stock = await tenant.StockOnHandAsync(product.ProductID);
            Assert.True(stock >= 0, $"iteration {i}: parallel write-offs drove stock to {stock}.");
            Assert.Equal(10m - (OkCount(results) * 8m), stock);
        }
    }

    // ==================================================================
    // 9. Period close race
    // ==================================================================

    /// <summary>
    /// TARGET #9: every transaction service validates the financial period BEFORE opening its
    /// transaction and never re-checks inside it. A period close that commits in that window lets
    /// a sale post into the just-closed period. Detection: the sale succeeded and its voucher was
    /// stamped after the period's ClosedAt.
    /// </summary>
    [Fact]
    public async Task A_sale_cannot_post_into_a_period_that_closed_while_it_was_in_flight()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 1000, unitCost: 10m);

        var period = await tenant.Db.FinancialPeriods.AsNoTracking()
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);

        var violations = new List<string>();

        for (var i = 0; i < 8; i++)
        {
            // Make sure the period is open before each attempt.
            await tenant.Get<IFinancialPeriodService>().OpenPeriodAsync(period.PeriodID, UserId);

            using var saleScope = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var closeScope = await _fixture.ScopeForAsync(tenant.PharmacyId);

            var results = await RaceAsync(
                Op(() => saleScope.Get<ISaleService>().CreateAsync(
                    SaleDoc(world.Customer, world.Product, 2, 20m, paid: 40m), UserId, world.Cash.AccountID)),
                Op<bool>(async () =>
                {
                    // Let the sale pass its period check first, then close while it is mid-flight.
                    await Task.Delay(15);
                    return await closeScope.Get<IFinancialPeriodService>()
                        .ClosePeriodAsync(period.PeriodID, "close race probe", UserId);
                }));

            var closedAt = (await tenant.Db.FinancialPeriods.AsNoTracking()
                .FirstAsync(p => p.PeriodID == period.PeriodID)).ClosedAt;

            if (results[0].Error is null && results[0].Value is StockMain postedSale && closedAt.HasValue)
            {
                var voucherStamps = await tenant.Db.Vouchers.AsNoTracking()
                    .Where(v => v.SourceTable == "StockMain" && v.SourceID == postedSale.StockMainID)
                    .Select(v => v.CreatedAt)
                    .ToListAsync();

                if (voucherStamps.Any(t => t > closedAt.Value))
                {
                    violations.Add(
                        $"iteration {i}: sale {postedSale.TransactionNo} posted its voucher at " +
                        $"{voucherStamps.Max():HH:mm:ss.fff} — AFTER the period was closed at {closedAt:HH:mm:ss.fff}.");
                }
            }
        }

        // Leave the period open so later tests in this tenant are unaffected (defensive).
        await tenant.Get<IFinancialPeriodService>().OpenPeriodAsync(period.PeriodID, UserId);

        Assert.True(violations.Count == 0,
            "sales posted into a closed period:\n" + string.Join("\n", violations));
    }

    // ==================================================================
    // 10. Concurrent tenant provisioning
    // ==================================================================

    /// <summary>Two ProvisionAsync calls with the same PharmacyCode: exactly one may win.</summary>
    [Fact]
    public async Task Parallel_provisioning_with_the_same_pharmacy_code_creates_exactly_one_tenant()
    {
        using var tenant = await _fixture.NewTenantAsync();

        for (var i = 0; i < 3; i++)
        {
            var code = $"RACE{Guid.NewGuid().ToString("N")[..8]}";

            using var scopeA = await _fixture.ScopeForAsync(tenant.PharmacyId);
            using var scopeB = await _fixture.ScopeForAsync(tenant.PharmacyId);

            ProvisionPharmacyRequest Request(string who) => new()
            {
                PharmacyName = $"Race Pharmacy {code}-{who}",
                PharmacyCode = code,
                AdminEmail = $"admin-{code}-{who}@test.local",
                AdminPassword = "TestPass123",
                AdminFullName = "Race Admin"
            };

            var results = await RaceAsync(
                Op(() => scopeA.Get<ITenantProvisioningService>().ProvisionAsync(Request("a"), 0)),
                Op(() => scopeB.Get<ITenantProvisioningService>().ProvisionAsync(Request("b"), 0)));

            var successes = results.Count(r => r.Error is null && r.Value is ProvisionResult { Success: true });
            var pharmacies = await tenant.Db.Pharmacies.AsNoTracking().CountAsync(p => p.Code == code);

            Assert.True(pharmacies <= 1,
                $"iteration {i}: {pharmacies} pharmacies share the code '{code}'.");
            Assert.True(successes == 1,
                $"iteration {i}: {successes} of 2 parallel provisionings reported success for code '{code}'.");
        }
    }
}
