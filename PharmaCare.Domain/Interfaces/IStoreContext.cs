namespace PharmaCare.Domain.Interfaces;

public interface IStoreContext
{
    int? CurrentStoreId { get; }
    bool IsAdmin { get; }
    void SetContext(int? storeId, bool isAdmin);
}
