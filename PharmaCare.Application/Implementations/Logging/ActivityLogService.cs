using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Logging;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Domain.Entities.Logging;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Logging;

public class ActivityLogService : IActivityLogService
{
    private readonly IActivityLogRepository _logRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentTenant _currentTenant;

    public ActivityLogService(
        IActivityLogRepository logRepository,
        IHttpContextAccessor httpContextAccessor,
        ICurrentTenant currentTenant)
    {
        _logRepository = logRepository;
        _httpContextAccessor = httpContextAccessor;
        _currentTenant = currentTenant;
    }

    // The activity log lives in a separate database with no global query filter, so every READ
    // must be scoped to the current pharmacy explicitly. A pharmacy admin sees only their own
    // pharmacy's log entries; entries stamped with a different (or null) Pharmacy_ID stay hidden.
    private IQueryable<ActivityLog> TenantScoped()
    {
        var tenantId = _currentTenant.TenantId;
        return _logRepository.Query().Where(l => l.Pharmacy_ID == tenantId);
    }

    public async Task LogActivityAsync(
        int userId,
        string userName,
        ActivityType activityType,
        string entityName,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        string? description = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var log = new ActivityLog
        {
            UserId = userId,
            UserName = userName,
            ActivityType = activityType,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = GetUserAgent(httpContext),
            Timestamp = AppTime.Now,
            Description = description ?? GenerateDescription(activityType, entityName, entityId),
            Pharmacy_ID = _currentTenant.TenantId
        };

        await _logRepository.AddAsync(log);
        await _logRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Widest page the log will serve. The audit log is the largest table in the system and the
    /// only one holding a copy of every changed field, so an unclamped page size is both the
    /// cheapest way to exhaust the server's memory and the cheapest way to walk off with the
    /// entire record in one request.
    /// </summary>
    private const int MaxPageSize = 200;

    public async Task<ActivityLogPagedResult> GetLogsAsync(ActivityLogFilterDto filter)
    {
        // Both paging values are model-bound straight from the query string. Unclamped, a
        // hand-edited ?PageNumber=0 reached SQL as a negative OFFSET and 500'd.
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);

        var query = TenantScoped();

        // Apply filters
        if (filter.UserId.HasValue)
            query = query.Where(l => l.UserId == filter.UserId.Value);

        if (!string.IsNullOrEmpty(filter.UserName))
            query = query.Where(l => l.UserName.Contains(filter.UserName));

        if (filter.ActivityType.HasValue)
            query = query.Where(l => l.ActivityType == filter.ActivityType.Value);

        if (!string.IsNullOrEmpty(filter.EntityName))
            query = query.Where(l => l.EntityName == filter.EntityName);

        if (!string.IsNullOrEmpty(filter.EntityId))
            query = query.Where(l => l.EntityId == filter.EntityId);

        if (filter.FromDate.HasValue)
            query = query.Where(l => l.Timestamp >= filter.FromDate.Value);

        // ToDate is INCLUSIVE of its whole calendar day. Timestamp carries a time of day, so the
        // old `<= ToDate` cut the day off at midnight — and today-to-today, the very filter this
        // screen defaults to, returned nothing. Same `< day+1` idiom as every report service.
        if (filter.ToDate.HasValue)
            query = query.Where(l => l.Timestamp < filter.ToDate.Value.Date.AddDays(1));

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply ordering and pagination
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => MapToDto(l))
            .ToListAsync();

        return new ActivityLogPagedResult
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<ActivityLogDto>> GetLogsByEntityAsync(string entityName, string entityId)
    {
        return await TenantScoped()
            .Where(l => l.EntityName == entityName && l.EntityId == entityId)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => MapToDto(l))
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivityLogDto>> GetLogsByUserAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = TenantScoped().Where(l => l.UserId == userId);

        if (fromDate.HasValue)
            query = query.Where(l => l.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.Timestamp <= toDate.Value);

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(100) // Limit to last 100 entries
            .Select(l => MapToDto(l))
            .ToListAsync();
    }

    public async Task<ActivityLogDto?> GetByIdAsync(long id)
    {
        var log = await _logRepository.GetByIdAsync(id);
        // Never expose a log entry belonging to another pharmacy.
        if (log == null || log.Pharmacy_ID != _currentTenant.TenantId)
        {
            return null;
        }
        return MapToDto(log);
    }

    public async Task<ActivityLogSummary> GetSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = TenantScoped();

        if (fromDate.HasValue)
            query = query.Where(l => l.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.Timestamp <= toDate.Value);

        var today = AppTime.Today;

        var summary = new ActivityLogSummary
        {
            TotalLogs = await query.CountAsync(),
            TodayLogs = await query.CountAsync(l => l.Timestamp >= today),
            CreateCount = await query.CountAsync(l => l.ActivityType == ActivityType.Create),
            UpdateCount = await query.CountAsync(l => l.ActivityType == ActivityType.Update),
            DeleteCount = await query.CountAsync(l => l.ActivityType == ActivityType.Delete),
            LoginCount = await query.CountAsync(l => l.ActivityType == ActivityType.Login)
        };

        // Top 5 entities
        summary.TopEntities = await query
            .GroupBy(l => l.EntityName)
            .Select(g => new { EntityName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToDictionaryAsync(x => x.EntityName, x => x.Count);

        // Top 5 users
        summary.TopUsers = await query
            .GroupBy(l => l.UserName)
            .Select(g => new { UserName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToDictionaryAsync(x => x.UserName, x => x.Count);

        return summary;
    }

    private static ActivityLogDto MapToDto(ActivityLog log)
    {
        return new ActivityLogDto
        {
            ActivityLogID = log.ActivityLogID,
            UserId = log.UserId,
            UserName = log.UserName,
            ActivityType = log.ActivityType,
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            OldValues = log.OldValues,
            NewValues = log.NewValues,
            IpAddress = log.IpAddress,
            Timestamp = log.Timestamp,
            Description = log.Description
        };
    }

    private static string GenerateDescription(ActivityType activityType, string entityName, string? entityId)
    {
        return activityType switch
        {
            ActivityType.Create => $"Created new {entityName}",
            ActivityType.Update => $"Updated {entityName}" + (entityId != null ? $" (ID: {entityId})" : ""),
            ActivityType.Delete => $"Deleted {entityName}" + (entityId != null ? $" (ID: {entityId})" : ""),
            ActivityType.Login => "User logged in",
            ActivityType.Logout => "User logged out",
            _ => $"{activityType} on {entityName}"
        };
    }

    private static string? GetUserAgent(HttpContext? httpContext)
    {
        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
        return userAgent?.Length > 500 ? userAgent[..500] : userAgent;
    }
}
