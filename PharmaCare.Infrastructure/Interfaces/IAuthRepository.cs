using PharmaCare.Domain.Models.Membership;

namespace PharmaCare.Infrastructure.Interfaces;

public interface IAuthRepository
{
    Task<bool> AnyUsersExistAsync();
    Task<AuthenticationResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
}
