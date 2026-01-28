using PharmaCare.Domain.Interfaces;

namespace PharmaCare.Infrastructure;

/// <summary>
/// Store context for multi-store/multi-tenancy support
/// </summary>
public class StoreContext : IStoreContext
{
    public int? CurrentStoreId { get; private set; }
    public bool IsAdmin { get; private set; }

    public void SetContext(int? storeId, bool isAdmin)
    {
        CurrentStoreId = storeId;
        IsAdmin = isAdmin;
    }
}
