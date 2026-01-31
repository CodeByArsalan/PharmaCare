using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.DTOs.Logging;

/// <summary>
/// DTO for displaying activity log information
/// </summary>
public class ActivityLogDto
{
    public long ActivityLogID { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public ActivityType ActivityType { get; set; }
    public string ActivityTypeName => ActivityType.ToString();
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Description { get; set; }
    public int? StoreId { get; set; }
}

/// <summary>
/// DTO for filtering activity logs
/// </summary>
public class ActivityLogFilterDto
{
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public ActivityType? ActivityType { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? StoreId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Paginated result for activity logs
/// </summary>
public class ActivityLogPagedResult
{
    public List<ActivityLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
