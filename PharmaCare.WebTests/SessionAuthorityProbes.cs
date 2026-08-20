using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Infrastructure;
using PharmaCare.Web.Utilities;

namespace PharmaCare.WebTests;

/// <summary>
/// Probes where the application's answer to "what is this user allowed to do?" actually comes from.
///
/// <para>
/// It is not the database. <c>SessionService.InitializeSessionAsync</c> reads the user's roles and
/// their page permissions ONCE, flattens them into a JSON blob and writes it into the HTTP session;
/// <c>PageAuthorizationFilter</c> then answers every later request out of that blob without a query
/// — its own summary says so. <c>SessionInitializationMiddleware</c> rebuilds the blob only when it
/// is missing entirely. So the permission set a user carries is a snapshot of the moment they
/// signed in, and the session slides on every request.
/// </para>
///
/// <para>
/// That makes revocation the interesting case. Deactivating a USER rotates the security stamp and
/// Program.cs revalidates it every five minutes, which is a deliberate, documented bound. Nothing
/// equivalent exists for a change to what a still-active user may DO — and the snapshot query never
/// looks at <c>Role.IsActive</c> at all, so a deactivated role is not withdrawn even at the next
/// sign-in.
/// </para>
///
/// <para>Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.</para>
/// </summary>
[Collection(WebCollection.Name)]
public class SessionAuthorityProbes
{
    private readonly WebTestFixture _fx;

    public SessionAuthorityProbes(WebTestFixture fx) => _fx = fx;

    /// <summary>A page every probe user is granted, and whose GET is a plain "view".</summary>
    private const string GuardedController = "Category";
    private const string GuardedPath = "/Category/CategoriesIndex";

