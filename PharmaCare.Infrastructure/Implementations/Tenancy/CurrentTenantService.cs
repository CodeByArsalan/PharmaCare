using Microsoft.AspNetCore.Http;
using PharmaCare.Application.Interfaces.Tenancy;

namespace PharmaCare.Infrastructure.Implementations.Tenancy;

/// <summary>
/// Resolves the current pharmacy (tenant) without touching the DbContext or session service,
/// to avoid a circular dependency (DbContext -> ICurrentTenant). Resolution order:
///   1. an explicit override (login, provisioning, background jobs, seeding), else
///   2. the "Pharmacy_ID" claim on the authenticated principal (present on every request after login).
/// Registered as scoped so the override lives for one request/scope.
/// </summary>
public sealed class CurrentTenantService : ICurrentTenant
{
    /// <summary>Claim type carrying the pharmacy id, added at sign-in.</summary>
    public const string PharmacyClaimType = "Pharmacy_ID";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _override;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? TenantId
    {
        get
        {
            if (_override.HasValue)
            {
                return _override;
            }

            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(PharmacyClaimType)?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : null;
        }
    }

    public bool HasValue => TenantId.HasValue;

    public void SetTenant(int pharmacyId)
    {
        if (pharmacyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pharmacyId), "Pharmacy id must be positive.");
        }

        _override = pharmacyId;
    }

    public IDisposable BeginScope(int pharmacyId)
    {
        if (pharmacyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pharmacyId), "Pharmacy id must be positive.");
        }

        var previous = _override;
        _override = pharmacyId;
        return new TenantScope(() => _override = previous);
    }

    private sealed class TenantScope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public TenantScope(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _onDispose();
        }
    }
}
