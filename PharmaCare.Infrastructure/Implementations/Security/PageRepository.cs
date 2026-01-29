using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations.Security;

/// <summary>
/// Repository implementation for Page entity.
/// </summary>
public class PageRepository : Repository<Page>, IPageRepository
{
    public PageRepository(PharmaCareDBContext context) : base(context)
    {
    }

    public async Task<List<Page>> GetActiveOrderedAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive)
            .OrderBy(p => p.Parent_ID)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();
    }
}
