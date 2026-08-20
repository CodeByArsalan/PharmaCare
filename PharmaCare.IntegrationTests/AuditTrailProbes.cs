using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Logging;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Logging;
using PharmaCare.Domain.Enums;
using PharmaCare.Infrastructure;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Probes the activity log — the only record of who did what, and the thing an owner reaches for
/// when the books disagree with the till. It has never been tested.
///
/// <para>
/// There are TWO writers into that second database and they disagree about who the tenant is.
/// <c>ActivityLogService.LogActivityAsync</c> (used for logins and explicit calls) stamps
/// <c>ICurrentTenant.TenantId</c>, which honours an explicit tenant scope.
/// <c>AuditSaveChangesInterceptor</c> (used for every entity change) reads the pharmacy claim off
/// the HttpContext directly and knows nothing about a scope. Anything written under an explicit
/// scope by a user who does not personally belong to that pharmacy is therefore logged with no
/// pharmacy at all — and the read side filters on exactly that column.
/// </para>
///
/// <para>Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.</para>
/// </summary>
[Collection(Collections.Database)]
public class AuditTrailProbes
{
    private readonly DatabaseFixture _fixture;

    public AuditTrailProbes(DatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------------------------------
    // Reading the log back. Entries are seeded through LogActivityAsync, the writer that stamps
    // the tenant correctly, so these probes isolate the READ filters.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Filtering_from_today_to_today_returns_todays_entries()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var logs = tenant.Get<IActivityLogService>();

        await logs.LogActivityAsync(7, "probe@test.local", ActivityType.Create, "Party", "42",
            description: "seeded for the date-boundary probe");

        // These are the dates the Activity Log screen puts in the boxes on first load.
        var sameDay = await logs.GetLogsAsync(new ActivityLogFilterDto
        {
            FromDate = AppTime.Today,
            ToDate = AppTime.Today
        });

        var wholeDay = await logs.GetLogsAsync(new ActivityLogFilterDto
        {
            FromDate = AppTime.Today,
            ToDate = AppTime.Today.AddDays(1)
        });

        Assert.True(sameDay.TotalCount == wholeDay.TotalCount,
            $"Filtering from today to today returned {sameDay.TotalCount} of the day's " +
            $"{wholeDay.TotalCount} entries. ActivityLogService compares a TIMESTAMP against " +
            "`<= ToDate`, so a date-only bound cuts the day off at midnight. Every report service " +
            "in the codebase uses `< ToDate.AddDays(1)` for precisely this reason — the audit log " +
            "is the one place that does not, and it is the screen whose default filter is " +
            "today-to-today.");
    }

    [Fact]
    public async Task A_zero_page_number_does_not_reach_SQL_as_a_negative_offset()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var logs = tenant.Get<IActivityLogService>();
        await logs.LogActivityAsync(7, "probe@test.local", ActivityType.Create, "Party", "1");

        // PageNumber is model-bound straight from the query string with no clamp; the offset is
        // (PageNumber - 1) * PageSize. BaseController.NormalizePage exists for exactly this case
        // and the activity log never calls it.
        var error = await Record.ExceptionAsync(() => logs.GetLogsAsync(new ActivityLogFilterDto
        {
            PageNumber = 0
        }));

