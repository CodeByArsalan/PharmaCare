using Microsoft.AspNetCore.Identity;
using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.ViewModels;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Membership;
using System.Linq.Expressions;
using System.Text.Json;

namespace PharmaCare.Application.Implementations.Membership;

public class SystemUserService(ISystemUserRepository _systemUserRepository, IRepository<SystemUser> _repository,
        IRepository<UserTypes> _usertypeRepository, UserManager<SystemUser> _userManager) : ISystemUserService
{
    public async Task<List<SystemUser>> GetUsers()
    {
        var allSystemUsers = _repository.GetAllWithInclude(d => d.UserType, d => d.Store).ToList();
        return allSystemUsers;
    }

    public async Task<List<UserTypes>> GetUserTypes()
    {
        return (await _usertypeRepository.GetAll()).ToList();
    }

    public string GetUserPagesJson(int userID)
    {
        var menuItems = _systemUserRepository.GetUserPages(userID);
        return JsonSerializer.Serialize(menuItems);
    }

    public UserWithPagesDto? GetUserWithPages(int userID)
    {
        return _systemUserRepository.GetUserWithPages(userID);
    }

    public async Task<List<WebPages>> GetAllWebPagesAsync()
    {
        return await _systemUserRepository.GetAllWebPagesAsync();
    }

    public async Task<List<int>> GetUserAssignedPageIdsAsync(int userId)
    {
        var userPages = _systemUserRepository.GetUserPages(userId);
        var pageIds = new List<int>();

        foreach (var parent in userPages)
        {
            pageIds.Add(parent.WebPageID);
            if (parent.Children != null)
            {
                pageIds.AddRange(parent.Children.Select(c => c.WebPageID));
            }
        }

        return pageIds;
    }

    public async Task<IdentityResult> CreateUser(SystemUser user, string password, List<int> pageIds)
    {
        user.IsActive = true;
        user.EmailConfirmed = true;
        user.CreatedDateTime = DateTime.Now;
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded && pageIds != null && pageIds.Any())
        {
            await _systemUserRepository.SaveUserPagesAsync(user.Id, pageIds);
        }

        return result;
    }

    public async Task<IdentityResult> UpdateUser(SystemUser user, List<int> pageIds)
    {
        var existingUser = await _userManager.FindByIdAsync(user.Id.ToString());
        if (existingUser == null) return IdentityResult.Failed(new IdentityError { Description = "User not found" });

        existingUser.FullName = user.FullName;
        existingUser.UserType_ID = user.UserType_ID;
        existingUser.Store_ID = user.Store_ID;
        existingUser.UpdatedDateTime = DateTime.Now;
        // Don't update password here, usually handled separately or check if password field is filled

        var result = await _userManager.UpdateAsync(existingUser);

        if (result.Succeeded && pageIds != null)
        {
            await _systemUserRepository.SaveUserPagesAsync(user.Id, pageIds);
        }

        return result;
    }

    public async Task DeleteUser(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            user.IsActive = !user.IsActive; // Toggle status
            await _userManager.UpdateAsync(user);
        }
    }

    public async Task<SystemUser> GetUserById(int userId)
    {
        Expression<Func<SystemUser, object>>[] includeExpressions = { entity => entity.UserType, };
        var res = _repository.FindByConditionWithInclude(d => d.Id == userId, includeExpressions);
        return res.FirstOrDefault();
        // return await _userManager.FindByIdAsync(userId.ToString());
    }
}

