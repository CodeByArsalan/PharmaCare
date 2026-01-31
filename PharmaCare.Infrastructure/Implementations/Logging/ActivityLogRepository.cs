using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Domain.Entities.Logging;

namespace PharmaCare.Infrastructure.Implementations.Logging;

/// <summary>
/// Repository implementation for activity logs using LogDbContext
/// </summary>
public class ActivityLogRepository : IActivityLogRepository
{
    private readonly LogDbContext _context;

    public ActivityLogRepository(LogDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ActivityLog log)
    {
        await _context.ActivityLogs.AddAsync(log);
    }

    public async Task<ActivityLog?> GetByIdAsync(long id)
    {
        return await _context.ActivityLogs.FindAsync(id);
    }

    public IQueryable<ActivityLog> Query()
    {
        return _context.ActivityLogs.AsQueryable();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
