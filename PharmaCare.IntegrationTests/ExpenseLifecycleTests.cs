using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Enums;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Expenses are the one transaction with an explicit approval step, so the invariant that matters
/// is that a draft touches nothing: no voucher, no cash, no effect on profit.
/// </summary>
[Collection(Collections.Database)]
public class ExpenseLifecycleTests
{
    private readonly DatabaseFixture _fixture;

    public ExpenseLifecycleTests(DatabaseFixture fixture) => _fixture = fixture;

    private async Task<(TenantWorld World, ExpenseCategory Category)> SetupAsync(TenantScope tenant)
    {
        var world = await tenant.SeedWorldAsync();
        var category = await tenant.SeedExpenseCategoryAsync();
        return (world, category);
    }

    private static Expense NewExpense(int categoryId, int sourceAccountId, decimal amount = 500m)
        => new()
        {
            ExpenseCategory_ID = categoryId,
            SourceAccount_ID = sourceAccountId,
            Amount = amount,
            ExpenseDate = AppTime.Now,
            Description = "Monthly rent"
        };

    [Fact]
    public async Task A_new_expense_starts_as_a_draft_with_no_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        var expense = await tenant.Get<IExpenseService>()
            .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId);

        Assert.Equal(TransactionStatus.Draft, expense.Status);
        Assert.Null(expense.Voucher_ID);
    }

    [Fact]
    public async Task Approving_an_expense_posts_a_balanced_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        var expense = await tenant.Get<IExpenseService>()
            .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId);
        Assert.True(await tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId));

        var reloaded = await tenant.Db.Set<Expense>().AsNoTracking().FirstAsync(e => e.ExpenseID == expense.ExpenseID);
        Assert.Equal(TransactionStatus.Approved, reloaded.Status);
        Assert.NotNull(reloaded.Voucher_ID);

        var lines = await tenant.Db.VoucherDetails
            .Include(d => d.Account)
            .Where(d => d.Voucher_ID == reloaded.Voucher_ID)
            .ToListAsync();

        Assert.Equal(500m, lines.Sum(l => l.DebitAmount));
        Assert.Equal(500m, lines.Sum(l => l.CreditAmount));
        Assert.Equal(500m, lines.Where(l => l.Account!.Name == "Cash in Hand").Sum(l => l.CreditAmount));
    }

    [Fact]
    public async Task An_expense_cannot_be_approved_twice()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        var expense = await tenant.Get<IExpenseService>()
            .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId);
        await tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId));

        var voucherCount = await tenant.Db.Vouchers.CountAsync(v => v.Narration != null && v.Narration.Contains("Monthly rent"));
        Assert.True(voucherCount <= 1, "double approval must not post the expense to the ledger twice");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task A_non_positive_expense_is_rejected(decimal amount)
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>()
                .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID, amount), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_expense_beyond_the_sanity_limit_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>()
                .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID, 200_000_000m), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_expense_against_an_unknown_category_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>()
                .CreateAsync(NewExpense(999_999, world.Cash.AccountID), TenantData.TestUserId));
    }

    /// <summary>
    /// A category with no expense account cannot be posted anywhere, so the expense must be
    /// refused rather than guessing an account.
    /// </summary>
    [Fact]
    public async Task An_expense_whose_category_has_no_account_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var orphan = new ExpenseCategory
        {
            Name = "Unconfigured",
            DefaultExpenseAccount_ID = null,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantData.TestUserId
        };
        tenant.Db.Set<ExpenseCategory>().Add(orphan);
        await tenant.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>()
                .CreateAsync(NewExpense(orphan.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_an_approved_expense_reverses_its_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        var expense = await tenant.Get<IExpenseService>()
            .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId);
        await tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId);

        var approved = await tenant.Db.Set<Expense>().AsNoTracking().FirstAsync(e => e.ExpenseID == expense.ExpenseID);
        Assert.True(await tenant.Get<IExpenseService>().VoidAsync(expense.ExpenseID, "duplicate", TenantData.TestUserId));

        var reloaded = await tenant.Db.Set<Expense>().AsNoTracking().FirstAsync(e => e.ExpenseID == expense.ExpenseID);
        Assert.Equal(TransactionStatus.Void, reloaded.Status);
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == approved.Voucher_ID));
    }

    /// <summary>Voiding a draft has no voucher to reverse, but must still be recorded.</summary>
    [Fact]
    public async Task Voiding_a_draft_expense_posts_nothing_to_the_ledger()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        var expense = await tenant.Get<IExpenseService>()
            .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId);

        var vouchersBefore = await tenant.Db.Vouchers.CountAsync();
        await tenant.Get<IExpenseService>().VoidAsync(expense.ExpenseID, "not needed", TenantData.TestUserId);
        var vouchersAfter = await tenant.Db.Vouchers.CountAsync();

        var reloaded = await tenant.Db.Set<Expense>().AsNoTracking().FirstAsync(e => e.ExpenseID == expense.ExpenseID);
        Assert.Equal(TransactionStatus.Void, reloaded.Status);
        Assert.Equal(vouchersBefore, vouchersAfter);
    }

    [Fact]
    public async Task An_expense_cannot_be_voided_twice()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        var expense = await tenant.Get<IExpenseService>()
            .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId);
        await tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId);
        await tenant.Get<IExpenseService>().VoidAsync(expense.ExpenseID, "first", TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>().VoidAsync(expense.ExpenseID, "second", TenantData.TestUserId));
    }

    /// <summary>A voided expense must not be approvable back into the ledger.</summary>
    [Fact]
    public async Task A_voided_expense_cannot_be_approved()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (world, category) = await SetupAsync(tenant);

        var expense = await tenant.Get<IExpenseService>()
            .CreateAsync(NewExpense(category.ExpenseCategoryID, world.Cash.AccountID), TenantData.TestUserId);
        await tenant.Get<IExpenseService>().VoidAsync(expense.ExpenseID, "cancelled", TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IExpenseService>().ApproveAsync(expense.ExpenseID, TenantData.TestUserId));
    }
}
