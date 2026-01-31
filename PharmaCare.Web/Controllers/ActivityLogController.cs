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
        
        // Default to last 7 days if no date filter specified
        if (!filter.FromDate.HasValue && !filter.ToDate.HasValue)
        {
            filter.FromDate = DateTime.Today.AddDays(-7);
            filter.ToDate = DateTime.Today.AddDays(1);
        }

        var result = await _activityLogService.GetLogsAsync(filter);
        
        ViewBag.Filter = filter;
        ViewBag.ActivityTypes = GetActivityTypeOptions();
        ViewBag.EntityNames = await GetEntityNamesAsync();
        
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

    private List<SelectListItem> GetActivityTypeOptions()
    {
        return Enum.GetValues<ActivityType>()
            .Select(t => new SelectListItem
            {
                Value = ((int)t).ToString(),
                Text = t.ToString()
            })
            .ToList();
    }

    private async Task<List<string>> GetEntityNamesAsync()
    {
        var filter = new ActivityLogFilterDto { PageSize = 1000 };
        var logs = await _activityLogService.GetLogsAsync(filter);
        return logs.Items
            .Select(l => l.EntityName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }
}
