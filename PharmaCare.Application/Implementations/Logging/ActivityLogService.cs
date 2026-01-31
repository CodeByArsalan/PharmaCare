using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Logging;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Domain.Entities.Logging;
using PharmaCare.Domain.Enums;
using PharmaCare.Domain.Interfaces;

namespace PharmaCare.Application.Implementations.Logging;

public class ActivityLogService : IActivityLogService
{
    private readonly IActivityLogRepository _logRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStoreContext _storeContext;

    public ActivityLogService(
        IActivityLogRepository logRepository,
        IHttpContextAccessor httpContextAccessor,
        IStoreContext storeContext)
    {
        _logRepository = logRepository;
        _httpContextAccessor = httpContextAccessor;
        _storeContext = storeContext;
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
            Timestamp = DateTime.Now,
            Description = description ?? GenerateDescription(activityType, entityName, entityId),
            StoreId = _storeContext.CurrentStoreId
        };

        await _logRepository.AddAsync(log);
        await _logRepository.SaveChangesAsync();
    }

    public async Task<ActivityLogPagedResult> GetLogsAsync(ActivityLogFilterDto filter)
    {
        var query = _logRepository.Query();

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

        if (filter.ToDate.HasValue)
            query = query.Where(l => l.Timestamp <= filter.ToDate.Value);

        if (filter.StoreId.HasValue)
            query = query.Where(l => l.StoreId == filter.StoreId.Value);

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply ordering and pagination
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(l => MapToDto(l))
            .ToListAsync();

        return new ActivityLogPagedResult
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
    }

    public async Task<IEnumerable<ActivityLogDto>> GetLogsByEntityAsync(string entityName, string entityId)
    {
        return await _logRepository.Query()
            .Where(l => l.EntityName == entityName && l.EntityId == entityId)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => MapToDto(l))
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivityLogDto>> GetLogsByUserAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _logRepository.Query().Where(l => l.UserId == userId);

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
        return log != null ? MapToDto(log) : null;
    }

    public async Task<ActivityLogSummary> GetSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _logRepository.Query();

        if (fromDate.HasValue)
            query = query.Where(l => l.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.Timestamp <= toDate.Value);

        var today = DateTime.Today;

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
            Description = log.Description,
            StoreId = log.StoreId
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
