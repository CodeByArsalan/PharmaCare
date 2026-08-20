using System.Net;

namespace PharmaCare.WebTests;

/// <summary>
/// Probes the shape of the login rate limiter.
///
/// <para>
/// The limiter used to be declared with <c>options.AddFixedWindowLimiter("auth", ...)</c> — a named
/// limiter with no partition key, which is ONE bucket for the whole process. Its 10-per-minute
/// budget was therefore not "10 attempts per caller" but 10 attempts for the entire application:
/// the eleventh person to sign on at shift change was refused, and any anonymous visitor could
/// spend the budget deliberately and keep every member of staff out. It is now partitioned per
/// caller, and these two probes pin both halves of that: distinct callers do not interfere, and a
/// single abusive caller is still throttled.
/// </para>
///
/// <para>
/// The probes run against their OWN host so that spending a login budget cannot starve the rest of
/// the suite. They neither reset nor seed anything — failed logins against addresses that do not
/// exist exercise the limiter perfectly well.
/// </para>
///
/// <para>
/// Joined to the shared web collection purely for SEQUENCING: its own host must not boot and seed
/// while the main fixture is doing the same against the same database. It takes no fixture, and
/// its separate host means its login budget is genuinely its own.
/// </para>
/// </summary>
[Collection(WebCollection.Name)]
public class RateLimitingProbes : IAsyncLifetime
{
    private PharmaCareWebFactory _factory = null!;

    public Task InitializeAsync()
    {
        AppTime.Initialize("Asia/Karachi");
        _factory = new PharmaCareWebFactory();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_login_limiter_does_not_lock_out_every_user_at_once()
    {
        // Twelve DISTINCT people at twelve distinct addresses, one login attempt each. Nobody is
        // being brute-forced; this is a morning shift starting. All twelve must reach a normal
        // "invalid credentials" answer.
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 12; i++)
        {
            using var client = _factory.CreateTestClient();
            var resp = await LoginFromAsync(client, $"203.0.113.{i + 1}", $"staff-member-{i}@webtest.local");
            statuses.Add(resp.StatusCode);
        }

        var throttled = statuses.Count(s => s == HttpStatusCode.TooManyRequests);

        Assert.True(throttled == 0,
            $"{throttled} of 12 separate callers were refused with HTTP 429 while each made a " +
            "single login attempt — the login budget is still shared rather than per-caller. " +
            $"Observed sequence: {string.Join(", ", statuses.Select(s => (int)s))}");
    }

    [Fact]
    public async Task The_login_limiter_still_throttles_one_abusive_caller()
    {
        // The other half of the contract: partitioning must not have turned the limiter off.
        // Twenty attempts from ONE address against the 10-per-minute budget.
        var statuses = new List<HttpStatusCode>();

        using var client = _factory.CreateTestClient();
        for (var i = 0; i < 20; i++)
        {
            var resp = await LoginFromAsync(client, "198.51.100.7", $"victim-{i}@webtest.local");
            statuses.Add(resp.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);

        // And the throttling must not leak to anybody else.
        using var bystander = _factory.CreateTestClient();
        var bystanderResp = await LoginFromAsync(bystander, "203.0.113.200", "bystander@webtest.local");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, bystanderResp.StatusCode);
    }

    /// <summary>One login attempt that presents itself as coming from <paramref name="clientIp"/>.</summary>
    private static async Task<HttpResponseMessage> LoginFromAsync(
        HttpClient client, string clientIp, string email)
    {
        var token = await GetTokenAsync(client, clientIp);

        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "SomePassword123",
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = token ?? string.Empty
        };

        return await HttpTestHelpers.PostFormAsync(client, "/Account/Login", form,
            antiForgeryToken: token,
            extraHeaders: new Dictionary<string, string> { ["X-Forwarded-For"] = clientIp });
    }

    /// <summary>
    /// Fetches the login page's antiforgery token from the same address. Returns null once the GET
    /// itself is being throttled, which is a legitimate state for the abusive-caller probe.
    /// </summary>
    private static async Task<string?> GetTokenAsync(HttpClient client, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var html = await response.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(
            html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        return match.Success ? match.Groups[1].Value : null;
    }
}
