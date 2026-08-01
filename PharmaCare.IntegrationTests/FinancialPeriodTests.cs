using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Financial periods are what the lock is applied to, so their own boundaries have to be sound.
/// Overlapping periods make "is this date locked?" ambiguous: the same date can sit in one closed
/// period and one open one, and which answer you get depends on row order.
/// </summary>
[Collection(Collections.Database)]
public class FinancialPeriodTests
{
    private readonly DatabaseFixture _fixture;

    public FinancialPeriodTests(DatabaseFixture fixture) => _fixture = fixture;

    private static FinancialPeriod Period(string name, DateTime start, DateTime end)
        => new() { Name = name, StartDate = start, EndDate = end };

    private static int Year => AppTime.Now.Year;

    /// <summary>Provisioning already creates FY&lt;year&gt;, so a clashing period must be refused.</summary>
    [Fact]
    public async Task A_period_overlapping_an_existing_one_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IFinancialPeriodService>().CreateAsync(
                Period("Clashing quarter", new DateTime(Year, 3, 1), new DateTime(Year, 5, 31)),
                TenantData.TestUserId));
    }

    [Fact]
    public async Task A_period_that_does_not_overlap_is_accepted()
    {
        using var tenant = await _fixture.NewTenantAsync();

        var next = await tenant.Get<IFinancialPeriodService>().CreateAsync(
            Period($"FY {Year + 1}", new DateTime(Year + 1, 1, 1), new DateTime(Year + 1, 12, 31)),
            TenantData.TestUserId);

        Assert.False(next.IsClosed);
        Assert.True(next.PeriodID > 0);
    }

    /// <summary>
    /// The overlap check asks whether the NEW period's start or end sits inside an existing one.
    /// A period that straddles an existing one satisfies neither test while still overlapping it
    /// completely — the containment case.
    /// </summary>
    [Fact]
    public async Task A_period_that_completely_contains_an_existing_one_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IFinancialPeriodService>().CreateAsync(
                Period("Straddling decade", new DateTime(Year - 1, 1, 1), new DateTime(Year + 1, 12, 31)),
                TenantData.TestUserId));
    }

    /// <summary>
    /// The consequence of the gap above, stated as the behaviour that actually matters: a date
    /// covered by a closed period must report as locked, whatever else covers it.
    /// </summary>
    [Fact]
    public async Task A_date_inside_a_closed_period_reports_as_locked()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var service = tenant.Get<IFinancialPeriodService>();

        Assert.False(await service.IsPeriodLockedAsync(AppTime.Today));

        await tenant.CloseCurrentPeriodAsync();

        Assert.True(await service.IsPeriodLockedAsync(AppTime.Today));
    }

    [Fact]
    public async Task A_date_outside_every_period_is_not_locked()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.CloseCurrentPeriodAsync();

        // Provisioning only creates the current financial year.
        var outsideAnyPeriod = new DateTime(Year + 5, 6, 15);
        Assert.False(await tenant.Get<IFinancialPeriodService>().IsPeriodLockedAsync(outsideAnyPeriod));
    }

    [Fact]
    public async Task Closing_records_who_closed_it_and_when()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var period = await tenant.Db.FinancialPeriods
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);

        await tenant.Get<IFinancialPeriodService>()
            .ClosePeriodAsync(period.PeriodID, "audited", TenantData.TestUserId);

        var reloaded = await tenant.Db.FinancialPeriods.AsNoTracking().FirstAsync(p => p.PeriodID == period.PeriodID);
        Assert.True(reloaded.IsClosed);
        Assert.NotNull(reloaded.ClosedAt);
        Assert.Equal(TenantData.TestUserId, reloaded.ClosedBy);
        Assert.Equal("audited", reloaded.Remarks);
    }

    [Fact]
    public async Task Re_opening_clears_the_closure_stamp()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var period = await tenant.Db.FinancialPeriods
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);

        var service = tenant.Get<IFinancialPeriodService>();
        await service.ClosePeriodAsync(period.PeriodID, "audited", TenantData.TestUserId);
        await service.OpenPeriodAsync(period.PeriodID, TenantData.TestUserId);

        var reloaded = await tenant.Db.FinancialPeriods.AsNoTracking().FirstAsync(p => p.PeriodID == period.PeriodID);
        Assert.False(reloaded.IsClosed);
        Assert.Null(reloaded.ClosedAt);
        Assert.Null(reloaded.ClosedBy);
    }

    /// <summary>Periods belong to one pharmacy — closing one must not freeze another's books.</summary>
    [Fact]
    public async Task Closing_one_pharmacys_period_does_not_lock_another_pharmacy()
    {
        using var alpha = await _fixture.NewTenantAsync("Alpha Pharmacy");
        using var beta = await _fixture.NewTenantAsync("Beta Pharmacy");

        await alpha.CloseCurrentPeriodAsync();

        Assert.True(await alpha.Get<IFinancialPeriodService>().IsPeriodLockedAsync(AppTime.Today));
        Assert.False(await beta.Get<IFinancialPeriodService>().IsPeriodLockedAsync(AppTime.Today));

        var world = await beta.SeedWorldAsync();
        await beta.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m);
        var sale = await beta.SellAsync(world, qty: 1, unitPrice: 20m, paid: 20m);
        Assert.Equal("Approved", sale.Status);
    }
}
