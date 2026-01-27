using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Application.Interfaces.AccountManagement;

/// <summary>
/// Service interface for managing account mappings
/// </summary>
public interface IAccountMappingService
{
    /// <summary>
    /// Get all mappings, optionally filtered by party type
    /// </summary>
    Task<List<AccountMapping>> GetMappings(string? partyType = null);

    /// <summary>
    /// Get a mapping by its ID
    /// </summary>
    Task<AccountMapping?> GetMappingById(int id);

    /// <summary>
    /// Get mapping for a specific party type
    /// </summary>
    Task<AccountMapping?> GetMappingByPartyType(string partyType);

    /// <summary>
    /// Create a new account mapping
    /// </summary>
    Task<bool> CreateMapping(AccountMapping mapping, int loginUserId);

    /// <summary>
    /// Update an existing mapping
    /// </summary>
    Task<bool> UpdateMapping(AccountMapping mapping, int loginUserId);

    /// <summary>
    /// Delete a mapping
    /// </summary>
    Task<bool> DeleteMapping(int id);

    /// <summary>
    /// Get the account ID for a party type (used in transactions)
    /// </summary>
    Task<int?> GetAccountIdForPartyType(string partyType);

    /// <summary>
    /// Get all heads for dropdown
    /// </summary>
    Task<List<Head>> GetHeads();

    /// <summary>
    /// Get subheads by head ID for cascading dropdown
    /// </summary>
    Task<List<Subhead>> GetSubheadsByHead(int headId);

    /// <summary>
    /// Get accounts by subhead ID for cascading dropdown
    /// </summary>
    Task<List<ChartOfAccount>> GetAccountsBySubhead(int subheadId);
}
