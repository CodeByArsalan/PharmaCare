using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations.Security;

/// <summary>
/// Repository implementation for RolePage entity.
/// </summary>
public class RolePageRepository : Repository<RolePage>, IRolePageRepository
{
    public RolePageRepository(PharmaCareDBContext context) : base(context)
    {
    }

    public async Task<Dictionary<int, RolePage>> GetPermissionsByRoleIdAsync(int roleId)
    {
        return await _dbSet
            .Where(rp => rp.Role_ID == roleId)
            .ToDictionaryAsync(rp => rp.Page_ID);
    }

    public async Task RemoveByRoleIdAsync(int roleId)
    {
        var rolePages = await _dbSet
            .Where(rp => rp.Role_ID == roleId)
            .ToListAsync();
        _dbSet.RemoveRange(rolePages);
    }
}
