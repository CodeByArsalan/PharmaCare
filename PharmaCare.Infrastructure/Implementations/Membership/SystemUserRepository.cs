using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.ViewModels;
using PharmaCare.Infrastructure.Interfaces.Membership;

namespace PharmaCare.Infrastructure.Implementations.Membership;

public class SystemUserRepository(DbContext _context) : ISystemUserRepository
{
    public List<MenuItemDto> GetUserPages(int userID)
    {
        // 1. Get all WebPage IDs assigned to the user
        var assignedPageIds = _context.Set<UserWebPages>()
            .Where(uwp => uwp.SystemUser_ID == userID)
            .Select(uwp => uwp.WebPage_ID)
            .ToList();

        if (!assignedPageIds.Any())
            return new List<MenuItemDto>();

        // 2. Get all assigned web pages with their URLs
        var assignedPages = _context.Set<WebPages>()
            .Include(wp => wp.WebPageUrls)
            .Where(wp => assignedPageIds.Contains(wp.WebPageID) && wp.IsVisible)
            .ToList();

        // 3. Get parent IDs of assigned pages (to include parent menus)
        var parentIds = assignedPages
            .Where(wp => wp.Parent_ID != 0)
            .Select(wp => wp.Parent_ID)
            .Distinct()
            .ToList();

        // 4. Get parent pages that aren't already in assigned pages
        var parentPages = _context.Set<WebPages>()
            .Where(wp => parentIds.Contains(wp.WebPageID) && wp.IsVisible && !assignedPageIds.Contains(wp.WebPageID))
            .ToList();

        // 5. Combine all pages
        var allPages = assignedPages.Concat(parentPages).ToList();

        // 6. Build hierarchical structure
        var result = new List<MenuItemDto>();

        // Get top-level parents (Parent_ID = 0)
        var topLevelPages = allPages
            .Where(wp => wp.Parent_ID == 0)
            .OrderBy(wp => wp.PageOrder)
            .ToList();

        foreach (var parent in topLevelPages)
        {
            var menuItem = MapToDto(parent);

            // Get children of this parent
            var children = allPages
                .Where(wp => wp.Parent_ID == parent.WebPageID)
                .OrderBy(wp => wp.PageOrder)
                .ToList();

            foreach (var child in children)
            {
                menuItem.Children.Add(MapToDto(child));
            }

            result.Add(menuItem);
        }

        // Also add any assigned pages that are standalone (Parent_ID = 0 and no children in result)
        return result;
    }
    private MenuItemDto MapToDto(WebPages webPage)
    {
        return new MenuItemDto
        {
            WebPageID = webPage.WebPageID,
            PageTitle = webPage.PageTitle,
            PageIcon = webPage.PageIcon,
            ControllerName = webPage.ControllerName,
            ViewName = webPage.ViewName,
            PageOrder = webPage.PageOrder,
            Urls = webPage.WebPageUrls?.Select(u => u.Url).ToList() ?? new List<string>()
        };
    }
    public UserWithPagesDto? GetUserWithPages(int userID)
    {
        // Get user details
        var user = _context.Set<SystemUser>()
            .Include(u => u.UserType)
            .FirstOrDefault(u => u.Id == userID);

        if (user == null)
            return null;

        // Get menu items using existing method
        var menuItems = GetUserPages(userID);

        return new UserWithPagesDto
        {
            UserID = user.Id,
            FullName = user.FullName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            UserTypeID = user.UserType_ID,
            UserType = user.UserType?.UserType ?? string.Empty,
            MenuItems = menuItems
        };
    }
    public async Task<List<WebPages>> GetAllWebPagesAsync()
    {
        return await _context.Set<WebPages>()
            .Include(wp => wp.WebPageUrls)
            .Where(wp => wp.IsVisible)
            .OrderBy(wp => wp.Parent_ID)
            .ThenBy(wp => wp.PageOrder)
            .ToListAsync();
    }
    public async Task SaveUserPagesAsync(int userId, List<int> pageIds)
    {
        // Remove existing page assignments for this user
        var existingAssignments = _context.Set<UserWebPages>()
            .Where(uwp => uwp.SystemUser_ID == userId);

        _context.Set<UserWebPages>().RemoveRange(existingAssignments);

        // Add new page assignments
        if (pageIds != null && pageIds.Any())
        {
            var newAssignments = pageIds.Select(pageId => new UserWebPages
            {
                SystemUser_ID = userId,
                WebPage_ID = pageId
            }).ToList();

            await _context.Set<UserWebPages>().AddRangeAsync(newAssignments);
        }

        await _context.SaveChangesAsync();
    }
}

