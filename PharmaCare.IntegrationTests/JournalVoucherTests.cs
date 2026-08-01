using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Application.Interfaces.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// The manual journal voucher writes straight to the general ledger with no transaction behind it,
/// so its own validation is the only thing standing between an operator and an unbalanced or
/// fabricated ledger.
/// </summary>
[Collection(Collections.Database)]
public class JournalVoucherTests
{
    private readonly DatabaseFixture _fixture;

    public JournalVoucherTests(DatabaseFixture fixture) => _fixture = fixture;

    private async Task<(int JvTypeId, int CashId, int RevenueId)> AccountsAsync(TenantScope tenant)
    {
        var jvType = await tenant.Db.VoucherTypes.FirstAsync(t => t.Code == "JV");
        var cash = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Cash in Hand");
        var revenue = await tenant.Db.Accounts.FirstAsync(a => a.Name == "Sales Revenue");
        return (jvType.VoucherTypeID, cash.AccountID, revenue.AccountID);
    }

    private static JournalVoucherDto Jv(int typeId, int debitAccount, int creditAccount, decimal debit, decimal credit)
        => new()
        {
            VoucherType_ID = typeId,
            VoucherDate = AppTime.Now,
            Narration = "Test journal",
            VoucherDetails = new List<JournalVoucherDetailDto>
            {
                new() { Account_ID = debitAccount, DebitAmount = debit, CreditAmount = 0 },
                new() { Account_ID = creditAccount, DebitAmount = 0, CreditAmount = credit }
            }
        };

