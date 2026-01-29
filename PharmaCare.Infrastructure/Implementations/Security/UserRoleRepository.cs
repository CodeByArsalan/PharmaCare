using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations.Security;

/// <summary>
/// Repository implementation for UserRole entity.
/// </summary>
public class UserRoleRepository : Repository<UserRole>, IUserRoleRepository
{
    public UserRoleRepository(PharmaCareDBContext context) : base(context)
    {
    }

    public async Task<List<int>> GetRoleIdsByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(ur => ur.User_ID == userId)
            .Select(ur => ur.Role_ID)
            .ToListAsync();
    }

    public async Task RemoveByUserIdAsync(int userId)
    {
        var userRoles = await _dbSet
            .Where(ur => ur.User_ID == userId)
            .ToListAsync();
        _dbSet.RemoveRange(userRoles);
    }
}
