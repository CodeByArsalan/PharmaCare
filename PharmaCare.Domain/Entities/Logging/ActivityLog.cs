using PharmaCare.Domain.Enums;

namespace PharmaCare.Domain.Entities.Logging;

public class ActivityLog
{
    public long ActivityLogID { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public ActivityType ActivityType { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Description { get; set; }
    public int? StoreId { get; set; }
}
