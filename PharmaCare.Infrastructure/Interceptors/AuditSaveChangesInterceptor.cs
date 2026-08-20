using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Logging;
using PharmaCare.Domain.Enums;
using PharmaCare.Infrastructure.Implementations.Tenancy;

namespace PharmaCare.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically captures entity changes and logs them
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditSaveChangesInterceptor> _logger;

    // Track audit entries during the save operation
    private List<AuditEntry> _pendingAuditEntries = new();

    // Audit rows built during an explicit business transaction, held back until it commits.
    // The activity log lives in a SEPARATE database, so it cannot join the business transaction;
    // writing on every SaveChanges meant a later rollback left log rows describing sales, GRNs and
    // vouchers that never existed. UnitOfWork drives the deferral (see BeginDeferral/Flush/Discard).
    private readonly List<ActivityLog> _deferredLogs = new();

    /// <summary>True while audit rows are being buffered for an open business transaction.</summary>
    public bool IsDeferring { get; private set; }
    private static readonly HashSet<string> ExcludedAuditProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "Password",
        "ConfirmPassword",
        "SecurityStamp",
        "ConcurrencyStamp",
        "AuthenticatorKey",
        "RecoveryCodes",
        "TwoFactorEnabled",
        "AccessFailedCount",
        "LockoutEnd",
        "LockoutEnabled",
        "PhoneNumberConfirmed"
    };

    private static readonly HashSet<string> MaskedAuditProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email",
        "NormalizedEmail",
        "PhoneNumber",
        "UserName",
        "NormalizedUserName",
        "AccountNumber",
        "IBAN",
        // Party's contact columns. The list above covered the names Identity uses, but a
        // CUSTOMER's phone, alternate contact and home address live in columns named Phone,
        // ContactNumber and Address — which went into the audit database in cleartext, another
        // copy on every edit, with no redaction path.
        "Phone",
        "ContactNumber",
        "Address"
    };

    public AuditSaveChangesInterceptor(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        ILogger<AuditSaveChangesInterceptor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        OnBeforeSaveChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        OnBeforeSaveChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        // Sync save path must stay fully synchronous — blocking on the async writer here
        // risks thread-pool starvation/deadlocks on every SaveChanges call.
        try
        {
            var logs = BuildActivityLogs();
            if (logs.Count > 0)
            {
                if (IsDeferring)
                {
                    // Inside a business transaction: hold until commit.
                    _deferredLogs.AddRange(logs);
                }
                else
                {
                    using var scope = _serviceProvider.CreateScope();
                    var logContext = scope.ServiceProvider.GetService<LogDbContext>();
                    if (logContext != null)
                    {
                        logContext.ActivityLogs.AddRange(logs);
                        logContext.SaveChanges();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record {Count} audit log entries; the business change itself succeeded.", _pendingAuditEntries.Count);
        }
        finally
        {
            _pendingAuditEntries.Clear();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = BuildActivityLogs();
            if (logs.Count > 0)
            {
                if (IsDeferring)
                {
                    // Inside a business transaction: hold until commit.
                    _deferredLogs.AddRange(logs);
                }
                else
                {
                    using var scope = _serviceProvider.CreateScope();
                    var logContext = scope.ServiceProvider.GetService<LogDbContext>();
                    if (logContext != null)
                    {
                        logContext.ActivityLogs.AddRange(logs);
                        await logContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record {Count} audit log entries; the business change itself succeeded.", _pendingAuditEntries.Count);
        }
        finally
        {
            _pendingAuditEntries.Clear();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Starts buffering audit rows instead of writing them. Called by UnitOfWork when an explicit
    /// business transaction opens.
    /// </summary>
    public void BeginDeferral()
    {
        IsDeferring = true;
        _deferredLogs.Clear();
    }

    /// <summary>
    /// Writes the buffered audit rows. Called by UnitOfWork AFTER the business transaction has
    /// committed, so the log only ever describes changes that actually landed. A failure here
    /// loses audit rows for an already-committed change, which is logged and not rethrown —
    /// failing the caller at this point would misreport a successful business transaction.
    /// </summary>
    public async Task FlushDeferredAsync(CancellationToken cancellationToken = default)
    {
        IsDeferring = false;
        if (_deferredLogs.Count == 0) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var logContext = scope.ServiceProvider.GetService<LogDbContext>();
            if (logContext != null)
            {
                logContext.ActivityLogs.AddRange(_deferredLogs);
                await logContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write {Count} buffered audit log entries after commit.", _deferredLogs.Count);
        }
        finally
        {
            _deferredLogs.Clear();
        }
    }

    /// <summary>
    /// Drops the buffered audit rows. Called by UnitOfWork on rollback — those changes never
    /// happened, so they must not appear in the activity log.
    /// </summary>
    public void DiscardDeferred()
    {
        IsDeferring = false;
        _deferredLogs.Clear();
    }

    private void OnBeforeSaveChanges(DbContext? context)
    {
        if (context == null) return;

        _pendingAuditEntries.Clear();

        var httpContext = _httpContextAccessor.HttpContext;

        // Platform super-admin actions (provisioning a pharmacy, suspending it, etc.) are system
        // setup, not a pharmacy's own operational activity — do not write them to the activity log.
        if (httpContext?.User?.HasClaim(TenantClaimsPrincipalFactory.PlatformAdminClaimType, "true") == true)
        {
            return;
        }

        var userId = GetCurrentUserId(httpContext);
        var userName = GetCurrentUserName(httpContext);
        var ipAddress = GetClientIpAddress(httpContext);
        var userAgent = GetUserAgent(httpContext);
        var fallbackPharmacyId = GetCurrentPharmacyId(httpContext);

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Skip if not a tracked entity type we want to audit
            if (entry.Entity is ActivityLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            // Prefer the row's own stamped tenant (set by DbContext.StampTenant before this runs);
            // fall back to the request's tenant claim for non-tenant entities (e.g. User).
            var pharmacyId = entry.Entity is ITenantEntity tenantEntity && tenantEntity.Pharmacy_ID > 0
                ? tenantEntity.Pharmacy_ID
                : fallbackPharmacyId;

            var auditEntry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Pharmacy_ID = pharmacyId,
                EntityName = entry.Entity.GetType().Name,
                Entry = entry
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry.ActivityType = ActivityType.Create;
                    auditEntry.NewValues = GetPropertyValues(entry, EntityState.Added);
                    break;

                case EntityState.Deleted:
                    auditEntry.ActivityType = ActivityType.Delete;
                    auditEntry.OldValues = GetPropertyValues(entry, EntityState.Deleted);
                    auditEntry.EntityId = GetPrimaryKeyValue(entry);
                    break;

                case EntityState.Modified:
                    auditEntry.ActivityType = ActivityType.Update;
                    auditEntry.OldValues = GetOriginalValues(entry);
                    auditEntry.NewValues = GetCurrentValues(entry);
                    auditEntry.EntityId = GetPrimaryKeyValue(entry);
                    break;
            }

            _pendingAuditEntries.Add(auditEntry);
        }
    }

    /// <summary>
    /// Materializes the pending audit entries as ActivityLog rows.
    /// <para>
    /// Built eagerly, right after the save, because the values are read from the tracked entities
    /// (and database-generated keys are only available now). The resulting rows are safe to hold
    /// in memory until the transaction commits.
    /// </para>
    /// </summary>
    private List<ActivityLog> BuildActivityLogs()
    {
        var logs = new List<ActivityLog>(_pendingAuditEntries.Count);
        if (_pendingAuditEntries.Count == 0) return logs;

        foreach (var auditEntry in _pendingAuditEntries)
        {
            // For newly created entities, we need to get the ID after save
            if (auditEntry.ActivityType == ActivityType.Create && auditEntry.Entry != null)
            {
                auditEntry.EntityId = GetPrimaryKeyValue(auditEntry.Entry);
                auditEntry.NewValues = GetPropertyValues(auditEntry.Entry, EntityState.Added);
            }

            var activityLog = new ActivityLog
            {
                UserId = auditEntry.UserId,
                UserName = auditEntry.UserName,
                ActivityType = auditEntry.ActivityType,
                EntityName = auditEntry.EntityName,
                EntityId = auditEntry.EntityId,
                OldValues = auditEntry.OldValues,
                NewValues = auditEntry.NewValues,
                IpAddress = auditEntry.IpAddress,
                UserAgent = auditEntry.UserAgent,
                Timestamp = AppTime.Now,
                Pharmacy_ID = auditEntry.Pharmacy_ID,
                Description = GenerateDescription(auditEntry)
            };

            logs.Add(activityLog);
        }

        return logs;
    }

    private int GetCurrentUserId(HttpContext? httpContext)
    {
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
            return 0;

        var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
    }

    private string GetCurrentUserName(HttpContext? httpContext)
    {
        return httpContext?.User?.Identity?.Name ?? "System";
    }

    private int? GetCurrentPharmacyId(HttpContext? httpContext)
    {
        var claim = httpContext?.User?.FindFirst(CurrentTenantService.PharmacyClaimType)?.Value;
        return int.TryParse(claim, out var id) && id > 0 ? id : null;
    }

    private string? GetClientIpAddress(HttpContext? httpContext)
    {
        return httpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent(HttpContext? httpContext)
    {
        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
        return userAgent?.Length > 500 ? userAgent[..500] : userAgent;
    }

    private string? GetPrimaryKeyValue(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties == null) return null;

        var keyValues = keyProperties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
            .Where(v => v != null);

        return string.Join(",", keyValues);
    }

    private string? GetPropertyValues(EntityEntry entry, EntityState state)
    {
        var properties = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var propertyName = property.Metadata.Name;
            if (property.Metadata.IsPrimaryKey() || ShouldExcludeProperty(propertyName)) continue;

            var value = state == EntityState.Deleted
                ? property.OriginalValue
                : property.CurrentValue;

            properties[propertyName] = MaskValue(propertyName, value);
        }

        return properties.Count > 0
            ? JsonSerializer.Serialize(properties, new JsonSerializerOptions { WriteIndented = false })
            : null;
    }

    private string? GetOriginalValues(EntityEntry entry)
    {
        var properties = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var propertyName = property.Metadata.Name;
            if (property.Metadata.IsPrimaryKey() || ShouldExcludeProperty(propertyName)) continue;
            if (!property.IsModified) continue;

            properties[propertyName] = MaskValue(propertyName, property.OriginalValue);
        }

        return properties.Count > 0
            ? JsonSerializer.Serialize(properties, new JsonSerializerOptions { WriteIndented = false })
            : null;
    }

    private string? GetCurrentValues(EntityEntry entry)
    {
        var properties = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var propertyName = property.Metadata.Name;
            if (property.Metadata.IsPrimaryKey() || ShouldExcludeProperty(propertyName)) continue;
            if (!property.IsModified) continue;

            properties[propertyName] = MaskValue(propertyName, property.CurrentValue);
        }

        return properties.Count > 0
            ? JsonSerializer.Serialize(properties, new JsonSerializerOptions { WriteIndented = false })
            : null;
    }

    private string GenerateDescription(AuditEntry auditEntry)
    {
        return auditEntry.ActivityType switch
        {
            ActivityType.Create => $"Created new {auditEntry.EntityName}",
            ActivityType.Update => $"Updated {auditEntry.EntityName} (ID: {auditEntry.EntityId})",
            ActivityType.Delete => $"Deleted {auditEntry.EntityName} (ID: {auditEntry.EntityId})",
            ActivityType.StatusChange => $"Changed status of {auditEntry.EntityName} (ID: {auditEntry.EntityId})",
            _ => $"{auditEntry.ActivityType} on {auditEntry.EntityName}"
        };
    }

    private static bool ShouldExcludeProperty(string propertyName)
    {
        return ExcludedAuditProperties.Contains(propertyName)
               || propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase);
    }

    private static object? MaskValue(string propertyName, object? value)
    {
        if (value == null || !ShouldMaskProperty(propertyName))
        {
            return value;
        }

        return value switch
        {
            string stringValue => MaskString(stringValue),
            _ => "***"
        };
    }

    private static bool ShouldMaskProperty(string propertyName)
    {
        return MaskedAuditProperties.Contains(propertyName)
               || propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Phone", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("UserName", StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Contains('@', StringComparison.Ordinal))
        {
            var atIndex = value.IndexOf('@');
            if (atIndex <= 1)
            {
                return $"***{value[atIndex..]}";
            }

            return $"{value[..1]}***{value[atIndex..]}";
        }

        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return $"{value[..2]}***{value[^2..]}";
    }

    /// <summary>
    /// Internal class to hold audit entry data during save operation
    /// </summary>
    private class AuditEntry
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public ActivityType ActivityType { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public int? Pharmacy_ID { get; set; }
        public EntityEntry? Entry { get; set; }
    }
}
