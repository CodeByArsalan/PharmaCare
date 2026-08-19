using System.Net;

namespace PharmaCare.WebTests;

/// <summary>
/// Sanity checks that the harness itself works: the app boots, an anonymous request is redirected
/// to login, and a real login yields a working authenticated session.
/// </summary>
[Collection(WebCollection.Name)]
public class SmokeTests
{
    private readonly WebTestFixture _fx;
    public SmokeTests(WebTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Anonymous_request_to_protected_page_redirects_to_login()
    {
        var client = _fx.Factory.CreateTestClient();

        var resp = await client.GetAsync("/Sale/SalesIndex");

        // Not a 500, not a silent 200 — a redirect toward the login page.
        Assert.True(resp.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Expected redirect, got {(int)resp.StatusCode}.");
        var location = resp.Headers.Location?.ToString() ?? "";
        Assert.Contains("Login", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_can_log_in_and_load_an_index_page()
    {
        var client = _fx.Factory.CreateTestClient();

        await HttpTestHelpers.LoginOrThrowAsync(client, _fx.AdminEmail, _fx.AdminPassword);

        var resp = await client.GetAsync("/Sale/SalesIndex");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
