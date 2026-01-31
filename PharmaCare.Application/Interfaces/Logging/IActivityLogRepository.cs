using PharmaCare.Domain.Entities.Logging;

namespace PharmaCare.Application.Interfaces.Logging;

/// <summary>
/// Repository interface for activity log persistence
/// Allows Application layer to work with logs without depending on Infrastructure
/// </summary>
public interface IActivityLogRepository
{
    Task AddAsync(ActivityLog log);
    Task<ActivityLog?> GetByIdAsync(long id);
    IQueryable<ActivityLog> Query();
    Task<int> SaveChangesAsync();
}
