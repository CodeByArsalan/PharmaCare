using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Implementations.Security;

/// <summary>
/// Implementation of user management service.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserManager _userManager;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRepository<Store> _storeRepository;
    private readonly IRepository<User> _userRepository;

    public UserService(
        IUserManager userManager,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRepository<Store> storeRepository,
        IRepository<User> userRepository)
    {
        _userManager = userManager;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _storeRepository = storeRepository;
        _userRepository = userRepository;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var users = await _userRepository.Query()
            .ToListAsync();
        return users.OrderBy(u => u.FullName).ToList();
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<List<int>> GetUserRoleIdsAsync(int userId)
    {
        return await _userRoleRepository.GetRoleIdsByUserIdAsync(userId);
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(User user, string password, List<int> roleIds, int createdBy)
    {
        user.UserName = user.Email;
        user.CreatedAt = DateTime.Now;
        user.CreatedBy = createdBy;
        user.IsActive = true;

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return (false, string.Join(", ", result.Errors));
        }

        // Assign roles
        foreach (var roleId in roleIds)
        {
            await _userRoleRepository.AddAsync(new UserRole
            {
                User_ID = user.Id,
                Role_ID = roleId
            });
        }

        await _userRoleRepository.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateUserAsync(User user, string? newPassword, List<int> roleIds, int updatedBy)
    {
        var existingUser = await _userManager.FindByIdAsync(user.Id);
        if (existingUser == null)
            return (false, "User not found");

        // Update user properties
        existingUser.FullName = user.FullName;
        existingUser.Email = user.Email;
        existingUser.UserName = user.Email;
        existingUser.PhoneNumber = user.PhoneNumber;
        existingUser.Store_ID = user.Store_ID;
        existingUser.UpdatedAt = DateTime.Now;
        existingUser.UpdatedBy = updatedBy;

        // Update password if provided
        if (!string.IsNullOrEmpty(newPassword))
        {
            var passwordResult = await _userManager.ResetPasswordAsync(existingUser, newPassword);
            if (!passwordResult.Succeeded)
            {
                return (false, string.Join(", ", passwordResult.Errors));
            }
        }

        // Update roles - remove existing and add new
        await _userRoleRepository.RemoveByUserIdAsync(user.Id);

        foreach (var roleId in roleIds)
        {
            await _userRoleRepository.AddAsync(new UserRole
            {
                User_ID = user.Id,
                Role_ID = roleId
            });
        }

        await _userRoleRepository.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> ToggleUserStatusAsync(int id, int updatedBy)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return false;

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.Now;
        user.UpdatedBy = updatedBy;

        await _userRepository.SaveChangesAsync();
        return true;
    }

    public async Task<List<Role>> GetRolesForDropdownAsync()
    {
        return await _roleRepository.GetActiveRolesAsync();
    }

    public async Task<List<Store>> GetStoresForDropdownAsync()
    {
        var stores = await _storeRepository.FindAsync(s => s.IsActive);
        return stores.OrderBy(s => s.Name).ToList();
    }
}

// Extension to add ToListAsync for IQueryable without EF Core dependency in Application layer
internal static class QueryableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this System.Linq.IQueryable<T> query)
    {
        return await Task.Run(() => query.ToList());
    }
}
