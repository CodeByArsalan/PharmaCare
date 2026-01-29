using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations.Security;

/// <summary>
/// Repository implementation for Role entity.
/// </summary>
public class RoleRepository : Repository<Role>, IRoleRepository
{
    public RoleRepository(PharmaCareDBContext context) : base(context)
    {
    }

    public async Task<List<Role>> GetAllOrderedAsync()
    {
        return await _dbSet
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<List<Role>> GetActiveRolesAsync()
    {
        return await _dbSet
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }
}
