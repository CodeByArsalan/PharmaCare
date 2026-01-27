using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

public interface IPartyService
{
    /// <summary>
    /// Get all active parties
    /// </summary>
    Task<List<Party>> GetParties();

    /// <summary>
    /// Get parties by type (Customer, Supplier, Both)
    /// </summary>
    Task<List<Party>> GetPartiesByType(string partyType);

    /// <summary>
    /// Get party by ID
    /// </summary>
    Task<Party?> GetPartyById(int id);

    /// <summary>
    /// Create a new party
    /// </summary>
    Task<bool> CreateParty(Party party, int userId);

    /// <summary>
    /// Update existing party
    /// </summary>
    Task<bool> UpdateParty(Party party, int userId);

    /// <summary>
    /// Delete (deactivate) a party
    /// </summary>
    Task<bool> DeleteParty(int id);

    /// <summary>
    /// Get customers (parties with type Customer or Both)
    /// </summary>
    Task<List<Party>> GetCustomers();

    /// <summary>
    /// Get suppliers (parties with type Supplier or Both)
    /// </summary>
    Task<List<Party>> GetSuppliers();
}
