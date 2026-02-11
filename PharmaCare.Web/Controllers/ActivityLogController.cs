using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.DTOs.Logging;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Web.Controllers;

[Authorize]
public class ActivityLogController : BaseController
{
    private readonly IActivityLogService _activityLogService;

    public ActivityLogController(IActivityLogService activityLogService)
    {
        _activityLogService = activityLogService;
    }

    public async Task<IActionResult> Index(ActivityLogFilterDto? filter = null)
    {
        filter ??= new ActivityLogFilterDto();

        // Check if any filter parameters are present in the request query
        bool isSearch = Request.Query.ContainsKey(nameof(ActivityLogFilterDto.FromDate)) ||
                        Request.Query.ContainsKey(nameof(ActivityLogFilterDto.ToDate)) ||
                        Request.Query.ContainsKey(nameof(ActivityLogFilterDto.UserId)) ||
                        Request.Query.ContainsKey(nameof(ActivityLogFilterDto.UserName)) ||
                        Request.Query.ContainsKey(nameof(ActivityLogFilterDto.ActivityType)) ||
                        Request.Query.ContainsKey(nameof(ActivityLogFilterDto.EntityName)) ||
                        Request.Query.ContainsKey(nameof(ActivityLogFilterDto.EntityId)) ||
                        Request.Query.ContainsKey(nameof(ActivityLogFilterDto.StoreId));

        ActivityLogPagedResult result;
        if (isSearch)
        {
            result = await _activityLogService.GetLogsAsync(filter);
        }
        else
        {
            // Initial load: Set default dates to Today for UI, but return empty results
            filter.FromDate = DateTime.Today;
            filter.ToDate = DateTime.Today;

            result = new ActivityLogPagedResult
            {
                Items = new List<ActivityLogDto>(),
                TotalCount = 0,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }
        
        // ViewBag.Filter = filter; // Pass filter via model or separate mechanism if needed? 
        // Actually, the View uses Model.Items. The Filter is passed back to view usually via Model/ViewBag.
        // Let's keep ViewBag.Filter as it's not a dropdown, but remove dropdowns.
        ViewBag.Filter = filter;
        // ViewBag.ActivityTypes = GetActivityTypeOptions(); // REMOVED
        // ViewBag.EntityNames = await GetEntityNamesAsync(); // REMOVED
        
        return View(result);
    }

    public async Task<IActionResult> Details(long id)
    {
        var log = await _activityLogService.GetByIdAsync(id);
        if (log == null)
            return NotFound();
        
        return View(log);
    }

    public async Task<IActionResult> EntityHistory(string entityName, string entityId)
    {
        var logs = await _activityLogService.GetLogsByEntityAsync(entityName, entityId);
        
        ViewBag.EntityName = entityName;
        ViewBag.EntityId = entityId;
        
        return View(logs);
    }

    public async Task<IActionResult> UserHistory(int userId)
    {
        var logs = await _activityLogService.GetLogsByUserAsync(userId, DateTime.Today.AddDays(-30));
        
        ViewBag.UserId = userId;
        
        return View(logs);
    }

    public async Task<IActionResult> Dashboard()
    {
        var summary = await _activityLogService.GetSummaryAsync(DateTime.Today.AddDays(-30));
        return View(summary);
    }

    // Removed private helper methods for dropdowns
}
