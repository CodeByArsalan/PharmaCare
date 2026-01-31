using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Implementations.Security;

/// <summary>
/// Implementation of role and permission management service.
/// </summary>
public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPageRepository _pageRepository;
    private readonly IRolePageRepository _rolePageRepository;

    public RoleService(
        IRoleRepository roleRepository,
        IPageRepository pageRepository,
        IRolePageRepository rolePageRepository)
    {
        _roleRepository = roleRepository;
        _pageRepository = pageRepository;
        _rolePageRepository = rolePageRepository;
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _roleRepository.GetAllOrderedAsync();
    }

    public async Task<Role?> GetRoleByIdAsync(int id)
    {
        return await _roleRepository.GetByIdAsync(id);
    }

    public async Task<bool> CreateRoleAsync(Role role, int createdBy)
    {
        role.CreatedAt = DateTime.Now;
        role.CreatedBy = createdBy;
        role.IsActive = true;

        await _roleRepository.AddAsync(role);
        await _roleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateRoleAsync(Role role, int updatedBy)
    {
        var existingRole = await _roleRepository.GetByIdAsync(role.RoleID);
        if (existingRole == null) return false;

        existingRole.Name = role.Name;
        existingRole.Description = role.Description;
        existingRole.UpdatedAt = DateTime.Now;
        existingRole.UpdatedBy = updatedBy;

        await _roleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleRoleStatusAsync(int id, int updatedBy)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null || role.IsSystemRole) return false;

        role.IsActive = !role.IsActive;
        role.UpdatedAt = DateTime.Now;
        role.UpdatedBy = updatedBy;

        await _roleRepository.SaveChangesAsync();
        return true;
    }

    public async Task<List<RolePagePermissionDTO>> GetPermissionsForRoleAsync(int roleId)
    {
        // Get all active pages
        var pages = await _pageRepository.GetActiveOrderedAsync();

        // Get existing permissions for this role
        var existingPermissions = await _rolePageRepository.GetPermissionsByRoleIdAsync(roleId);

        // Build result list
        var result = new List<RolePagePermissionDTO>();
        foreach (var page in pages)
        {
            var hasPermission = existingPermissions.TryGetValue(page.PageID, out var rolePage);
            
            var parentPage = page.Parent_ID.HasValue 
                ? pages.FirstOrDefault(p => p.PageID == page.Parent_ID) 
                : null;

            result.Add(new RolePagePermissionDTO
            {
                PageId = page.PageID,
                PageTitle = page.Title,
                ParentId = page.Parent_ID,
                ParentTitle = parentPage?.Title,
                Controller = page.Controller,
                Action = page.Action,
                DisplayOrder = page.DisplayOrder,
                CanView = hasPermission && rolePage!.CanView,
                CanCreate = hasPermission && rolePage!.CanCreate,
                CanEdit = hasPermission && rolePage!.CanEdit,
                CanDelete = hasPermission && rolePage!.CanDelete
            });
        }

        return result;
    }

    public async Task<bool> UpdatePermissionsAsync(int roleId, List<RolePagePermissionDTO> permissions)
    {
        // Get existing permissions
        var existingPermissions = await _rolePageRepository.GetPermissionsByRoleIdAsync(roleId);

        foreach (var perm in permissions)
        {
            var hasNewPermissions = perm.CanView || perm.CanCreate || perm.CanEdit || perm.CanDelete;
            
            // Case 1: Permission exists
            if (existingPermissions.TryGetValue(perm.PageId, out var existingRolePage))
            {
                if (hasNewPermissions)
                {
                    // Update if tracking or values changed
                    if (existingRolePage.CanView != perm.CanView ||
                        existingRolePage.CanCreate != perm.CanCreate ||
                        existingRolePage.CanEdit != perm.CanEdit ||
                        existingRolePage.CanDelete != perm.CanDelete)
                    {
                        existingRolePage.CanView = perm.CanView;
                        existingRolePage.CanCreate = perm.CanCreate;
                        existingRolePage.CanEdit = perm.CanEdit;
                        existingRolePage.CanDelete = perm.CanDelete;
                        _rolePageRepository.Update(existingRolePage);
                    }
                }
                else
                {
                    // If all permissions revoked, remove the record
                    _rolePageRepository.Remove(existingRolePage);
                }
            }
            // Case 2: New Permission being granted
            else if (hasNewPermissions)
            {
                await _rolePageRepository.AddAsync(new RolePage
                {
                    Role_ID = roleId,
                    Page_ID = perm.PageId,
                    CanView = perm.CanView,
                    CanCreate = perm.CanCreate,
                    CanEdit = perm.CanEdit,
                    CanDelete = perm.CanDelete
                });
            }
            // Case 3: Permission doesn't exist and not requested -> Do nothing
        }

        await _rolePageRepository.SaveChangesAsync();
        return true;
    }
}
