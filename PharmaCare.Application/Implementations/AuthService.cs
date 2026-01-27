using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations;

public class AuthService(IAuthRepository _authRepository) : IAuthService
{
    public async Task<bool> AnyUsersExistAsync()
    {
        return await _authRepository.AnyUsersExistAsync();
    }
    public async Task<AuthenticationResult> LoginAsync(string email, string password, bool rememberMe)
    {
        return await _authRepository.LoginAsync(email, password, rememberMe);
    }
    public async Task LogoutAsync()
    {
        await _authRepository.LogoutAsync();
    }
}
