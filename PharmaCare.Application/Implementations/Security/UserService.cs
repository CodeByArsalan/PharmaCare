using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Implementations.Security;

/// <summary>
/// Implementation of user management service.
/// Users are NOT globally query-filtered (login resolves users cross-tenant by email), so this
/// service scopes user listing/lookup to the current pharmacy explicitly.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserManager _userManager;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;

    public UserService(
        IUserManager userManager,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant)
    {
        _userManager = userManager;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (tenantId is null) return new List<User>();

        var users = await _userRepository.Query()
            .Where(u => u.Pharmacy_ID == tenantId)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();
        return users.ToList();
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id);
        // Never expose a user belonging to another pharmacy.
        if (user == null || user.Pharmacy_ID != _currentTenant.TenantId)
        {
            return null;
        }
        return user;
    }

    public async Task<List<int>> GetUserRoleIdsAsync(int userId)
    {
        return await _userRoleRepository.GetRoleIdsByUserIdAsync(userId);
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(User user, string password, List<int> roleIds, int createdBy)
    {
        if (_currentTenant.TenantId is null)
        {
            return (false, "No pharmacy in context.");
        }

        user.UserName = user.Email;
        user.CreatedAt = DateTime.Now;
        user.CreatedBy = createdBy;
        user.IsActive = true;
        // New users always belong to the current pharmacy (never platform admins here).
        user.Pharmacy_ID = _currentTenant.TenantId;
        user.IsPlatformAdmin = false;

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

        await _unitOfWork.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateUserAsync(User user, string? newPassword, List<int> roleIds, int updatedBy)
    {
        var existingUser = await _userManager.FindByIdAsync(user.Id);
        if (existingUser == null || existingUser.Pharmacy_ID != _currentTenant.TenantId)
            return (false, "User not found");

        // Update user properties
        existingUser.FullName = user.FullName;
        existingUser.Email = user.Email;
        existingUser.UserName = user.Email;
        existingUser.PhoneNumber = user.PhoneNumber;
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

        await _unitOfWork.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> ToggleUserStatusAsync(int id, int updatedBy)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.Pharmacy_ID != _currentTenant.TenantId) return false;

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.Now;
        user.UpdatedBy = updatedBy;

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<List<Role>> GetRolesForDropdownAsync()
    {
        return await _roleRepository.GetActiveRolesAsync();
    }
}

