using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PharmaCare.Domain.Entities.Logging;
using PharmaCare.Domain.Enums;
using PharmaCare.Domain.Interfaces;

namespace PharmaCare.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically captures entity changes and logs them
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStoreContext _storeContext;

    // Track audit entries during the save operation
    private List<AuditEntry> _pendingAuditEntries = new();

    public AuditSaveChangesInterceptor(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IStoreContext storeContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _storeContext = storeContext;
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
        OnAfterSaveChanges().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await OnAfterSaveChanges();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void OnBeforeSaveChanges(DbContext? context)
    {
        if (context == null) return;

        _pendingAuditEntries.Clear();

        var httpContext = _httpContextAccessor.HttpContext;
        var userId = GetCurrentUserId(httpContext);
        var userName = GetCurrentUserName(httpContext);
        var ipAddress = GetClientIpAddress(httpContext);
        var userAgent = GetUserAgent(httpContext);
        var storeId = _storeContext.CurrentStoreId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Skip if not a tracked entity type we want to audit
            if (entry.Entity is ActivityLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry
            {
                UserId = userId,
                UserName = userName,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                StoreId = storeId,
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

    private async Task OnAfterSaveChanges()
    {
        if (_pendingAuditEntries.Count == 0) return;

        try
        {
            // Create a new scope to get LogDbContext
            using var scope = _serviceProvider.CreateScope();
            var logContext = scope.ServiceProvider.GetService<LogDbContext>();

            if (logContext == null) return;

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
                    Timestamp = DateTime.Now,
                    StoreId = auditEntry.StoreId,
                    Description = GenerateDescription(auditEntry)
                };

                logContext.ActivityLogs.Add(activityLog);
            }

            await logContext.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Log failures should not affect main application
            // Consider adding a fallback logging mechanism here
        }
        finally
        {
            _pendingAuditEntries.Clear();
        }
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
            if (property.Metadata.IsPrimaryKey()) continue;

            var value = state == EntityState.Deleted
                ? property.OriginalValue
                : property.CurrentValue;

            properties[property.Metadata.Name] = value;
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
            if (property.Metadata.IsPrimaryKey()) continue;
            if (!property.IsModified) continue;

            properties[property.Metadata.Name] = property.OriginalValue;
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
            if (property.Metadata.IsPrimaryKey()) continue;
            if (!property.IsModified) continue;

            properties[property.Metadata.Name] = property.CurrentValue;
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
        public int? StoreId { get; set; }
        public EntityEntry? Entry { get; set; }
    }
}
