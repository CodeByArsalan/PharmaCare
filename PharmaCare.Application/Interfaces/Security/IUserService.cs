using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces.Security;

/// <summary>
/// Service interface for user management operations.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Get all users with their roles info.
    /// </summary>
    Task<List<User>> GetAllUsersAsync();

    /// <summary>
    /// Get a user by ID with related data.
    /// </summary>
    Task<User?> GetUserByIdAsync(int id);

    /// <summary>
    /// Get role IDs assigned to a user.
    /// </summary>
    Task<List<int>> GetUserRoleIdsAsync(int userId);

    /// <summary>
    /// Create a new user with password and assigned roles.
    /// </summary>
    Task<(bool Success, string? Error)> CreateUserAsync(User user, string password, List<int> roleIds, int createdBy);

    /// <summary>
    /// Update an existing user and their role assignments.
    /// </summary>
    Task<(bool Success, string? Error)> UpdateUserAsync(User user, string? newPassword, List<int> roleIds, int updatedBy);

    /// <summary>
    /// Toggle user active status.
    /// </summary>
    Task<bool> ToggleUserStatusAsync(int id, int updatedBy);

    /// <summary>
    /// Get all roles for dropdown.
    /// </summary>
    Task<List<Role>> GetRolesForDropdownAsync();
}
