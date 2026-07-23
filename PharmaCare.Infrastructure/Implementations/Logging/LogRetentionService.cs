using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PharmaCare.Infrastructure.Implementations.Logging;

/// <summary>
/// Background job that enforces the activity-log retention policy:
///  - moves ActivityLogs rows older than <see cref="LogRetentionOptions.HotRetentionDays"/>
///    into ActivityLogsArchive, then
///  - purges ActivityLogsArchive rows older than <see cref="LogRetentionOptions.ArchiveRetentionDays"/>.
/// Work is done in bounded batches using set-based SQL so it never holds a long lock or
/// loads rows into memory. Runs on a fixed interval; failures are logged and retried next tick.
/// </summary>
public class LogRetentionService : BackgroundService
{
    // Atomic move: deletes a batch from the hot table and writes the deleted rows straight
    // into the archive in a single statement. Original ActivityLogID is preserved.
    private const string MoveSql = @"
DELETE TOP (@batch) FROM ActivityLogs
OUTPUT DELETED.ActivityLogID, DELETED.UserId, DELETED.UserName, DELETED.ActivityType,
       DELETED.EntityName, DELETED.EntityId, DELETED.OldValues, DELETED.NewValues,
       DELETED.IpAddress, DELETED.UserAgent, DELETED.Timestamp, DELETED.Description, DELETED.StoreId,
       DELETED.Pharmacy_ID
  INTO ActivityLogsArchive (ActivityLogID, UserId, UserName, ActivityType,
       EntityName, EntityId, OldValues, NewValues,
       IpAddress, UserAgent, Timestamp, Description, StoreId,
       Pharmacy_ID)
WHERE Timestamp < @cutoff;";

    private const string PurgeSql =
        "DELETE TOP (@batch) FROM ActivityLogsArchive WHERE Timestamp < @cutoff;";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogRetentionService> _logger;
    private readonly LogRetentionOptions _options;

    public LogRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<LogRetentionService> logger,
        IOptions<LogRetentionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Log retention job is disabled via configuration.");
            return;
        }

        try
        {
            // Stagger the first run so it doesn't compete with application startup.
            await Task.Delay(TimeSpan.FromMinutes(Math.Max(0, _options.InitialDelayMinutes)), stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, _options.RunIntervalHours)));
            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Never let a transient failure kill the loop — try again next tick.
                    _logger.LogError(ex, "Activity-log retention run failed; will retry on next interval.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogDbContext>();

        var hotCutoff = DateTime.Now.AddDays(-Math.Max(0, _options.HotRetentionDays));
        var archiveCutoff = DateTime.Now.AddDays(-Math.Max(0, _options.ArchiveRetentionDays));
        int batch = Math.Max(1, _options.BatchSize);

        long movedTotal = await RunBatchedAsync(db, MoveSql, batch, hotCutoff, ct);
        long purgedTotal = await RunBatchedAsync(db, PurgeSql, batch, archiveCutoff, ct);

        if (movedTotal > 0 || purgedTotal > 0)
        {
            _logger.LogInformation(
                "Activity-log retention: archived {Moved} rows older than {HotDays}d; purged {Purged} archived rows older than {ArchiveDays}d.",
                movedTotal, _options.HotRetentionDays, purgedTotal, _options.ArchiveRetentionDays);
        }
    }

    /// <summary>
    /// Executes <paramref name="sql"/> repeatedly (fresh parameters each time) until a batch
    /// affects zero rows, returning the total number of rows affected.
    /// </summary>
    private static async Task<long> RunBatchedAsync(LogDbContext db, string sql, int batch, DateTime cutoff, CancellationToken ct)
    {
        long total = 0;
        int affected;
        do
        {
            // New SqlParameter instances per call — they cannot be shared across commands.
            var parameters = new object[]
            {
                new SqlParameter("@batch", batch),
                new SqlParameter("@cutoff", cutoff)
            };

            affected = await db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
            total += affected;
        }
        while (affected > 0 && !ct.IsCancellationRequested);

        return total;
    }
}
