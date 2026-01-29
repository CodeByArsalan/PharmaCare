using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces.Security;

/// <summary>
/// Repository interface for RolePage (permissions) entity operations.
/// </summary>
public interface IRolePageRepository : IRepository<RolePage>
{
    /// <summary>
    /// Get all permissions for a role as dictionary keyed by PageID.
    /// </summary>
    Task<Dictionary<int, RolePage>> GetPermissionsByRoleIdAsync(int roleId);

    /// <summary>
    /// Remove all permissions for a role.
    /// </summary>
    Task RemoveByRoleIdAsync(int roleId);
}
