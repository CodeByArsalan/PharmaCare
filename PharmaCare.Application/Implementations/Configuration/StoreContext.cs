using PharmaCare.Domain.Interfaces;

namespace PharmaCare.Application.Implementations.Configuration;

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
