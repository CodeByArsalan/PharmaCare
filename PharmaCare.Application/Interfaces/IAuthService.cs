using PharmaCare.Domain.Models.Membership;

namespace PharmaCare.Application.Interfaces;

public interface IAuthService
{
    Task<bool> AnyUsersExistAsync();
    Task<AuthenticationResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
}
