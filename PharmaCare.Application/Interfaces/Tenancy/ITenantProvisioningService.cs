namespace PharmaCare.Application.Interfaces.Tenancy;

/// <summary>
/// Input for provisioning a brand-new pharmacy (tenant) plus its first admin user.
/// </summary>
public class ProvisionPharmacyRequest
{
    public string PharmacyName { get; set; } = string.Empty;
    public string PharmacyCode { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }

    public string AdminFullName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

public class ProvisionResult
{
    public bool Success { get; set; }
    public int PharmacyId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Creates a new pharmacy and seeds the complete per-tenant reference set required for it to
/// operate on day one: chart of accounts (with well-known codes), price types, profit settings,
/// an open financial period, a default Admin role granted every page, and the first admin user.
/// All seeded rows are stamped with the new pharmacy's id via an explicit tenant scope.
/// </summary>
public interface ITenantProvisioningService
{
    Task<ProvisionResult> ProvisionAsync(ProvisionPharmacyRequest request, int actingUserId);
}
