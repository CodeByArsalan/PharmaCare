using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Stands in for the real SessionService, which stores its state in an HTTP session that does not
/// exist outside a request. Services under test only use it to stamp a user name on activity-log
/// entries, so a fixed identity is enough.
/// <para>
/// Page permissions are NOT modelled here: authorization is enforced by PageAuthorizationFilter in
/// the web layer, which these service-level tests do not exercise.
/// </para>
/// </summary>
public sealed class TestSessionService : ISessionService
{
    public const int TestUserId = 1;

    public Task InitializeSessionAsync(int userId) => Task.CompletedTask;

    public void ClearSession() { }

    public UserSessionInfo? GetCurrentUser() => new()
    {
        UserId = TestUserId,
        FullName = "Integration Test",
        Email = "tests@test.local"
    };

    public List<PagePermission> GetAccessiblePages() => new();

    public bool HasPageAccess(string controller, string action, string permissionType) => true;

    public List<SidebarMenuItemDTO> GetSidebarMenu() => new();
}
