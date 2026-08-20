using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PharmaCare.Web.Filters;

namespace PharmaCare.WebTests;

/// <summary>
/// Standing guard over the authorization model rather than over one endpoint.
///
/// <para>
/// <c>PageAuthorizationFilter</c> now refuses a state-changing request whose required permission
/// cannot be established, so a mutating endpoint that declares nothing and whose name says nothing
/// is DENIED at runtime. That is the safe failure, but it is still a failure — the endpoint is
/// simply broken for every user. This test walks the app's own routing table and reports those
/// endpoints up front, so the omission surfaces here instead of as a support call.
/// </para>
/// </summary>
[Collection(WebCollection.Name)]
public class PermissionDeclarationGuardTest
{
    private readonly WebTestFixture _fx;
    public PermissionDeclarationGuardTest(WebTestFixture fx) => _fx = fx;

    // The rule itself, tested directly. The endpoint-level probes in RbacPermissionMatrixProbes
    // pass on the strength of the explicit annotations those five endpoints now carry, so they do
    // NOT exercise this fallback — it is what protects the endpoint nobody has annotated yet.
    [Theory]
    [InlineData("Void")]
    [InlineData("Approve")]
    [InlineData("ReverseJournalVoucher")]
    [InlineData("Open")]
    [InlineData("Close")]
    [InlineData("SavePermissions")]
    [InlineData("MakePayment")]
    [InlineData("SomethingNobodyHasThoughtOfYet")]
    public void An_undeclared_mutating_action_resolves_to_no_permission(string actionName)
    {
        var resolved = PageAuthorizationFilter.ResolvePermissionType(
            declared: null, httpMethod: "POST", actionName: actionName);

        Assert.Null(resolved);
    }

    [Theory]
    [InlineData("AddSale", "create")]
    [InlineData("EditPurchase", "edit")]
    [InlineData("ToggleStatus", "delete")]
    [InlineData("DeleteExpenseCategory", "delete")]
    public void An_action_whose_name_states_its_effect_still_resolves(string actionName, string expected)
    {
        Assert.Equal(expected, PageAuthorizationFilter.ResolvePermissionType(null, "POST", actionName));
    }

    [Fact]
    public void An_explicit_declaration_always_wins_over_the_name()
    {
        // "AddSale" reads as create; a declaration of delete must override that, which is what
        // lets a destructive endpoint keep a name that does not advertise the danger.
        Assert.Equal("delete", PageAuthorizationFilter.ResolvePermissionType("delete", "POST", "AddSale"));
    }

    [Fact]
    public void A_read_with_no_declaration_is_still_treated_as_a_read()
    {
        Assert.Equal("view", PageAuthorizationFilter.ResolvePermissionType(null, "GET", "ViewSale"));
    }

    [Fact]
    public void Every_mutating_endpoint_declares_the_permission_it_needs()
    {
        var eds = _fx.Factory.Services.GetRequiredService<EndpointDataSource>();

        var undeclared = new StringBuilder();
        var checkedCount = 0;

        foreach (var endpoint in eds.Endpoints)
        {
            var cad = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (cad == null) continue;

            // Anonymous endpoints, the login/dashboard exemptions and policy-gated areas are all
            // outside the page-permission system — the filter skips them for the same reasons.
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null) continue;
            if (string.Equals(cad.ControllerName, "Account", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(cad.ControllerName, "Home", StringComparison.OrdinalIgnoreCase)) continue;
            if (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => !string.IsNullOrEmpty(a.Policy))) continue;

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                          ?? new[] { "GET" };

            foreach (var method in methods.Where(PageAuthorizationFilter.IsStateChanging))
            {
                checkedCount++;

                var declared = cad.MethodInfo
                    .GetCustomAttributes(typeof(LinkedToPageAttribute), false)
                    .OfType<LinkedToPageAttribute>()
                    .FirstOrDefault()?.PermissionType;

                var resolved = PageAuthorizationFilter.ResolvePermissionType(declared, method, cad.ActionName);

                if (resolved == null)
                {
                    undeclared.AppendLine(
                        $"  {method} {cad.ControllerName}/{cad.ActionName} — add " +
                        "[LinkedToPage(\"<Controller>\", \"<IndexAction>\", PermissionType = \"create|edit|delete\")]");
                }
            }
        }

        Assert.True(checkedCount > 0, "Endpoint enumeration found no state-changing actions — routing broke.");
        Assert.True(undeclared.Length == 0,
            $"These mutating endpoints do not establish a required permission, so the filter will " +
            $"refuse them for every user ({checkedCount} checked):\n{undeclared}");
    }
}
