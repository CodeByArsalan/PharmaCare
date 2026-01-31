using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

/// <summary>
/// Service interface for Party entity operations
/// </summary>
public interface IPartyService
{
    Task<IEnumerable<Party>> GetAllAsync();
    Task<Party?> GetByIdAsync(int id);
    Task<Party> CreateAsync(Party party, int userId);
    Task<bool> UpdateAsync(Party party, int userId);
    /// <summary>
    /// Toggles the active status of a party
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);
}
