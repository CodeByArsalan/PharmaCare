using System.Net;

namespace PharmaCare.WebTests;

/// <summary>
/// HTTP probes for the RBAC void-permission gap, unhandled-exception 500s, and the oversized
/// void-reason overflow. Each test asserts the CORRECT behavior, so a FAILING test = confirmed
/// defect (these are left failing on purpose per the audit brief).
/// </summary>
[Collection(WebCollection.Name)]
public class RbacAndErrorProbes
{
    private const string TokenPage = "/Home/Index"; // every authed page carries the header logout form's token
    private readonly WebTestFixture _fx;
    public RbacAndErrorProbes(WebTestFixture fx) => _fx = fx;

    // Probe 1 — RBAC void gap. ReverseJournalVoucher is [LinkedToPage("JournalVoucher",
    // "JournalVoucherIndex")] with NO PermissionType, so it resolves to "view". A view-only user
    // must NOT be able to reverse a financial voucher.
    [Fact]
    public async Task ViewOnly_user_cannot_reverse_a_journal_voucher()
    {
        var voucher = await _fx.SeedJournalVoucherAsync();

        var client = await _fx.ViewOnlyClientAsync();

        await HttpTestHelpers.PostFormAsync(client, "/JournalVoucher/ReverseJournalVoucher",
            new Dictionary<string, string> { ["id"] = voucher.EncryptedId, ["voidReason"] = "rbac probe" },
            tokenPath: TokenPage);

        var reversed = await _fx.IsVoucherReversedAsync(voucher.VoucherId);
        Assert.False(reversed,
            "DEFECT: a VIEW-only user reversed a posted journal voucher (endpoint gated on 'view').");
    }

    // Probe 2 — unhandled exception. Reversing an already-reversed JV throws (plain Exception) and
    // ReverseJournalVoucher has no try/catch, so the second POST 500s instead of a handled error.
    [Fact]
    public async Task Double_reverse_of_a_journal_voucher_is_handled_not_500()
    {
        var voucher = await _fx.SeedJournalVoucherAsync();

        var client = await _fx.AdminClientAsync();

        var first = await HttpTestHelpers.PostFormAsync(client, "/JournalVoucher/ReverseJournalVoucher",
            new Dictionary<string, string> { ["id"] = voucher.EncryptedId, ["voidReason"] = "first" },
            tokenPath: TokenPage);
        Assert.True(first.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Setup: first reverse should redirect, got {(int)first.StatusCode}.");

        var second = await HttpTestHelpers.PostFormAsync(client, "/JournalVoucher/ReverseJournalVoucher",
            new Dictionary<string, string> { ["id"] = voucher.EncryptedId, ["voidReason"] = "second" },
            tokenPath: TokenPage);

        Assert.NotEqual(HttpStatusCode.InternalServerError, second.StatusCode);
    }

    // Probe 6 — void reason overflow. voidReason is unbounded at the controller but the DB column is
    // nvarchar(500); a longer reason should be validated, not thrown as a raw SqlException (500).
    [Fact]
    public async Task Oversized_void_reason_is_handled_not_500()
    {
        var voucher = await _fx.SeedJournalVoucherAsync();

        var client = await _fx.AdminClientAsync();

        var reason = new string('X', 600); // column caps at 500

        var resp = await HttpTestHelpers.PostFormAsync(client, "/JournalVoucher/ReverseJournalVoucher",
            new Dictionary<string, string> { ["id"] = voucher.EncryptedId, ["voidReason"] = reason },
            tokenPath: TokenPage);

        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    // Probe 10a — auth basics: unauthenticated POST/GET is redirected to login, not 500/200.
    [Fact]
    public async Task Unauthenticated_request_redirects_to_login()
    {
        var client = _fx.Factory.CreateTestClient();

        var resp = await client.GetAsync("/JournalVoucher/JournalVoucherIndex");

        Assert.True(resp.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Expected redirect to login, got {(int)resp.StatusCode}.");
        Assert.Contains("Login", resp.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // Probe 10b — CSRF: an authenticated POST WITHOUT the antiforgery token must be rejected (400).
    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var voucher = await _fx.SeedJournalVoucherAsync();

        var client = await _fx.AdminClientAsync();

        // No token supplied.
        var resp = await HttpTestHelpers.PostFormAsync(client, "/JournalVoucher/ReverseJournalVoucher",
            new Dictionary<string, string> { ["id"] = voucher.EncryptedId, ["voidReason"] = "no token" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.False(await _fx.IsVoucherReversedAsync(voucher.VoucherId),
            "A tokenless POST must not have any effect.");
    }
}
