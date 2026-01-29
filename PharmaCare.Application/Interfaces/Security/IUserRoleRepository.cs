using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces.Security;

/// <summary>
/// Repository interface for UserRole entity operations.
/// </summary>
public interface IUserRoleRepository : IRepository<UserRole>
{
    /// <summary>
    /// Get role IDs for a specific user.
    /// </summary>
    Task<List<int>> GetRoleIdsByUserIdAsync(int userId);

    /// <summary>
    /// Remove all roles for a user.
    /// </summary>
    Task RemoveByUserIdAsync(int userId);
}
