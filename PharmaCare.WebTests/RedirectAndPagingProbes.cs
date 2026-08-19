using System.Net;
using PharmaCare.Web.Utilities;

namespace PharmaCare.WebTests;

[Collection(WebCollection.Name)]
public class RedirectAndPagingProbes
{
    private const string TokenPage = "/Home/Index";
    private readonly WebTestFixture _fx;
    public RedirectAndPagingProbes(WebTestFixture fx) => _fx = fx;

    // Probe 7 — open redirect. SupplierPayment.ApplyCredit redirects to the raw Referer header with
    // no Url.IsLocalUrl guard. A crafted external Referer must NOT become the redirect target.
    [Fact]
    public async Task ApplyCredit_does_not_open_redirect_to_external_referer()
    {
        var client = await _fx.AdminClientAsync();

        // Invalid credit ids: the action catches the failure and STILL redirects to Referer.
        var resp = await HttpTestHelpers.PostFormAsync(client, "/SupplierPayment/ApplyCredit",
            new Dictionary<string, string>
            {
                ["creditType"] = "CreditNote",
                ["creditId"] = Utility.EncryptId(1),
                ["grnId"] = Utility.EncryptId(1),
                ["amount"] = "1"
            },
            tokenPath: TokenPage,
            extraHeaders: new Dictionary<string, string> { ["Referer"] = "https://evil.example/phish" });

        var location = resp.Headers.Location?.ToString() ?? "";
        Assert.False(location.StartsWith("https://evil.example", StringComparison.OrdinalIgnoreCase),
            $"DEFECT: open redirect — server sent the user to '{location}' from the Referer header.");
    }

    // Probe 4 — paging. page <= 0 yields a negative SQL OFFSET → SqlException (500). NormalizePageSize
    // guards pageSize but nothing clamps page.
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(int.MinValue)]
    public async Task Nonpositive_page_does_not_500(int page)
    {
        var client = await _fx.AdminClientAsync();

        var resp = await client.GetAsync($"/Sale/SalesIndex?page={page}");

        Assert.NotEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }
}
