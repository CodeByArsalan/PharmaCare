namespace PharmaCare.Application.Interfaces.Tenancy;

/// <summary>
/// Ambient accessor for the current pharmacy (tenant). Resolved per request/scope and used by
/// the DbContext to (a) apply the global query filter on every tenant-owned entity and
/// (b) stamp Pharmacy_ID on inserts. An explicit override (set during login, provisioning,
/// background jobs, or seeding) always wins over ambient resolution.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>The current pharmacy id, or null when no tenant is in context (e.g. an
    /// unauthenticated request or a platform super-admin). A null tenant makes tenant-scoped
    /// reads return no rows and tenant-scoped writes throw — the safe default.</summary>
    int? TenantId { get; }

    bool HasValue { get; }

    /// <summary>Force the tenant for the remainder of this scope (login, provisioning, seeding).</summary>
    void SetTenant(int pharmacyId);

    /// <summary>Run a block scoped to an explicit tenant, restoring the previous value on dispose.
    /// Use for provisioning a new pharmacy or seeding under a known tenant.</summary>
    IDisposable BeginScope(int pharmacyId);
}
