using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Security;

namespace PharmaCare.Infrastructure.Implementations.Security;

/// <summary>
/// THE single implementation of "what may this user do?".
///
/// <para>
/// Both consumers — <c>SessionService</c> (which snapshots the answer into the HTTP session) and
/// <c>AuthService.HasPermissionAsync</c> (which answers ad hoc) — used to carry their own copies of
/// this query, and the copies had drifted apart in three ways that each turned into a defect:
/// neither consulted <c>Role.IsActive</c> (so a deactivated role kept granting), AuthService read
/// only the FIRST RolePage row across a user's roles instead of the union, and AuthService knew
/// nothing about PageUrls. One shared implementation makes those disagreements impossible.
/// </para>
///
/// <para>
/// Rules encoded here: only ACTIVE roles grant anything; a user's effective permission on a page is
/// the union (OR) of what their roles grant; PageUrl aliases inherit the parent page's permissions.
/// </para>
/// </summary>
public static class PermissionResolution
{
    /// <summary>The user's roles that are currently allowed to grant permissions.</summary>
    public static async Task<List<(int RoleId, string Name)>> ActiveRolesAsync(
        PharmaCareDBContext db, int userId)
    {
        var roles = await db.UserRoles_Custom
            .AsNoTracking()
            .Where(ur => ur.User_ID == userId)
            .Join(db.Roles_Custom.Where(r => r.IsActive),
                ur => ur.Role_ID,
                r => r.RoleID,
                (ur, r) => new { r.RoleID, r.Name })
            .ToListAsync();

        return roles.Select(r => (r.RoleID, r.Name)).ToList();
    }

    /// <summary>Effective page permissions for the user, PageUrl aliases included.</summary>
    public static async Task<List<PagePermission>> EffectivePermissionsAsync(
        PharmaCareDBContext db, int userId)
    {
        var roleIds = (await ActiveRolesAsync(db, userId)).Select(r => r.RoleId).ToList();
        return await EffectivePermissionsAsync(db, roleIds);
    }

    /// <summary>Overload for callers that already resolved the active role ids.</summary>
    public static async Task<List<PagePermission>> EffectivePermissionsAsync(
        PharmaCareDBContext db, List<int> activeRoleIds)
    {
        var pagePermissions = await db.RolePages
            .AsNoTracking()
            .Where(rp => activeRoleIds.Contains(rp.Role_ID))
            .Select(rp => new
            {
                rp.Page_ID,
                rp.Page!.Controller,
                rp.Page.Action,
                rp.CanView,
                rp.CanCreate,
                rp.CanEdit,
                rp.CanDelete
            })
            .ToListAsync();

        // Union across roles: holding a grant through ANY role is holding it.
        var aggregated = pagePermissions
            .GroupBy(p => new { p.Page_ID, p.Controller, p.Action })
            .Select(g => new PagePermission
            {
                PageId = g.Key.Page_ID,
                Controller = g.Key.Controller ?? string.Empty,
                Action = g.Key.Action ?? string.Empty,
                CanView = g.Any(p => p.CanView),
                CanCreate = g.Any(p => p.CanCreate),
                CanEdit = g.Any(p => p.CanEdit),
                CanDelete = g.Any(p => p.CanDelete)
            })
            .ToList();

        // PageUrl aliases (extra controller/action routes belonging to a page) inherit the parent
        // page's permissions, unless an explicit permission already covers that route.
        var accessiblePageIds = aggregated.Select(p => p.PageId).ToList();
        var pageUrls = await db.PageUrls
            .AsNoTracking()
            .Where(pu => accessiblePageIds.Contains(pu.Page_ID))
            .ToListAsync();

        foreach (var pageUrl in pageUrls)
        {
            var parent = aggregated.FirstOrDefault(p => p.PageId == pageUrl.Page_ID);
            if (parent == null) continue;

            var exists = aggregated.Any(p =>
                string.Equals(p.Controller, pageUrl.Controller, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Action, pageUrl.Action, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                aggregated.Add(new PagePermission
                {
                    PageId = pageUrl.Page_ID,
                    Controller = pageUrl.Controller,
                    Action = pageUrl.Action,
                    CanView = parent.CanView,
                    CanCreate = parent.CanCreate,
                    CanEdit = parent.CanEdit,
                    CanDelete = parent.CanDelete
                });
            }
        }

        return aggregated;
    }
}
