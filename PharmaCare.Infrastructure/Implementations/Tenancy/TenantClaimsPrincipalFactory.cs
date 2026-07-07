using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations.Tenancy;

/// <summary>
/// Adds the pharmacy (tenant) claim to the authentication cookie at sign-in, so every
/// subsequent request can resolve the current tenant from the principal without a DB hit.
/// Also emits an IsPlatformAdmin claim used to route platform super-admins.
/// </summary>
public class TenantClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
{
    public const string PlatformAdminClaimType = "IsPlatformAdmin";

    public TenantClaimsPrincipalFactory(
        UserManager<User> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.Pharmacy_ID is int pharmacyId && pharmacyId > 0)
        {
            identity.AddClaim(new Claim(CurrentTenantService.PharmacyClaimType, pharmacyId.ToString()));
        }

        if (user.IsPlatformAdmin)
        {
            identity.AddClaim(new Claim(PlatformAdminClaimType, "true"));
        }

        return identity;
    }
}
