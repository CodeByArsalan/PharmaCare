using Microsoft.EntityFrameworkCore;
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
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RoleService(
        IRoleRepository roleRepository,
        IPageRepository pageRepository,
        IRolePageRepository rolePageRepository,
        IUserRoleRepository userRoleRepository,
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _pageRepository = pageRepository;
        _rolePageRepository = rolePageRepository;
        _userRoleRepository = userRoleRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _roleRepository.GetAllOrderedAsync();
    }

    public async Task<Role?> GetRoleByIdAsync(int id)
    {
        return await GetOwnedRoleAsync(id);
    }

    /// <summary>
    /// Resolves a role id to one of THIS pharmacy's roles, or null. Role ids reach these methods
    /// straight from the client with no ownership check upstream, so the lookup must stay
    /// tenant-scoped — this states that requirement locally rather than resting on the repository.
    /// </summary>
    private async Task<Role?> GetOwnedRoleAsync(int id)
    {
        return await _roleRepository.Query().FirstOrDefaultAsync(r => r.RoleID == id);
    }

    public async Task<bool> CreateRoleAsync(Role role, int createdBy)
    {
        role.CreatedAt = AppTime.Now;
        role.CreatedBy = createdBy;
        role.IsActive = true;

        await _roleRepository.AddAsync(role);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateRoleAsync(Role role, int updatedBy)
    {
        var existingRole = await GetOwnedRoleAsync(role.RoleID);
        if (existingRole == null) return false;

        // A system role's NAME is part of the tenant's plumbing — provisioning created it, other
        // code recognises it, and renaming it is how "Administrator" quietly stops meaning
        // anything. ToggleRoleStatusAsync already refuses system roles; the same rule has to hold
        // here or the toggle's guard is one rename away from irrelevant. Description edits are fine.
        if (existingRole.IsSystemRole &&
            !string.Equals(existingRole.Name, role.Name, StringComparison.Ordinal))
        {
            return false;
        }

        existingRole.Name = role.Name;
        existingRole.Description = role.Description;
        existingRole.UpdatedAt = AppTime.Now;
        existingRole.UpdatedBy = updatedBy;

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleRoleStatusAsync(int id, int updatedBy)
    {
        var role = await GetOwnedRoleAsync(id);
        if (role == null || role.IsSystemRole) return false;

        role.IsActive = !role.IsActive;
        role.UpdatedAt = AppTime.Now;
        role.UpdatedBy = updatedBy;

        // Toggling a role changes what its holders may do RIGHT NOW — permission resolution only
        // honours active roles — so their live sessions must rebuild their snapshots.
        await BumpHoldersPermissionsStampAsync(id);

        await _unitOfWork.SaveChangesAsync();
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
        // The role id arrives unencrypted from the permissions form with no ownership check upstream.
        // Writing straight through would stamp RolePage rows for this pharmacy that point at another
        // pharmacy's role.
        var role = await GetOwnedRoleAsync(roleId);
        if (role is null)
            return false;

        // The system Administrator role keeps everything, always. The permissions form deletes the
        // RolePage row for any page left unticked, so one submit with the boxes cleared removed the
        // tenant's ONLY route back to this very screen — an unrecoverable self-lockout, because the
        // pharmacy administers itself and nothing outside it can put the rows back.
        if (role.IsSystemRole)
        {
            throw new InvalidOperationException(
                "The system Administrator role always holds every permission; it cannot be edited.");
        }

        // Page ids arrive from the form as plain integers. An unknown id used to reach SQL and die
        // as a foreign-key violation — a generic 500. It is either tampering or a stale form, and
        // in both cases the row grants nothing real, so it is dropped.
        var validPageIds = (await _pageRepository.Query()
            .Select(p => p.PageID)
            .ToListAsync()).ToHashSet();

        // Get existing permissions
        var existingPermissions = await _rolePageRepository.GetPermissionsByRoleIdAsync(roleId);

        foreach (var perm in permissions)
        {
            if (!validPageIds.Contains(perm.PageId))
                continue;

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

        // What this role grants just changed; every holder's live session must rebuild its
        // permission snapshot rather than keep answering from the sign-in copy.
        await BumpHoldersPermissionsStampAsync(roleId);

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Rotates the <see cref="User.PermissionsStamp"/> of every user holding <paramref name="roleId"/>.
    /// SessionInitializationMiddleware compares the stamp per request, so this is what makes a
    /// permission change reach an already-signed-in user. Saved by the caller's SaveChanges.
    /// </summary>
    private async Task BumpHoldersPermissionsStampAsync(int roleId)
    {
        var userIds = await _userRoleRepository.Query()
            .Where(ur => ur.Role_ID == roleId)
            .Select(ur => ur.User_ID)
            .ToListAsync();

        if (userIds.Count == 0) return;

        var users = await _userRepository.Query()
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        foreach (var user in users)
        {
            user.PermissionsStamp = Guid.NewGuid().ToString("N");
        }
    }
}
