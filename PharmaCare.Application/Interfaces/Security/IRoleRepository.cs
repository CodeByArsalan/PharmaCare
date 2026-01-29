using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces.Security;

/// <summary>
/// Repository interface for Role entity operations.
/// </summary>
public interface IRoleRepository : IRepository<Role>
{
    /// <summary>
    /// Get all roles ordered by name.
    /// </summary>
    Task<List<Role>> GetAllOrderedAsync();

    /// <summary>
    /// Get active roles for dropdown.
    /// </summary>
    Task<List<Role>> GetActiveRolesAsync();
}
