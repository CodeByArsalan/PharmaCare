using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Money in and money out. The amounts on these screens are posted by the browser, so every one of
/// them has to be re-derived server-side from what the customer actually owes.
/// </summary>
[Collection(Collections.Database)]
public class FinanceFlowTests
{
    private readonly DatabaseFixture _fixture;

    public FinanceFlowTests(DatabaseFixture fixture) => _fixture = fixture;

    private async Task<(TenantWorld World, StockMain Sale)> CreditSaleAsync(
        TenantScope tenant, decimal qty = 10, decimal price = 20m, decimal paid = 0m)
    {
        var world = await tenant.SeedWorldAsync();
        var customer = await tenant.SeedCustomerAsync("Account Customer", creditLimit: 1_000_000m);
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 1000, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = paid,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = qty, UnitPrice = price }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        return (world with { Customer = customer }, sale);
    }

    // ---------------- Customer receipts ----------------

    [Fact]
    public async Task A_receipt_reduces_the_sale_balance_and_posts_a_balanced_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, sale) = await CreditSaleAsync(tenant);

        var receipt = await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
        {
            StockMain_ID = sale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 80m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == sale.StockMainID);
        Assert.Equal(120m, reloaded.BalanceAmount);
        Assert.Equal("Partial", reloaded.PaymentStatus);

        var lines = await tenant.Db.VoucherDetails.Where(d => d.Voucher_ID == receipt.Voucher_ID).ToListAsync();
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    [Fact]
    public async Task A_receipt_larger_than_the_outstanding_balance_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, sale) = await CreditSaleAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
            {
                StockMain_ID = sale.StockMainID,
                Account_ID = world.Cash.AccountID,
                Amount = 200.01m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, TenantData.TestUserId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task A_non_positive_receipt_is_rejected(decimal amount)
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, sale) = await CreditSaleAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
            {
                StockMain_ID = sale.StockMainID,
                Account_ID = world.Cash.AccountID,
                Amount = amount,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, TenantData.TestUserId));
    }

    /// <summary>
    /// Receipts must land in a cash or bank account. Pointing one at a revenue account would
    /// inflate income and never touch cash.
    /// </summary>
    [Fact]
    public async Task A_receipt_into_a_non_cash_account_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (_, sale) = await CreditSaleAsync(tenant);
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
            {
                StockMain_ID = sale.StockMainID,
                Account_ID = revenue.AccountID,
                Amount = 50m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Receipts_cannot_be_stacked_past_the_invoice_total()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, sale) = await CreditSaleAsync(tenant);
        var service = tenant.Get<ICustomerPaymentService>();

        Payment Receipt(decimal amount) => new()
        {
            StockMain_ID = sale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = amount,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        };

        await service.CreateReceiptAsync(Receipt(120m), TenantData.TestUserId);
        await service.CreateReceiptAsync(Receipt(80m), TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateReceiptAsync(Receipt(0.01m), TenantData.TestUserId));

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == sale.StockMainID);
        Assert.Equal(0m, reloaded.BalanceAmount);
        Assert.Equal("Paid", reloaded.PaymentStatus);
    }

    [Fact]
    public async Task A_receipt_against_a_voided_sale_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, sale) = await CreditSaleAsync(tenant);
        await tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "cancelled", TenantData.TestUserId);

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

    /// <summary>Receipts belong to sales — a GRN is a payable, not a receivable.</summary>
    [Fact]
    public async Task A_receipt_recorded_against_a_GRN_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
            {
                StockMain_ID = grn.StockMainID,
                Account_ID = world.Cash.AccountID,
                Amount = 50m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_a_receipt_restores_the_balance_and_reverses_the_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, sale) = await CreditSaleAsync(tenant);

        var receipt = await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
        {
            StockMain_ID = sale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 80m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        Assert.True(await tenant.Get<ICustomerPaymentService>()
            .VoidReceiptAsync(receipt.PaymentID, "cheque bounced", TenantData.TestUserId));

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == sale.StockMainID);
        Assert.Equal(200m, reloaded.BalanceAmount);

        var stored = await tenant.Db.Set<Payment>().AsNoTracking().FirstAsync(p => p.PaymentID == receipt.PaymentID);
        Assert.True(stored.IsVoided);
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == receipt.Voucher_ID));
    }

    [Fact]
    public async Task A_receipt_cannot_be_voided_twice()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, sale) = await CreditSaleAsync(tenant);

        var receipt = await tenant.Get<ICustomerPaymentService>().CreateReceiptAsync(new Payment
        {
            StockMain_ID = sale.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 80m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        var service = tenant.Get<ICustomerPaymentService>();
        await service.VoidReceiptAsync(receipt.PaymentID, "first", TenantData.TestUserId);

        var secondAttempt = await Record.ExceptionAsync(() =>
            service.VoidReceiptAsync(receipt.PaymentID, "second", TenantData.TestUserId));

        var reversals = await tenant.Db.Vouchers.CountAsync(v => v.ReversesVoucher_ID == receipt.Voucher_ID);
        Assert.True(secondAttempt is not null || reversals == 1,
            "a second void must either be refused or leave exactly one reversal voucher");
        Assert.Equal(1, reversals);
    }

    // ---------------- Customer refunds ----------------

    /// <summary>
    /// A refund with nothing behind it is money walking out of the door. The customer must
    /// actually be in credit.
    /// </summary>
    [Fact]
    public async Task A_refund_to_a_customer_who_is_not_in_credit_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, _) = await CreditSaleAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ICustomerPaymentService>().CreateRefundAsync(new Payment
            {
                Party_ID = world.Customer.PartyID,
                Account_ID = world.Cash.AccountID,
                Amount = 1_000_000m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, TenantData.TestUserId));
    }

    // ---------------- Supplier payments ----------------

    [Fact]
    public async Task A_supplier_payment_reduces_the_GRN_balance_and_posts_a_balanced_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var payment = await tenant.Get<IPaymentService>().CreatePaymentAsync(new Payment
        {
            Party_ID = world.Supplier.PartyID,
            StockMain_ID = grn.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 400m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID);
        Assert.Equal(600m, reloaded.BalanceAmount);

        var lines = await tenant.Db.VoucherDetails.Where(d => d.Voucher_ID == payment.Voucher_ID).ToListAsync();
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    [Fact]
    public async Task Paying_a_supplier_more_than_the_GRN_is_worth_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPaymentService>().CreatePaymentAsync(new Payment
            {
                Party_ID = world.Supplier.PartyID,
                StockMain_ID = grn.StockMainID,
                Account_ID = world.Cash.AccountID,
                Amount = 1_000.01m,
                PaymentDate = AppTime.Now,
                PaymentMethod = "Cash"
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_a_supplier_payment_restores_the_GRN_balance()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var payment = await tenant.Get<IPaymentService>().CreatePaymentAsync(new Payment
        {
            Party_ID = world.Supplier.PartyID,
            StockMain_ID = grn.StockMainID,
            Account_ID = world.Cash.AccountID,
            Amount = 400m,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        }, TenantData.TestUserId);

        Assert.True(await tenant.Get<IPaymentService>()
            .VoidPaymentAsync(payment.PaymentID, "paid the wrong supplier", TenantData.TestUserId));

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID);
        Assert.Equal(1000m, reloaded.BalanceAmount);
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == payment.Voucher_ID));
    }
}
