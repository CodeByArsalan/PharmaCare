using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces;
using PharmaCare.Infrastructure.Implementations.Security;

namespace PharmaCare.Infrastructure.Implementations;

/// <summary>
/// Session service implementation that manages user context and page permissions.
/// Uses JSON serialization to store data in the HTTP session.
/// </summary>
public class SessionService : ISessionService
{
    private const string UserSessionKey = "UserSession";
    private const string PagePermissionsKey = "PagePermissions";
    private const string SidebarMenuKey = "SidebarMenu";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PharmaCareDBContext _context;

    public SessionService(
        IHttpContextAccessor httpContextAccessor,
        PharmaCareDBContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    /// <inheritdoc />
    public async Task InitializeSessionAsync(int userId)
    {
        if (Session == null) return;

        // Fetch user's roles and basic info using AsNoTracking for speed
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return;

        // Only ACTIVE roles grant anything; a user's grant on a page is the union across their
        // roles; PageUrl aliases inherit the parent page. One shared implementation of those rules
        // (PermissionResolution) serves this snapshot and AuthService alike.
        var userRoles = await PermissionResolution.ActiveRolesAsync(_context, userId);
        var roleIds = userRoles.Select(r => r.RoleId).ToList();
        var aggregatedPermissions = await PermissionResolution.EffectivePermissionsAsync(_context, roleIds);

        // Resolve the owning pharmacy name (Pharmacies is not tenant-filtered).
        string? pharmacyName = null;
        if (user.Pharmacy_ID.HasValue)
        {
            pharmacyName = await _context.Pharmacies
                .AsNoTracking()
                .Where(p => p.PharmacyID == user.Pharmacy_ID.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();
        }

        // Create user session info
        var userSession = new UserSessionInfo
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Pharmacy_ID = user.Pharmacy_ID,
            PharmacyName = pharmacyName,
            IsPlatformAdmin = user.IsPlatformAdmin,
            RoleIds = roleIds,
            RoleNames = userRoles.Select(r => r.Name).ToList(),
            // The permissions version this snapshot was built from — compared per request by
            // SessionInitializationMiddleware; mismatch forces a rebuild.
            PermissionsStamp = user.PermissionsStamp
        };

        // Fetch sidebar menu items
        var sidebarPageIds = aggregatedPermissions
            .Where(p => p.CanView)
            .Select(p => p.PageId)
            .ToHashSet();

        var allPages = await _context.Pages
            .AsNoTracking()
            .Where(p => p.IsActive && p.IsVisible)
            .OrderBy(p => p.Parent_ID)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();

        // Build hierarchical menu
        var sidebarMenu = BuildSidebarMenu(allPages, sidebarPageIds);

        // Store in session
        Session.SetString(UserSessionKey, JsonSerializer.Serialize(userSession));
        Session.SetString(PagePermissionsKey, JsonSerializer.Serialize(aggregatedPermissions));
        Session.SetString(SidebarMenuKey, JsonSerializer.Serialize(sidebarMenu));
    }

    /// <inheritdoc />
    public void ClearSession()
    {
        // Clear EVERYTHING, not just our three keys. Removing named keys leaves the session alive
        // under the same id, so any other state — and the id itself — survives the auth boundary.
        Session?.Clear();
    }

    /// <inheritdoc />
    public UserSessionInfo? GetCurrentUser()
    {
        var json = Session?.GetString(UserSessionKey);
        if (string.IsNullOrEmpty(json)) return null;

        var info = JsonSerializer.Deserialize<UserSessionInfo>(json);
        if (info == null) return null;

        // The session must belong to the AUTHENTICATED principal, or it is not served at all.
        // The session cookie is a bearer of state, not of identity: without this check a request
        // presenting user B's auth cookie alongside user A's session cookie would run with A's
        // permission snapshot and pharmacy — session fixation/swap turned into privilege transfer.
        var httpUser = _httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated != true) return null;

        var idClaim = httpUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var principalId) || principalId != info.UserId) return null;

        return info;
    }

    /// <inheritdoc />
    public List<PagePermission> GetAccessiblePages()
    {
        // Same principal binding as GetCurrentUser: a session that does not belong to the
        // authenticated user grants nothing. HasPageAccess — the authorization decision — reads
        // through here, so this is where the binding must hold.
        if (GetCurrentUser() == null) return new List<PagePermission>();

        var json = Session?.GetString(PagePermissionsKey);
        if (string.IsNullOrEmpty(json)) return new List<PagePermission>();

        return JsonSerializer.Deserialize<List<PagePermission>>(json) ?? new List<PagePermission>();
    }

    /// <inheritdoc />
    public bool HasPageAccess(string controller, string action, string permissionType)
    {
        var pages = GetAccessiblePages();

        // Exact match on controller + action (checks both main Pages and flattened PageUrls)
        var page = pages.FirstOrDefault(p =>
            string.Equals(p.Controller, controller, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Action, action, StringComparison.OrdinalIgnoreCase));

        if (page == null) return false;

        return permissionType.ToLower() switch
        {
            "view" => page.CanView,
            "create" => page.CanCreate,
            "edit" => page.CanEdit,
            "delete" => page.CanDelete,
            _ => false
        };
    }

    /// <inheritdoc />
    public List<SidebarMenuItemDTO> GetSidebarMenu()
    {
        if (GetCurrentUser() == null) return new List<SidebarMenuItemDTO>();

        var json = Session?.GetString(SidebarMenuKey);
        if (string.IsNullOrEmpty(json)) return new List<SidebarMenuItemDTO>();

        return JsonSerializer.Deserialize<List<SidebarMenuItemDTO>>(json) ?? new List<SidebarMenuItemDTO>();
    }

    /// <summary>
    /// Build a hierarchical sidebar menu from flat page list.
    /// Only includes pages user has access to, plus parent categories.
    /// </summary>
    private List<SidebarMenuItemDTO> BuildSidebarMenu(
        List<Domain.Entities.Security.Page> allPages,
        HashSet<int> accessiblePageIds)
    {
        var result = new List<SidebarMenuItemDTO>();
        var pageDict = allPages.ToDictionary(p => p.PageID);

        // Get all accessible pages and their parent chains
        var includedPageIds = new HashSet<int>();
        foreach (var pageId in accessiblePageIds)
        {
            // Add this page and all its parents
            var currentId = (int?)pageId;
            while (currentId.HasValue && pageDict.TryGetValue(currentId.Value, out var page))
            {
                includedPageIds.Add(currentId.Value);
                currentId = page.Parent_ID;
            }
        }

        // Build menu items for included pages
        var menuItems = allPages
            .Where(p => includedPageIds.Contains(p.PageID))
            .Select(p => new SidebarMenuItemDTO
            {
                PageId = p.PageID,
                Title = p.Title,
                Icon = p.Icon,
                Controller = p.Controller,
                Action = p.Action,
                ParentId = p.Parent_ID,
                DisplayOrder = p.DisplayOrder,
                IsVisible = p.IsVisible
            })
            .ToList();

        // --- Sidebar Refinement ---
        // 1. Remove "Customer Ledger" and "Advance Receipts" (already covered by unified reports)
        menuItems.RemoveAll(m => m.Title == "Customer Ledger" || m.Title == "Advance Receipts");

        // 2. Move "Financial Periods" under "Configuration" header
        var configMenu = menuItems.FirstOrDefault(m => m.Title == "Configuration" && m.ParentId == null);
        var financialPeriodMenu = menuItems.FirstOrDefault(m => m.Title == "Financial Periods");
        if (configMenu != null && financialPeriodMenu != null)
        {
            financialPeriodMenu.ParentId = configMenu.PageId;
        }
        // --------------------------

        // Build hierarchy
        var menuDict = menuItems.ToDictionary(m => m.PageId);
        foreach (var item in menuItems)
        {
            if (item.ParentId.HasValue && menuDict.TryGetValue(item.ParentId.Value, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                result.Add(item);
            }
        }

        // Sort children by display order
        foreach (var item in menuItems)
        {
            item.Children = item.Children.OrderBy(c => c.DisplayOrder).ToList();
        }

        return result.OrderBy(r => r.DisplayOrder).ToList();
    }
}
