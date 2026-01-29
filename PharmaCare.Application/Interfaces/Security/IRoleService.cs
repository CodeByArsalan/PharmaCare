using PharmaCare.Application.DTOs.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces.Security;

/// <summary>
/// Service interface for role and permission management.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Get all roles.
    /// </summary>
    Task<List<Role>> GetAllRolesAsync();

    /// <summary>
    /// Get a role by ID.
    /// </summary>
    Task<Role?> GetRoleByIdAsync(int id);

    /// <summary>
    /// Create a new role.
    /// </summary>
    Task<bool> CreateRoleAsync(Role role, int createdBy);

    /// <summary>
    /// Update an existing role.
    /// </summary>
    Task<bool> UpdateRoleAsync(Role role, int updatedBy);

    /// <summary>
    /// Toggle role active status (only for non-system roles).
    /// </summary>
    Task<bool> ToggleRoleStatusAsync(int id, int updatedBy);

    /// <summary>
    /// Get all pages with their current permission state for a role.
    /// </summary>
    Task<List<RolePagePermissionDTO>> GetPermissionsForRoleAsync(int roleId);

    /// <summary>
    /// Update permissions for a role.
    /// </summary>
    Task<bool> UpdatePermissionsAsync(int roleId, List<RolePagePermissionDTO> permissions);
}
