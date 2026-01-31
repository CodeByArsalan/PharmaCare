using PharmaCare.Application.DTOs.Logging;
using PharmaCare.Domain.Entities.Logging;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Interfaces.Logging;

public interface IActivityLogService
{
    /// <summary>
    /// Log an activity manually (for login/logout or custom events)
    /// </summary>
    Task LogActivityAsync(
        int userId,
        string userName,
        ActivityType activityType,
        string entityName,
        string? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        string? description = null);

    Task<ActivityLogPagedResult> GetLogsAsync(ActivityLogFilterDto filter);

    Task<IEnumerable<ActivityLogDto>> GetLogsByEntityAsync(string entityName, string entityId);

    Task<IEnumerable<ActivityLogDto>> GetLogsByUserAsync(int userId, DateTime? fromDate = null, DateTime? toDate = null);

    Task<ActivityLogDto?> GetByIdAsync(long id);

    /// <summary>
    /// Get summary statistics for the dashboard
    /// </summary>
    Task<ActivityLogSummary> GetSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null);
}

/// <summary>
/// Summary statistics for activity logs
/// </summary>
public class ActivityLogSummary
{
    public int TotalLogs { get; set; }
    public int TodayLogs { get; set; }
    public int CreateCount { get; set; }
    public int UpdateCount { get; set; }
    public int DeleteCount { get; set; }
    public int LoginCount { get; set; }
    public Dictionary<string, int> TopEntities { get; set; } = new();
    public Dictionary<string, int> TopUsers { get; set; } = new();
}
