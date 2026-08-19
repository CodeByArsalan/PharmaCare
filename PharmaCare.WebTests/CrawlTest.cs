using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PharmaCare.WebTests;

/// <summary>
/// Smoke-crawls every reachable controller Index/Add page with an admin session and asserts none
/// return HTTP 500. Endpoints are discovered from the app's own routing table, so new pages are
/// covered automatically.
/// </summary>
[Collection(WebCollection.Name)]
public class CrawlTest
{
    private readonly WebTestFixture _fx;
    public CrawlTest(WebTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Admin_crawl_of_index_and_add_pages_has_no_500s()
    {
        var eds = _fx.Factory.Services.GetRequiredService<EndpointDataSource>();

        var urls = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in eds.Endpoints)
        {
            var cad = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (cad == null) continue;

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            var getAllowed = methods == null || methods.Contains("GET");
            if (!getAllowed) continue;

            var action = cad.ActionName;
            if (!(action.EndsWith("Index", StringComparison.Ordinal) ||
                  action.StartsWith("Add", StringComparison.Ordinal)))
                continue;

            // Account pages are anonymous; Pharmacies is the platform-admin area (our user is a
            // pharmacy admin) — both are out of scope for a tenant-admin crawl.
            if (string.Equals(cad.ControllerName, "Account", StringComparison.OrdinalIgnoreCase)) continue;

            urls.Add($"/{cad.ControllerName}/{action}");
        }

        var client = await _fx.AdminClientAsync();

        var failures = new StringBuilder();
        var crawled = 0;
        foreach (var url in urls)
        {
            HttpResponseMessage resp;
            try
            {
                resp = await client.GetAsync(url);
            }
            catch (Exception ex)
            {
                failures.AppendLine($"{url} -> THREW {ex.GetType().Name}: {ex.Message}");
                continue;
            }
            crawled++;
            if (resp.StatusCode == HttpStatusCode.InternalServerError)
                failures.AppendLine($"{url} -> 500");
        }

        Assert.True(crawled > 0, "Crawl discovered no Index/Add pages — routing enumeration likely broke.");
        Assert.True(failures.Length == 0, $"Pages returned 500:\n{failures}");
    }
}
