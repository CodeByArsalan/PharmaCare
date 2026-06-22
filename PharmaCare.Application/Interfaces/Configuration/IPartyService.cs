using PharmaCare.Application.DTOs;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

/// <summary>
/// Service interface for Party entity operations
/// </summary>
public interface IPartyService
{
    Task<IEnumerable<Party>> GetAllAsync();

    /// <summary>
    /// Server-side paged + filtered list of parties for the index grid.
    /// </summary>
    Task<PagedResult<Party>> GetPagedAsync(string? search, string? partyType, bool? isActive, int page, int pageSize);
    Task<Party?> GetByIdAsync(int id);
    Task<Party> CreateAsync(Party party, int userId);
    Task<bool> UpdateAsync(Party party, int userId);
    /// <summary>
    /// Toggles the active status of a party
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);

    /// <summary>
    /// Gets a party with their linked Account for accounting operations
    /// </summary>
    Task<Party?> GetByIdWithAccountAsync(int id);
}
