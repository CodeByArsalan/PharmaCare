using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace PharmaCare.Application.Interfaces.Membership;

public interface ISystemUserService
{
    Task<List<SystemUser>> GetUsers();
    string GetUserPagesJson(int userID);
    UserWithPagesDto? GetUserWithPages(int userID);
    Task<List<WebPages>> GetAllWebPagesAsync();
    Task<List<int>> GetUserAssignedPageIdsAsync(int userId);
    Task<IdentityResult> CreateUser(SystemUser user, string password, List<int> pageIds);
    Task<IdentityResult> UpdateUser(SystemUser user, List<int> pageIds);
    Task DeleteUser(int userId);
    Task<SystemUser> GetUserById(int userId);
    Task<List<UserTypes>> GetUserTypes();
}

