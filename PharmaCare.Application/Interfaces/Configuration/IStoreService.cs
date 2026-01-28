using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

/// <summary>
/// Service interface for Store entity operations
/// </summary>
public interface IStoreService
{
    /// <summary>
    /// Gets all active (non-deleted) stores
    /// </summary>
    Task<IEnumerable<Store>> GetAllAsync();

    /// <summary>
    /// Gets a store by its ID
    /// </summary>
    Task<Store?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new store
    /// </summary>
    Task<Store> CreateAsync(Store store, int userId);

    /// <summary>
    /// Updates an existing store
    /// </summary>
    Task<bool> UpdateAsync(Store store, int userId);

    /// <summary>
    /// Toggles the active status of a store
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);

    /// <summary>
    /// Checks if a store exists
    /// </summary>
    Task<bool> ExistsAsync(int id);
}
