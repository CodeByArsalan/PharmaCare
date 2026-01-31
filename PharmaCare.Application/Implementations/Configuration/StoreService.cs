using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Implementations.Configuration;

/// <summary>
/// Service implementation for Store entity operations
/// </summary>
public class StoreService : IStoreService
{
    private readonly IRepository<Store> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public StoreService(IRepository<Store> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Store>> GetAllAsync()
    {
        var stores = await _repository.GetAllAsync();
        return stores.OrderByDescending(s => s.IsActive).ThenBy(s => s.Name);
    }

    /// <inheritdoc />
    public async Task<Store?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task<Store> CreateAsync(Store store, int userId)
    {
        store.CreatedAt = DateTime.Now;
        store.CreatedBy = userId;
        store.IsActive = true;

        await _repository.AddAsync(store);
        await _unitOfWork.SaveChangesAsync();
        
        return store;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Store store, int userId)
    {
        var existing = await GetByIdAsync(store.StoreID);
        if (existing == null)
            return false;

        existing.Name = store.Name;
        existing.Address = store.Address;
        existing.Phone = store.Phone;

        existing.IsActive = store.IsActive;
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var store = await GetByIdAsync(id);
        if (store == null)
            return false;

        store.IsActive = !store.IsActive;
        store.UpdatedAt = DateTime.Now;
        store.UpdatedBy = userId;

        _repository.Update(store);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(int id)
    {
        return await _repository.AnyAsync(s => s.StoreID == id);
    }
}
