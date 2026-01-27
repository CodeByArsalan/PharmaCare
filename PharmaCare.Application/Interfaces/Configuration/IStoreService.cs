using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

public interface IStoreService
{
    Task<List<Store>> GetStoresByLoginUserID(int loginUserId);
    Task<Store> GetStoreById(int id);
    Task<bool> CreateStore(Store store, int loginUserId);
    Task<bool> UpdateStore(Store store, int loginUserId);
    Task<bool> DeleteStore(int id, int loginUserId);
}
