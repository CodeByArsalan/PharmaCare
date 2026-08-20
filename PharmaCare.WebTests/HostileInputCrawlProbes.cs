using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PharmaCare.WebTests;

/// <summary>
/// Drives every mutating endpoint the application routes, with hostile-but-plausible input, and
/// asserts none of them answers 500.
///
/// <para>
/// Endpoints are enumerated from the app's own routing table rather than a hand-written list, so a
/// newly added action is covered the day it appears. Three inputs are used against each: a
/// well-formed but forged id token, a syntactically invalid one, and an empty form. A 400/403/404
/// or a redirect carrying an error message are all fine answers; an unhandled 500 is not, because
/// it means an exception escaped the action and the user gets an opaque error page instead of a
/// message they can act on.
/// </para>
///
/// <para>
/// GET endpoints get the same treatment: a view action that trusts <c>DecryptId</c> to return
/// something real is the classic source of a null-dereference 500.
/// </para>
/// </summary>
[Collection(WebCollection.Name)]
public class HostileInputCrawlProbes
{
    private const string TokenPage = "/Home/Index";
    private readonly WebTestFixture _fx;

    public HostileInputCrawlProbes(WebTestFixture fx) => _fx = fx;

    [Fact]
    public async Task No_mutating_endpoint_500s_on_a_forged_or_malformed_id()
    {
        var endpoints = DiscoverEndpoints(wantPost: true);
        Assert.True(endpoints.Count > 0, "Endpoint enumeration found no POST actions.");

        var client = await _fx.AdminClientAsync();
        var token = await HttpTestHelpers.GetAntiForgeryTokenAsync(client, TokenPage);

        // Three shapes of bad id: a valid-looking but foreign protected token, obvious garbage,
        // and a raw integer for the endpoints that take one unencrypted.
        var badIds = new[] { "CfDJ8AAAAAAAAAAAAAAAAAAAAAA_forged_token_value", "%%%not-base64%%%", "-1" };

        var failures = new StringBuilder();
        var probed = 0;

        foreach (var url in endpoints)
        {
            foreach (var badId in badIds)
            {
                var form = new Dictionary<string, string>
                {
                    ["id"] = badId,
                    ["roleId"] = badId,
                    ["voidReason"] = "hostile input crawl",
                    ["remarks"] = "hostile input crawl"
                };

                HttpResponseMessage resp;
                try
                {
                    resp = await HttpTestHelpers.PostFormAsync(client, url, form, antiForgeryToken: token);
                }
                catch (Exception ex)
                {
                    failures.AppendLine($"POST {url} id='{Trim(badId)}' -> THREW {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                probed++;
                if (resp.StatusCode == HttpStatusCode.InternalServerError)
                    failures.AppendLine($"POST {url} id='{Trim(badId)}' -> 500");
            }
        }

        Assert.True(probed > 0, "No endpoint was actually probed.");
        Assert.True(failures.Length == 0,
            $"Endpoints answered 500 to a forged/malformed id ({probed} probes):\n{failures}");
    }

    [Fact]
    public async Task No_mutating_endpoint_500s_on_an_empty_form()
    {
        var endpoints = DiscoverEndpoints(wantPost: true);
        var client = await _fx.AdminClientAsync();
        var token = await HttpTestHelpers.GetAntiForgeryTokenAsync(client, TokenPage);

        var failures = new StringBuilder();
        var probed = 0;

        foreach (var url in endpoints)
        {
            HttpResponseMessage resp;
            try
            {
                resp = await HttpTestHelpers.PostFormAsync(client, url,
                    new Dictionary<string, string>(), antiForgeryToken: token);
            }
            catch (Exception ex)
            {
                failures.AppendLine($"POST {url} (empty) -> THREW {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            probed++;
            if (resp.StatusCode == HttpStatusCode.InternalServerError)
                failures.AppendLine($"POST {url} (empty) -> 500");
        }

        Assert.True(probed > 0, "No endpoint was actually probed.");
        Assert.True(failures.Length == 0,
            $"Endpoints answered 500 to an empty form ({probed} probes):\n{failures}");
    }

    [Fact]
    public async Task No_view_or_detail_page_500s_on_a_forged_id()
    {
        var endpoints = DiscoverEndpoints(wantPost: false);
        var client = await _fx.AdminClientAsync();

        var failures = new StringBuilder();
        var probed = 0;

        foreach (var url in endpoints)
        {
            foreach (var badId in new[] { "CfDJ8AAAAAAAAAAAAAAAAAAAAAA_forged", "%%%bad%%%", "-1" })
            {
                HttpResponseMessage resp;
                try
                {
                    resp = await client.GetAsync($"{url}?id={Uri.EscapeDataString(badId)}");
                }
                catch (Exception ex)
                {
                    failures.AppendLine($"GET {url} id='{Trim(badId)}' -> THREW {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                probed++;
                if (resp.StatusCode == HttpStatusCode.InternalServerError)
                    failures.AppendLine($"GET {url} id='{Trim(badId)}' -> 500");
            }
        }

        Assert.True(probed > 0, "No endpoint was actually probed.");
        Assert.True(failures.Length == 0,
            $"Pages answered 500 to a forged id ({probed} probes):\n{failures}");
    }

    private static string Trim(string s) => s.Length <= 24 ? s : s[..24] + "...";

    /// <summary>
    /// Enumerates the app's routed controller actions. Account (anonymous login/logout) and
    /// Pharmacies (platform-admin only) are excluded: neither is reachable by the tenant admin
    /// these probes sign in as, so a redirect from them would prove nothing.
    /// </summary>
    private List<string> DiscoverEndpoints(bool wantPost)
    {
        var eds = _fx.Factory.Services.GetRequiredService<EndpointDataSource>();
        var urls = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in eds.Endpoints)
        {
            var cad = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (cad == null) continue;

            if (string.Equals(cad.ControllerName, "Account", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(cad.ControllerName, "Pharmacies", StringComparison.OrdinalIgnoreCase)) continue;

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            var isPost = methods != null && methods.Contains("POST");
            var isGet = methods == null || methods.Contains("GET");

            if (wantPost)
            {
                if (!isPost) continue;
            }
            else
            {
                if (!isGet || isPost) continue;
                // Only the actions that actually take an id are interesting for this probe.
                var a = cad.ActionName;
                if (!(a.StartsWith("View", StringComparison.OrdinalIgnoreCase)
                      || a.StartsWith("Edit", StringComparison.OrdinalIgnoreCase)
                      || a.StartsWith("Details", StringComparison.OrdinalIgnoreCase)
                      || a.Contains("History", StringComparison.OrdinalIgnoreCase)
                      || a.StartsWith("Get", StringComparison.OrdinalIgnoreCase)))
                    continue;
            }

            urls.Add($"/{cad.ControllerName}/{cad.ActionName}");
        }

        return urls.ToList();
    }
}
