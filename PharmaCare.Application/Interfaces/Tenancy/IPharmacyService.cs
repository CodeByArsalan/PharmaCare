using PharmaCare.Domain.Entities.Tenancy;

namespace PharmaCare.Application.Interfaces.Tenancy;

/// <summary>
/// Read/management access to pharmacies (tenants). Used by the login flow (status checks) and
/// the platform super-admin area. Pharmacy is not tenant-scoped, so this service sees all rows.
/// </summary>
public interface IPharmacyService
{
    Task<IEnumerable<Pharmacy>> GetAllAsync();
    Task<Pharmacy?> GetByIdAsync(int id);

    /// <summary>True when the pharmacy exists, is active, and not suspended (may log in / operate).</summary>
    Task<bool> IsOperationalAsync(int pharmacyId);

    Task<bool> CodeExistsAsync(string code, int? excludeId = null);

    /// <summary>Sets Status to Active/Suspended. Suspended pharmacies cannot log in.</summary>
    Task<bool> SetStatusAsync(int pharmacyId, string status, int userId);
}
