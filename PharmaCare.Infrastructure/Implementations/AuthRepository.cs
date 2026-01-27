using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Infrastructure.Implementations;

public class AuthRepository(SignInManager<SystemUser> _signInManager, UserManager<SystemUser> _userManager, PharmaCareDBContext _dbContext) : IAuthRepository
{
    public async Task<bool> AnyUsersExistAsync()
    {
        return await _userManager.Users.AnyAsync();
    }
    public async Task<AuthenticationResult> LoginAsync(string email, string password, bool rememberMe)
    {
        // Find the user first
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return AuthenticationResult.Failure(new[] { "Invalid email or password." });
        }

        // Check password
        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Load user type
            var userWithType = await _dbContext.Set<SystemUser>()
                .Include(u => u.UserType)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (userWithType?.UserType != null)
            {
                user = userWithType;
            }

            // Create custom claims
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("LoginUserDetail", System.Text.Json.JsonSerializer.Serialize(new
                {
                    UserID = user.Id,
                    UserName = user.UserName ?? "",
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    LoginUserTypeID = user.UserType_ID,
                    LoginUserType = user.UserType?.UserType ?? "Unknown",
                    Store_ID = user.Store_ID
                }))
            };

            // Sign in with custom claims
            await _signInManager.SignInWithClaimsAsync(user, rememberMe, claims);

            return AuthenticationResult.Success();
        }

        if (result.RequiresTwoFactor)
        {
            return AuthenticationResult.TwoFactorRequired();
        }

        if (result.IsLockedOut)
        {
            return AuthenticationResult.LockedOut();
        }

        return AuthenticationResult.Failure(new[] { "Invalid email or password." });
    }
    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }
}
