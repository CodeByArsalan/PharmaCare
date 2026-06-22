namespace PharmaCare.Infrastructure.Implementations.Logging;

/// <summary>
/// Configuration for the activity-log retention job. Bound from the "LogRetention"
/// section of configuration so the windows can be tuned without code changes.
/// </summary>
public class LogRetentionOptions
{
    public const string SectionName = "LogRetention";

    /// <summary>Master switch — set false to disable the retention job entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How long rows stay in the live ActivityLogs table before being archived. Default 30 days.</summary>
    public int HotRetentionDays { get; set; } = 30;

    /// <summary>How long rows are kept in the archive before being purged. Default 730 days (~2 years).</summary>
    public int ArchiveRetentionDays { get; set; } = 730;

    /// <summary>Rows processed per SQL batch, to keep locks/transactions short.</summary>
    public int BatchSize { get; set; } = 2000;

    /// <summary>How often the job runs. Default once per day.</summary>
    public int RunIntervalHours { get; set; } = 24;

    /// <summary>Delay before the first run after startup, so it doesn't compete with boot-up work.</summary>
    public int InitialDelayMinutes { get; set; } = 5;
}