    // ------------------------------------------------------------------------------------------
    // Revocation while the user is signed in.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Revoking_a_permission_takes_effect_without_waiting_for_a_new_login()
    {
        var probe = await NewProbeUserAsync("revoke-live", "203.0.113.21");

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(probe, GuardedPath)).StatusCode);

        // An administrator unticks every box for this page — the ordinary way to withdraw access,
        // performed through the same service the role screen posts to.
        await RevokeAllPermissionsAsync(probe.RoleId);

        var after = await GetAsync(probe, GuardedPath);

        Assert.True(IsDenied(after),
            $"The user's page permissions were deleted and they still received " +
            $"{(int)after.StatusCode} on {GuardedPath}. Authorization is answered from a snapshot " +
            "taken at sign-in, and the session slides on every request, so a user who stays active " +
            "keeps withdrawn rights indefinitely. Revoking access is the one operation that must not " +
            "wait for the person to volunteer a fresh login.");
    }

    [Fact]
    public async Task Revoking_delete_rights_stops_the_user_deleting()
    {
        // The read-side probe above shows the snapshot is stale. This one shows what that costs:
        // the same staleness governs CanDelete, which is what gates every destructive endpoint.
        var probe = await NewProbeUserAsync("revoke-delete", "203.0.113.32");

        var categoryId = await RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var account = await db.Accounts.AsNoTracking()
                .Where(a => a.Name == "Inventory / Stock").Select(a => a.AccountID).FirstAsync();
            var sale = await db.Accounts.AsNoTracking()
                .Where(a => a.Name == "Sales Revenue").Select(a => a.AccountID).FirstAsync();
            var cogs = await db.Accounts.AsNoTracking()
                .Where(a => a.Name == "Cost of Goods Sold").Select(a => a.AccountID).FirstAsync();
            var damage = await db.Accounts.AsNoTracking()
                .Where(a => a.Name == "Damage & Loss").Select(a => a.AccountID).FirstAsync();

            var category = new Domain.Entities.Configuration.Category
            {
                Name = $"Revoke Probe {Guid.NewGuid():N}",
                StockAccount_ID = account,
                SaleAccount_ID = sale,
                COGSAccount_ID = cogs,
                DamageAccount_ID = damage,
                IsActive = true,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return category.CategoryID;
        });

        // The administrator takes away delete — and only delete — through the role screen's service.
        await RunAsync(async sp =>
        {
            var roles = sp.GetRequiredService<PharmaCare.Application.Interfaces.Security.IRoleService>();
            var current = await roles.GetPermissionsForRoleAsync(probe.RoleId);
            foreach (var p in current) p.CanDelete = false;
            await roles.UpdatePermissionsAsync(probe.RoleId, current);
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/Category/Delete")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = Utility.EncryptId(categoryId),
                ["__RequestVerificationToken"] =
                    await TokenFromAsync(probe.Client, GuardedPath, probe.ClientIp) ?? string.Empty
            })
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", probe.ClientIp);
        await probe.Client.SendAsync(request);

        var stillActive = await RunAsync(async sp =>
            await sp.GetRequiredService<PharmaCareDBContext>().Categories.AsNoTracking()
                .Where(c => c.CategoryID == categoryId).Select(c => c.IsActive).FirstAsync());

        Assert.True(stillActive,
            "Delete rights were revoked and the user deactivated the category anyway. The staleness " +
            "is not confined to what a user can SEE — the same snapshot answers CanDelete, so every " +
            "destructive endpoint in the application stays open to someone whose rights were " +
            "withdrawn, for as long as they keep the session alive.");
    }

    [Fact]
    public async Task Removing_every_role_from_a_user_takes_effect_without_a_new_login()
    {
        var probe = await NewProbeUserAsync("derole-live", "203.0.113.22");

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(probe, GuardedPath)).StatusCode);

        // The administrator clears the user's role list on the user edit screen.
        await RunAsync(async sp =>
        {
            var users = sp.GetRequiredService<PharmaCare.Application.Interfaces.Security.IUserService>();
            var (ok, error) = await users.UpdateUserAsync(
                new User { Id = probe.UserId, Email = probe.Email, FullName = "Probe derole-live" },
                newPassword: null,
                roleIds: new List<int>(),
                updatedBy: 0);
            if (!ok) throw new InvalidOperationException($"Role removal failed: {error}");
        });

        var after = await GetAsync(probe, GuardedPath);

        Assert.True(IsDenied(after),
            $"Every role was removed from the user and they still received {(int)after.StatusCode} " +
            $"on {GuardedPath}.");
    }

    [Fact]
    public async Task Deactivating_a_role_denies_its_holders_even_on_a_brand_new_login()
    {
        var probe = await NewProbeUserAsync("dead-role", "203.0.113.23");

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(probe, GuardedPath)).StatusCode);

        await RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var role = await db.Roles_Custom.FirstAsync(r => r.RoleID == probe.RoleId);
            role.IsActive = false;
            await db.SaveChangesAsync();
        });

        // A completely fresh sign-in, so the permission snapshot is rebuilt from the database.
        var reborn = _fx.Factory.CreateTestClient();
        await LoginAsync(reborn, probe.Email, ProbePassword, "203.0.113.24");

        var after = await GetAsync(reborn, GuardedPath, "203.0.113.24");

        Assert.True(IsDenied(after),
            $"The role was deactivated, the user then signed in FRESH, and still received " +
            $"{(int)after.StatusCode} on {GuardedPath}. The snapshot query joins UserRoles to " +
            "RolePages and never reads Role.IsActive, so deactivating a role withdraws nothing — it " +
            "only removes the role from the assignment dropdown. That makes the toggle on the role " +
            "screen look like a revocation control while doing no revoking.");
    }

    [Fact]
    public async Task A_deactivated_user_is_stopped_at_the_login_screen()
    {
        var probe = await NewProbeUserAsync("deactivated", "203.0.113.25");

        await RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var user = await db.Users.FirstAsync(u => u.Id == probe.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        });

        var fresh = _fx.Factory.CreateTestClient();
        var response = await HttpTestHelpers.PostFormAsync(fresh, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Email"] = probe.Email,
                ["Password"] = ProbePassword,
                ["RememberMe"] = "false"
            },
            antiForgeryToken: await TokenFromAsync(fresh, "/Account/Login", "203.0.113.26"),
            extraHeaders: Forwarded("203.0.113.26"));

        var location = response.Headers.Location?.ToString() ?? string.Empty;

        Assert.True(response.StatusCode == HttpStatusCode.OK || location.Contains("/Account/Login"),
            "A deactivated account was allowed to sign in.");
    }

    // ------------------------------------------------------------------------------------------
    // Who counts as an administrator.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_role_merely_named_admin_does_not_unlock_the_dashboard_financials()
    {
        // HomeController: `IsInRole("Admin", "Administrator") ||
        //                  CurrentUserRoleNames.Any(r => r.Contains("Admin", ...))`
        // and an affirmative answer sets CanViewSales/CanViewInventory/CanViewFinancials all true,
        // overriding the page permissions that were just evaluated.
        var probe = await NewProbeUserAsync("named-admin", "203.0.113.27", roleName: "Admin Assistant");

        var html = await (await GetAsync(probe, "/Home/Index")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Gross Profit", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_user_with_no_reporting_rights_sees_no_financial_figures()
    {
        var probe = await NewProbeUserAsync("no-reports", "203.0.113.28", roleName: "Shelf Stacker");

        var html = await (await GetAsync(probe, "/Home/Index")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Gross Profit", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------------
    // Session identity.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task One_users_session_cookie_on_another_users_auth_grants_only_the_auth_owners_rights()
    {
        // The session cookie is a bearer of STATE, not identity: the permission snapshot, user id
        // and pharmacy id all live behind it. This grafts the OWNER's session cookie onto a
        // request authenticated as a RIDER who holds no page permissions — the fixation/swap
        // shape. The request must be decided by the RIDER's identity, never by the owner's
        // snapshot, so session state has to be bound to the authenticated principal.
        var ownerJar = await ManualLoginJarAsync(
            (await NewProbeUserAsync("session-owner", "203.0.113.29")).Email, "203.0.113.34");
        var riderEmail = await CreateBareUserAsync("session-rider");
        var riderJar = await ManualLoginJarAsync(riderEmail, "203.0.113.35");

        var ownerSession = ownerJar.FirstOrDefault(c => c.StartsWith(".AspNetCore.Session", StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(ownerSession), "Owner login established no session cookie.");

        // Rider's auth cookie(s), but the OWNER's session cookie in place of the rider's own.
        var frankenCookies = riderJar
            .Where(c => !c.StartsWith(".AspNetCore.Session", StringComparison.Ordinal))
            .Append(ownerSession!)
            .ToList();

        using var attacker = _fx.Factory.CreateTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, GuardedPath);
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", frankenCookies));
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.90");
        var response = await attacker.SendAsync(request);

        Assert.True(IsDenied(response),
            $"A request authenticated as a permission-less rider, carrying the owner's session " +
            $"cookie, received {(int)response.StatusCode} on {GuardedPath}. Session state is not " +
            "bound to the authenticated principal, so grafting a session id transfers its owner's " +
            "rights to whoever presents it.");
    }

    /// <summary>Logs in with a NON-cookie-handling client so every Set-Cookie is visible, and
    /// returns the raw "name=value" cookie strings the browser would hold afterwards.</summary>
    private async Task<List<string>> ManualLoginJarAsync(string email, string clientIp)
    {
        using var client = _fx.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("http://localhost")
        });

        // GET the login page for the antiforgery token AND its cookie.
        var getReq = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        getReq.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
        var getResp = await client.SendAsync(getReq);
        var jar = CookiesFrom(getResp);
        var html = await getResp.Content.ReadAsStringAsync();
        var token = System.Text.RegularExpressions.Regex.Match(
            html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var postReq = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = ProbePassword,
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = token
            })
        };
        postReq.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
        postReq.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", jar.Values));
        var postResp = await client.SendAsync(postReq);

        // Merge cookies the login response set (auth cookie, refreshed antiforgery, session).
        foreach (var (name, value) in CookiesFrom(postResp))
            jar[name] = value;

        return jar.Values.ToList();
    }

    private static Dictionary<string, string> CookiesFrom(HttpResponseMessage response)
    {
        var jar = new Dictionary<string, string>(StringComparer.Ordinal);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var raw in setCookies)
            {
                var pair = raw.Split(';')[0];
                var eq = pair.IndexOf('=');
                if (eq > 0) jar[pair[..eq]] = pair;
            }
        }
        return jar;
    }

    private async Task<string> CreateBareUserAsync(string label)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{label}-{suffix}@webtest.local";
        await RunAsync(async sp =>
        {
            var userManager = sp.GetRequiredService<UserManager<User>>();
            var user = new User
            {
                UserName = email,
                Email = email,
                FullName = $"Bare {label}",
                IsActive = true,
                Pharmacy_ID = _fx.PharmacyId,
                IsPlatformAdmin = false,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            var created = await userManager.CreateAsync(user, ProbePassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("Bare user creation failed: " +
                    string.Join("; ", created.Errors.Select(e => e.Description)));
        });
        return email;
    }

    [Fact]
    public async Task Logging_out_leaves_the_session_unusable()
    {
        var probe = await NewProbeUserAsync("logout", "203.0.113.30");

        await HttpTestHelpers.PostFormAsync(probe.Client, "/Account/Logout",
            new Dictionary<string, string>(), tokenPath: "/Home/Index");

        var after = await GetAsync(probe, GuardedPath);

        Assert.True(IsDenied(after),
            $"A signed-out client still received {(int)after.StatusCode} on {GuardedPath}.");
    }

    // ------------------------------------------------------------------------------------------
    // What the real pipeline records.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_failed_sign_in_is_recorded_in_the_activity_log()
    {
        var probe = await NewProbeUserAsync("bad-password", "203.0.113.31");

        // NewProbeUserAsync signs the user in, and a SUCCESSFUL login is recorded. Count what is
        // already there so only what the failed attempt adds is measured.
        var before = await EntriesMentioningAsync(probe.Email);

        var attacker = _fx.Factory.CreateTestClient();
        await HttpTestHelpers.PostFormAsync(attacker, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Email"] = probe.Email,
                ["Password"] = "DefinitelyNotThePassword1",
                ["RememberMe"] = "false"
            },
            antiForgeryToken: await TokenFromAsync(attacker, "/Account/Login", "198.51.100.31"),
            extraHeaders: Forwarded("198.51.100.31"));

        var after = await EntriesMentioningAsync(probe.Email);

        Assert.True(after > before,
            $"A failed sign-in against a real account added nothing to the activity log ({before} " +
            "entries before, the same after). AccountController records a Login entry only inside " +
            "`if (result.Succeeded)`, and the ActivityType enum has no value for a failure at all, " +
            "so the log — the product's only forensic record — cannot show a password-guessing run, " +
            "cannot name the account being targeted, and cannot explain why an account is locked out.");
    }

    [Fact]
    public async Task A_change_made_over_HTTP_is_stamped_with_the_acting_pharmacy()
    {
        var admin = await _fx.AdminClientAsync();

        var name = $"Audit Probe {Guid.NewGuid():N}";
        await HttpTestHelpers.PostFormAsync(admin, "/Category/AddCategory",
            new Dictionary<string, string>
            {
                ["Name"] = name,
                ["IsActive"] = "true"
            },
            tokenPath: "/Category/CategoriesIndex");

        var stamped = await RunAsync(async sp =>
        {
            var log = sp.GetRequiredService<LogDbContext>();
            return await log.ActivityLogs.AsNoTracking()
                .CountAsync(l => l.Pharmacy_ID == _fx.PharmacyId);
        });

        Assert.True(stamped > 0,
            "No activity-log entry carries the acting pharmacy's id, so the pharmacy's own audit " +
            "screen — which filters on that column — shows nothing at all.");
    }

    // ------------------------------------------------------------------------------------------
    // The activity log over real HTTP.
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("?UserId=1&PageNumber=0")]
    [InlineData("?UserId=1&PageNumber=-5")]
    [InlineData("?UserId=1&PageSize=-1")]
    [InlineData("?UserId=1&PageSize=0")]
    [InlineData("?UserId=1&FromDate=not-a-date")]
    [InlineData("?UserId=1&PageSize=100000000")]
    public async Task The_activity_log_survives_a_hand_edited_query_string(string query)
    {
        var admin = await _fx.AdminClientAsync();

        var response = await admin.GetAsync("/ActivityLog/Index" + query);

        Assert.True((int)response.StatusCode < 500,
            $"/ActivityLog/Index{query} returned {(int)response.StatusCode}. Paging values are " +
            "model-bound straight from the query string; BaseController.NormalizePage and " +
            "NormalizePageSize exist for this and the activity log calls neither.");
    }

    // ------------------------------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------------------------------

    private const string ProbePassword = "ProbeUserPass123";

    private sealed record ProbeUser(HttpClient Client, string Email, int UserId, int RoleId, string ClientIp);

    /// <summary>
    /// Creates a role holding full rights on one ordinary page, a user holding that role, and signs
    /// them in. Every probe gets its own role and user so that revoking one cannot disturb another.
    /// </summary>
    private async Task<ProbeUser> NewProbeUserAsync(
        string label, string clientIp, string? roleName = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{label}-{suffix}@webtest.local";

        var (userId, roleId) = await RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var userManager = sp.GetRequiredService<UserManager<User>>();

            var role = new Role
            {
                Name = roleName ?? $"Probe {label} {suffix}",
                Description = "Created by SessionAuthorityProbes.",
                IsSystemRole = false,
                IsActive = true,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            db.Roles_Custom.Add(role);
            await db.SaveChangesAsync();

            var pages = await db.Pages.Where(p => p.Controller == GuardedController).ToListAsync();
            foreach (var page in pages)
            {
                db.RolePages.Add(new RolePage
                {
                    Role_ID = role.RoleID,
                    Page_ID = page.PageID,
                    CanView = true,
                    CanCreate = true,
                    CanEdit = true,
                    CanDelete = true
                });
            }
            await db.SaveChangesAsync();

            var user = new User
            {
                UserName = email,
                Email = email,
                FullName = $"Probe {label}",
                IsActive = true,
                Pharmacy_ID = _fx.PharmacyId,
                IsPlatformAdmin = false,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            var created = await userManager.CreateAsync(user, ProbePassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("Probe user creation failed: " +
                    string.Join("; ", created.Errors.Select(e => e.Description)));

            db.UserRoles_Custom.Add(new UserRole { User_ID = user.Id, Role_ID = role.RoleID });
            await db.SaveChangesAsync();

            return (user.Id, role.RoleID);
        });

        var client = _fx.Factory.CreateTestClient();
        await LoginAsync(client, email, ProbePassword, clientIp);

        return new ProbeUser(client, email, userId, roleId, clientIp);
    }

    /// <summary>Unticks every permission box for the role, through the role screen's own service.</summary>
    private Task RevokeAllPermissionsAsync(int roleId) => RunAsync(async sp =>
    {
        var roles = sp.GetRequiredService<PharmaCare.Application.Interfaces.Security.IRoleService>();
        var current = await roles.GetPermissionsForRoleAsync(roleId);
        foreach (var p in current)
        {
            p.CanView = false;
            p.CanCreate = false;
            p.CanEdit = false;
            p.CanDelete = false;
        }
        await roles.UpdatePermissionsAsync(roleId, current);
    });

    /// <summary>Activity-log entries whose description names this account.</summary>
    private Task<int> EntriesMentioningAsync(string email) => RunAsync(async sp =>
        await sp.GetRequiredService<LogDbContext>().ActivityLogs.AsNoTracking()
            .CountAsync(l => l.Description != null && l.Description.Contains(email)));

    private Task RunAsync(Func<IServiceProvider, Task> action) =>
        _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, action);

    private Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action) =>
        _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, action);

    private static Dictionary<string, string> Forwarded(string ip) =>
        new() { ["X-Forwarded-For"] = ip };

    private static async Task<string?> TokenFromAsync(HttpClient client, string path, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var html = await response.Content.ReadAsStringAsync();
        var match = System.Text.RegularExpressions.Regex.Match(
            html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, string ip)
    {
        var token = await TokenFromAsync(client, "/Account/Login", ip);

        var response = await HttpTestHelpers.PostFormAsync(client, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["RememberMe"] = "false"
            },
            antiForgeryToken: token,
            extraHeaders: Forwarded(ip));

        var location = response.Headers.Location?.ToString() ?? string.Empty;
        if (response.StatusCode != HttpStatusCode.Redirect && response.StatusCode != HttpStatusCode.Found)
            throw new InvalidOperationException(
                $"Probe login for '{email}' did not redirect (got {(int)response.StatusCode}).");
        if (location.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Probe login for '{email}' bounced back to login.");
    }

    private static Task<HttpResponseMessage> GetAsync(ProbeUser probe, string path) =>
        GetAsync(probe.Client, path, probe.ClientIp);

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
        return client.SendAsync(request);
    }

    /// <summary>A refusal is either the access-denied redirect or a bounce back to login.</summary>
    private static bool IsDenied(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden) return true;
        if (response.StatusCode != HttpStatusCode.Redirect &&
            response.StatusCode != HttpStatusCode.Found) return false;

        var location = response.Headers.Location?.ToString() ?? string.Empty;
        return location.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase)
            || location.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase);
    }

}