        Assert.True(error is null,
            $"?PageNumber=0 on the activity log reached the database and threw " +
            $"{error?.GetType().Name}: a hand-edited URL is an unhandled 500.");
    }

    [Fact]
    public async Task A_negative_page_size_does_not_reach_SQL()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var logs = tenant.Get<IActivityLogService>();
        await logs.LogActivityAsync(7, "probe@test.local", ActivityType.Create, "Party", "1");

        var error = await Record.ExceptionAsync(() => logs.GetLogsAsync(new ActivityLogFilterDto
        {
            PageSize = -1
        }));

        Assert.True(error is null,
            $"?PageSize=-1 on the activity log threw {error?.GetType().Name}.");
    }

    [Fact]
    public async Task An_unbounded_page_size_is_capped()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var logs = tenant.Get<IActivityLogService>();
        await logs.LogActivityAsync(7, "probe@test.local", ActivityType.Create, "Party", "1");

        var result = await logs.GetLogsAsync(new ActivityLogFilterDto { PageSize = 1_000_000 });

        Assert.True(result.PageSize <= 500,
            $"The activity log accepted a page size of {result.PageSize}. It is the largest table in " +
            "the system and the only one holding a copy of every changed field, so an unclamped page " +
            "size is both the cheapest way to exhaust the server's memory and the cheapest way to " +
            "walk off with the entire record in a single request.");
    }

    [Fact]
    public async Task One_pharmacys_activity_is_invisible_to_another()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        await first.Get<IActivityLogService>().LogActivityAsync(
            7, "confidential@first.local", ActivityType.Update, "Party", "9001",
            description: "first pharmacy's private business");

        var visible = await second.Get<IActivityLogService>().GetLogsAsync(new ActivityLogFilterDto
        {
            EntityName = "Party",
            EntityId = "9001"
        });

        Assert.Empty(visible.Items);
    }

    [Fact]
    public async Task A_log_entry_from_another_pharmacy_cannot_be_opened_by_id()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        await first.Get<IActivityLogService>().LogActivityAsync(
            7, "confidential@first.local", ActivityType.Update, "Party", "9002");

        var foreignId = await first.Get<LogDbContext>().ActivityLogs
            .AsNoTracking()
            .Where(l => l.EntityId == "9002")
            .Select(l => l.ActivityLogID)
            .FirstAsync();

        Assert.Null(await second.Get<IActivityLogService>().GetByIdAsync(foreignId));
    }

    [Fact]
    public async Task The_summary_counts_only_the_current_pharmacys_activity()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        var before = await second.Get<IActivityLogService>().GetSummaryAsync(AppTime.Today.AddDays(-1));

        for (var i = 0; i < 3; i++)
        {
            await first.Get<IActivityLogService>().LogActivityAsync(
                7, "busy@first.local", ActivityType.Create, "Party", $"70{i}");
        }

        var after = await second.Get<IActivityLogService>().GetSummaryAsync(AppTime.Today.AddDays(-1));

        Assert.Equal(before.TotalLogs, after.TotalLogs);
    }

    // ------------------------------------------------------------------------------------------
    // What the interceptor writes.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_customers_contact_details_are_masked_in_the_audit_log()
    {
        using var tenant = await _fixture.NewTenantAsync();

        const string phone = "0300-555-0142";
        const string contact = "0321-555-0199";
        const string address = "House 14, Jinnah Road, Karachi";

        await tenant.Get<IPartyService>().CreateAsync(new Party
        {
            Name = "Private Person",
            PartyType = "Customer",
            Phone = phone,
            ContactNumber = contact,
            Address = address,
            Email = "private.person@example.com"
        }, TenantData.TestUserId);

        var written = await LatestEntityValuesAsync(tenant, nameof(Party));

        Assert.NotNull(written);
        Assert.False(written!.Contains(phone) || written.Contains(contact) || written.Contains(address),
            "A customer's phone number, alternate contact number and home address were written " +
            "verbatim into the audit database. The interceptor already masks Email, PhoneNumber, " +
            "AccountNumber and IBAN, so the intent to keep personal data out of that second database " +
            "is explicit — the mask list simply does not cover the column names Party actually uses " +
            "(Phone, ContactNumber, Address). The audit log has no redaction path, so every edit " +
            "accumulates another cleartext copy of a customer's details.");
    }

    [Fact]
    public async Task A_password_never_appears_in_the_audit_log()
    {
        using var tenant = await _fixture.NewTenantAsync();

        var text = string.Concat(await tenant.Get<LogDbContext>().ActivityLogs
            .AsNoTracking()
            .Where(l => l.EntityName == "User")
            .Select(l => (l.OldValues ?? "") + (l.NewValues ?? ""))
            .ToListAsync());

        Assert.DoesNotContain("PasswordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TestPass123", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provisioning_a_pharmacy_is_recorded_in_that_pharmacys_own_audit_trail()
    {
        // Provisioning runs under an explicit tenant scope, driven by a platform administrator who
        // carries no pharmacy claim of their own. Everything it creates — the chart of accounts, the
        // Administrator role and its permissions, the first user — is an entity change and so goes
        // through AuditSaveChangesInterceptor, which reads the pharmacy from the HTTP claim rather
        // than from the scope.
        using var tenant = await _fixture.NewTenantAsync();

        var stamped = await tenant.Get<LogDbContext>().ActivityLogs
            .AsNoTracking()
            .CountAsync(l => l.Pharmacy_ID == tenant.PharmacyId);

        Assert.True(stamped > 0,
            "Not one entry from the pharmacy's own provisioning carries its pharmacy id, so the " +
            "creation of its chart of accounts, its Administrator role and its first user is absent " +
            "from its audit trail permanently. AuditSaveChangesInterceptor reads the tenant from the " +
            "HttpContext claim; ActivityLogService reads it from ICurrentTenant, which honours the " +
            "scope. The two writers into the same table disagree.");
    }

    // ------------------------------------------------------------------------------------------

    private static async Task<string?> LatestEntityValuesAsync(TenantScope tenant, string entityName) =>
        await tenant.Get<LogDbContext>().ActivityLogs
            .AsNoTracking()
            .Where(l => l.EntityName == entityName)
            .OrderByDescending(l => l.ActivityLogID)
            .Select(l => l.NewValues)
            .FirstOrDefaultAsync();
}
