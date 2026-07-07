using PharmaCare.Domain.Enums;

namespace PharmaCare.Domain.Entities.Logging;

/// <summary>
/// Archive copy of an <see cref="ActivityLog"/> row. Rows are moved here from the live
/// ActivityLogs table once they exceed the hot-retention window, and kept for the longer
/// archive-retention window before being purged. The original <see cref="ActivityLogID"/>
/// is preserved (the PK is NOT database-generated here).
/// </summary>
public class ActivityLogArchive
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
    // Tenant (pharmacy) this log entry belongs to. Preserved from the live ActivityLog row.
    public int? Pharmacy_ID { get; set; }
}
