using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PharmaCare.WebTests;

/// <summary>
/// Thin helpers for driving the real HTTP endpoints: a cookie-persisting client, the actual login
/// round-trip (antiforgery token + credentials), and antiforgery-protected form posts.
/// </summary>
public static class HttpTestHelpers
{
    private static readonly Regex TokenRegex = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>A client that keeps cookies across requests and never auto-follows redirects,
    /// so probes can assert on the 302/500/200 the server actually returns.
    /// NOTE: deliberately NOT named CreateClient — the built-in instance method of the same name
    /// would win overload resolution and silently give a redirect-following client.</summary>
    public static HttpClient CreateTestClient(this PharmaCareWebFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost")
        });

    /// <summary>Pulls the hidden antiforgery token out of a rendered form page.</summary>
    public static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = TokenRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException($"No antiforgery token found on '{path}'.");
        return match.Groups[1].Value;
    }

    /// <summary>Logs in through the real /Account/Login endpoint. Returns the final response
    /// (a 302 to Home on success). The client holds the auth + session cookies afterwards.</summary>
    public static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string email, string password)
    {
        var token = await GetAntiForgeryTokenAsync(client, "/Account/Login");

        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        };

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));
    }

    /// <summary>Logs in and asserts it succeeded (302 away from the login page).</summary>
    public static async Task LoginOrThrowAsync(HttpClient client, string email, string password)
    {
        var resp = await LoginAsync(client, email, password);
        if (resp.StatusCode != HttpStatusCode.Redirect && resp.StatusCode != HttpStatusCode.Found)
            throw new InvalidOperationException(
                $"Login for '{email}' did not redirect (got {(int)resp.StatusCode}). " +
                "Credentials or cookie policy likely wrong.");
        var location = resp.Headers.Location?.ToString() ?? "";
        if (location.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Login for '{email}' bounced back to the login page.");
    }

    /// <summary>Posts an antiforgery-protected form. <paramref name="tokenPath"/> is a page the
    /// current user can GET that renders a token; falls back to the caller-supplied token.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string url,
        IDictionary<string, string> fields,
        string? antiForgeryToken = null,
        string? tokenPath = null,
        IDictionary<string, string>? extraHeaders = null)
    {
        var token = antiForgeryToken
                    ?? (tokenPath != null ? await GetAntiForgeryTokenAsync(client, tokenPath) : null);

        var all = new Dictionary<string, string>(fields);
        if (token != null)
            all["__RequestVerificationToken"] = token;

        var content = new FormUrlEncodedContent(all);
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (extraHeaders != null)
            foreach (var (k, v) in extraHeaders)
                request.Headers.TryAddWithoutValidation(k, v);

        return await client.SendAsync(request);
    }
}
