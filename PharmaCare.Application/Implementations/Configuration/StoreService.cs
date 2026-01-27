using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PharmaCare.Application.Implementations.Configuration;

public class StoreService(IRepository<Store> _storeRepo, IRepository<SystemUser> _userRepo) : IStoreService
{

    public async Task<List<Store>> GetStoresByLoginUserID(int loginUserId)
    {
        var user = await _userRepo.FindByCondition(u => u.Id == loginUserId)
            .Include(u => u.Store)
            .FirstOrDefaultAsync();

        if (user == null) return new List<Store>();

        // Admin sees all stores
        if (user.UserType_ID == 1)
        {
            var allStores = await _storeRepo.GetAll();
            return allStores.ToList();
        }

        // Branch user sees only their assigned store
        if (user.Store_ID.HasValue)
        {
            var store = await _storeRepo.FindByCondition(s => s.StoreID == user.Store_ID.Value).FirstOrDefaultAsync();
            return store != null ? new List<Store> { store } : new List<Store>();
        }

        return new List<Store>();
    }
    public async Task<Store> GetStoreById(int id)
    {
        return _storeRepo.GetById(id);
    }
    public async Task<bool> CreateStore(Store store, int loginUserId)
    {
        return await _storeRepo.Insert(store);
    }
    public async Task<bool> UpdateStore(Store store, int loginUserId)
    {
        return await _storeRepo.Update(store);
    }
    public async Task<bool> DeleteStore(int id, int loginUserId)
    {
        var store = _storeRepo.GetById(id);
        if (store == null) return false;

        return await _storeRepo.Delete(store);
    }
}