    [Fact]
    public async Task A_balanced_journal_voucher_posts()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var voucher = await tenant.Get<IJournalVoucherService>()
            .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 500m, 500m), TenantData.TestUserId);

        Assert.Equal("Posted", voucher.Status);
        Assert.Equal(500m, voucher.TotalDebit);
        Assert.Equal(500m, voucher.TotalCredit);
        Assert.Equal(2, voucher.VoucherDetails.Count);
    }

    [Fact]
    public async Task An_unbalanced_journal_voucher_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IJournalVoucherService>()
                .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 500m, 400m), TenantData.TestUserId));
    }

    /// <summary>
    /// Header totals arrive from the browser. They must be recomputed from the lines, or an
    /// operator can post a voucher whose header says one thing and whose ledger says another.
    /// </summary>
    [Fact]
    public async Task Spoofed_header_totals_are_recomputed_from_the_lines()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var dto = Jv(jvType, cash, revenue, 500m, 500m);
        dto.TotalDebit = 999_999m;
        dto.TotalCredit = 999_999m;

        var voucher = await tenant.Get<IJournalVoucherService>()
            .CreateJournalVoucherAsync(dto, TenantData.TestUserId);

        Assert.Equal(500m, voucher.TotalDebit);
        Assert.Equal(500m, voucher.TotalCredit);
    }

    [Fact]
    public async Task A_zero_value_journal_voucher_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IJournalVoucherService>()
                .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 0m, 0m), TenantData.TestUserId));
    }

    [Fact]
    public async Task A_journal_voucher_beyond_the_sanity_limit_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IJournalVoucherService>()
                .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 200_000_000m, 200_000_000m), TenantData.TestUserId));
    }

    /// <summary>
    /// Sale, purchase and receipt vouchers are machine-generated. Letting an operator hand-write
    /// one would put a voucher in the ledger that no transaction backs.
    /// </summary>
    [Fact]
    public async Task Hand_writing_an_auto_generated_voucher_type_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (_, cash, revenue) = await AccountsAsync(tenant);
        var salesVoucherType = await tenant.Db.VoucherTypes.FirstAsync(t => t.Code == "SV");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            tenant.Get<IJournalVoucherService>()
                .CreateJournalVoucherAsync(Jv(salesVoucherType.VoucherTypeID, cash, revenue, 100m, 100m), TenantData.TestUserId));
    }

    /// <summary>
    /// Negative amounts balance arithmetically but are meaningless in double-entry — a negative
    /// credit is really a debit, and reports that sum a column will disagree with reports that
    /// sum a signed net.
    /// </summary>
    [Fact]
    public async Task A_journal_voucher_with_negative_amounts_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var dto = new JournalVoucherDto
        {
            VoucherType_ID = jvType,
            VoucherDate = AppTime.Now,
            Narration = "Negative line",
            VoucherDetails = new List<JournalVoucherDetailDto>
            {
                new() { Account_ID = cash, DebitAmount = 200m, CreditAmount = 0 },
                new() { Account_ID = revenue, DebitAmount = 0, CreditAmount = 300m },
                new() { Account_ID = revenue, DebitAmount = 0, CreditAmount = -100m }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IJournalVoucherService>().CreateJournalVoucherAsync(dto, TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_a_journal_voucher_posts_a_mirrored_reversal()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var voucher = await tenant.Get<IJournalVoucherService>()
            .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 500m, 500m), TenantData.TestUserId);

        Assert.True(await tenant.Get<IJournalVoucherService>()
            .VoidVoucherAsync(voucher.VoucherID, "keyed in error", TenantData.TestUserId));

        var reversal = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .FirstAsync(v => v.ReversesVoucher_ID == voucher.VoucherID);

        var cashLine = reversal.VoucherDetails.First(d => d.Account_ID == cash);
        Assert.Equal(500m, cashLine.CreditAmount); // was a debit on the original
        Assert.Equal(0m, cashLine.DebitAmount);

        var original = await tenant.Db.Vouchers.AsNoTracking().FirstAsync(v => v.VoucherID == voucher.VoucherID);
        Assert.True(original.IsReversed);
        Assert.Equal(reversal.VoucherID, original.ReversedByVoucher_ID);
    }

    [Fact]
    public async Task A_journal_voucher_cannot_be_reversed_twice()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var voucher = await tenant.Get<IJournalVoucherService>()
            .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 500m, 500m), TenantData.TestUserId);

        await tenant.Get<IJournalVoucherService>().VoidVoucherAsync(voucher.VoucherID, "first", TenantData.TestUserId);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            tenant.Get<IJournalVoucherService>().VoidVoucherAsync(voucher.VoucherID, "second", TenantData.TestUserId));
    }

    /// <summary>A reversal is itself a posted voucher and must not be reversible in turn.</summary>
    [Fact]
    public async Task A_reversal_voucher_cannot_itself_be_reversed()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var voucher = await tenant.Get<IJournalVoucherService>()
            .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 500m, 500m), TenantData.TestUserId);
        await tenant.Get<IJournalVoucherService>().VoidVoucherAsync(voucher.VoucherID, "error", TenantData.TestUserId);

        var reversal = await tenant.Db.Vouchers.FirstAsync(v => v.ReversesVoucher_ID == voucher.VoucherID);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            tenant.Get<IJournalVoucherService>().VoidVoucherAsync(reversal.VoucherID, "undo the undo", TenantData.TestUserId));
    }

    /// <summary>
    /// Voiding a journal runs inside a business transaction, so its audit rows are buffered and
    /// only written on commit. This guards the other side of that deferral: the rows must actually
    /// be flushed. Losing them would leave a reversal in the ledger with no trace of who made it.
    /// </summary>
    [Fact]
    public async Task Voiding_a_journal_voucher_still_writes_its_audit_trail()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var voucher = await tenant.Get<IJournalVoucherService>()
            .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 500m, 500m), TenantData.TestUserId);

        var before = await CountLogsAsync(tenant);
        await tenant.Get<IJournalVoucherService>()
            .VoidVoucherAsync(voucher.VoucherID, "keyed in error", TenantData.TestUserId);

        Assert.True(await CountLogsAsync(tenant) > before,
            "Deferred audit rows were not flushed after the void committed — the reversal is untraceable.");
    }

    private static async Task<int> CountLogsAsync(TenantScope tenant)
    {
        // A fresh context each time so the count is read from the database, not a cached one.
        var options = new DbContextOptionsBuilder<PharmaCare.Infrastructure.LogDbContext>()
            .UseSqlServer(tenant.Get<PharmaCare.Infrastructure.LogDbContext>().Database.GetConnectionString())
            .Options;
        using var fresh = new PharmaCare.Infrastructure.LogDbContext(options);
        return await fresh.ActivityLogs.CountAsync();
    }

    /// <summary>A voucher and its reversal must net to nothing in the ledger.</summary>
    [Fact]
    public async Task A_voucher_and_its_reversal_net_to_zero()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();
        var (jvType, cash, revenue) = await AccountsAsync(tenant);

        var voucher = await tenant.Get<IJournalVoucherService>()
            .CreateJournalVoucherAsync(Jv(jvType, cash, revenue, 500m, 500m), TenantData.TestUserId);
        await tenant.Get<IJournalVoucherService>().VoidVoucherAsync(voucher.VoucherID, "error", TenantData.TestUserId);

        var reversalId = (await tenant.Db.Vouchers.FirstAsync(v => v.ReversesVoucher_ID == voucher.VoucherID)).VoucherID;

        var cashNet = await tenant.Db.VoucherDetails
            .Where(d => d.Account_ID == cash && (d.Voucher_ID == voucher.VoucherID || d.Voucher_ID == reversalId))
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        Assert.Equal(0m, cashNet);
    }
}
