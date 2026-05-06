using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces;

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

        // Fetch roles directly
        var userRoles = await _context.UserRoles_Custom
            .AsNoTracking()
            .Where(ur => ur.User_ID == userId)
            .Join(_context.Roles_Custom,
                ur => ur.Role_ID,
                r => r.RoleID,
                (ur, r) => new { r.RoleID, r.Name })
            .ToListAsync();

        var roleIds = userRoles.Select(r => r.RoleID).ToList();

        // Fetch accessible pages for user's roles - Optimized with Grouping and Projections
        var pagePermissions = await _context.RolePages
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.Role_ID))
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

        var aggregatedPermissions = pagePermissions
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

        // Create user session info
        var userSession = new UserSessionInfo
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            StoreId = null,
            StoreName = null,
            RoleIds = roleIds,
            RoleNames = userRoles.Select(r => r.Name).ToList()
        };

        // Fetch PageUrls for accessible pages and add them to permissions
        var accessiblePageIds = aggregatedPermissions.Select(p => p.PageId).ToList();
        var pageUrls = await _context.PageUrls
            .AsNoTracking()
            .Where(pu => accessiblePageIds.Contains(pu.Page_ID))
            .ToListAsync();

        // Add PageUrl entries
        foreach (var pageUrl in pageUrls)
        {
            var parentPermission = aggregatedPermissions.FirstOrDefault(p => p.PageId == pageUrl.Page_ID);
            if (parentPermission != null)
            {
                var exists = aggregatedPermissions.Any(p =>
                    string.Equals(p.Controller, pageUrl.Controller, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Action, pageUrl.Action, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    aggregatedPermissions.Add(new PagePermission
                    {
                        PageId = pageUrl.Page_ID,
                        Controller = pageUrl.Controller,
                        Action = pageUrl.Action,
                        CanView = parentPermission.CanView,
                        CanCreate = parentPermission.CanCreate,
                        CanEdit = parentPermission.CanEdit,
                        CanDelete = parentPermission.CanDelete
                    });
                }
            }
        }

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
        Session?.Remove(UserSessionKey);
        Session?.Remove(PagePermissionsKey);
        Session?.Remove(SidebarMenuKey);
    }

    /// <inheritdoc />
    public UserSessionInfo? GetCurrentUser()
    {
        var json = Session?.GetString(UserSessionKey);
        if (string.IsNullOrEmpty(json)) return null;

        return JsonSerializer.Deserialize<UserSessionInfo>(json);
    }

    /// <inheritdoc />
    public List<PagePermission> GetAccessiblePages()
    {
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
