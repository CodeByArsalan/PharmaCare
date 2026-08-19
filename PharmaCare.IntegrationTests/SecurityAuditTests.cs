using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Probes from the 2026-08-19 security-service audit. Every test asserts behavior the system is
/// SUPPOSED to have; a failing test is a confirmed defect, not a broken test.
/// </summary>
[Collection(Collections.Database)]
public class SecurityAuditTests
{
    private readonly DatabaseFixture _fixture;

    public SecurityAuditTests(DatabaseFixture fixture) => _fixture = fixture;

    private static User NewUser(string email) => new()
    {
        FullName = "Probe User",
        Email = email,
        PhoneNumber = "0300-0000000"
    };

    private static string UniqueEmail() => $"probe-{Guid.NewGuid():N}@test.local";

    /// <summary>
    /// SEC-1: AuthService.LoginAsync resolves users with FindByEmailAsync, which matches on
    /// NormalizedEmail. Changing an email through UserService.UpdateUserAsync must therefore
    /// keep the normalized columns in step, or the new email can never log in.
    /// </summary>
    [Fact]
    public async Task A_changed_email_is_resolvable_for_login()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var users = tenant.Get<IUserService>();

        var user = NewUser(UniqueEmail());
        var created = await users.CreateUserAsync(user, "TestPass123", new List<int>(), TestSessionService.TestUserId);
        Assert.True(created.Success, created.Error);

        var newEmail = UniqueEmail();
        user.Email = newEmail;
        var updated = await users.UpdateUserAsync(user, null, new List<int>(), TestSessionService.TestUserId);
        Assert.True(updated.Success, updated.Error);

        var resolved = await tenant.Get<UserManager<User>>().FindByEmailAsync(newEmail);
        Assert.True(resolved is not null && resolved.Id == user.Id,
            "the updated email cannot be resolved by FindByEmailAsync — login with the new email is impossible");
    }

    /// <summary>
    /// SEC-2: after an email change the OLD address must stop resolving, otherwise the address the
    /// admin thinks was retired still logs in indefinitely.
    /// </summary>
    [Fact]
    public async Task A_changed_email_retires_the_old_address()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var users = tenant.Get<IUserService>();

        var oldEmail = UniqueEmail();
        var user = NewUser(oldEmail);
        var created = await users.CreateUserAsync(user, "TestPass123", new List<int>(), TestSessionService.TestUserId);
        Assert.True(created.Success, created.Error);

        user.Email = UniqueEmail();
        var updated = await users.UpdateUserAsync(user, null, new List<int>(), TestSessionService.TestUserId);
        Assert.True(updated.Success, updated.Error);

        var resolvedOld = await tenant.Get<UserManager<User>>().FindByEmailAsync(oldEmail);
        Assert.True(resolvedOld is null, "the retired email still resolves for login after the change");
    }

    /// <summary>
    /// SEC-3: the create path enforces unique emails (Identity RequireUniqueEmail); the update path
    /// must enforce the same rule, or two users can share an address.
    /// </summary>
    [Fact]
    public async Task An_email_cannot_be_changed_to_one_already_in_use()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var users = tenant.Get<IUserService>();

        var takenEmail = UniqueEmail();
        var first = NewUser(takenEmail);
        Assert.True((await users.CreateUserAsync(first, "TestPass123", new List<int>(), TestSessionService.TestUserId)).Success);

        var second = NewUser(UniqueEmail());
        Assert.True((await users.CreateUserAsync(second, "TestPass123", new List<int>(), TestSessionService.TestUserId)).Success);

        second.Email = takenEmail;
        var updated = await users.UpdateUserAsync(second, null, new List<int>(), TestSessionService.TestUserId);

        Assert.False(updated.Success, "a user's email was changed to an address another user already holds");
    }

    /// <summary>
    /// SEC-4: roles are tenant-owned. Assigning another pharmacy's role id to a user must be
    /// refused — the role id is client-controlled on the user form.
    /// </summary>
    [Fact]
    public async Task A_role_from_another_pharmacy_cannot_be_assigned_to_a_user()
    {
        using var tenantA = await _fixture.NewTenantAsync();
        using var tenantB = await _fixture.NewTenantAsync();

        var foreignRole = new Role { Name = "Foreign Role" };
        Assert.True(await tenantB.Get<IRoleService>().CreateRoleAsync(foreignRole, TestSessionService.TestUserId));

        var user = NewUser(UniqueEmail());
        await tenantA.Get<IUserService>().CreateUserAsync(
            user, "TestPass123", new List<int> { foreignRole.RoleID }, TestSessionService.TestUserId);

        var smuggled = await tenantA.Db.Set<UserRole>().IgnoreQueryFilters()
            .AnyAsync(ur => ur.User_ID == user.Id && ur.Role_ID == foreignRole.RoleID);
        Assert.False(smuggled, "a user was linked to a role belonging to another pharmacy");
    }

    /// <summary>
    /// SEC-5: permission grants are keyed by raw role id. Writing permissions against another
    /// pharmacy's role must be refused, not silently create RolePage rows for the foreign role.
    /// </summary>
    [Fact]
    public async Task Permissions_cannot_be_written_against_another_pharmacys_role()
    {
        using var tenantA = await _fixture.NewTenantAsync();
        using var tenantB = await _fixture.NewTenantAsync();

        var foreignRole = new Role { Name = "Victim Role" };
        Assert.True(await tenantB.Get<IRoleService>().CreateRoleAsync(foreignRole, TestSessionService.TestUserId));

        var page = await tenantA.Db.Set<Page>().AsNoTracking().FirstAsync();
        await tenantA.Get<IRoleService>().UpdatePermissionsAsync(foreignRole.RoleID, new List<RolePagePermissionDTO>
        {
            new() { PageId = page.PageID, CanView = true, CanCreate = true, CanEdit = true, CanDelete = true }
        });

        var written = await tenantA.Db.Set<RolePage>().IgnoreQueryFilters()
            .AnyAsync(rp => rp.Role_ID == foreignRole.RoleID);
        Assert.False(written, "permissions were granted against a role belonging to another pharmacy");
    }

    /// <summary>
    /// SEC-6: role assignments of another pharmacy's user must not be readable across the
    /// tenant boundary.
    /// </summary>
    [Fact]
    public async Task Role_ids_of_another_pharmacys_user_are_not_readable()
    {
        using var tenantA = await _fixture.NewTenantAsync();
        using var tenantB = await _fixture.NewTenantAsync();

        var roleB = new Role { Name = "B Role" };
        Assert.True(await tenantB.Get<IRoleService>().CreateRoleAsync(roleB, TestSessionService.TestUserId));

        var userB = NewUser(UniqueEmail());
        var created = await tenantB.Get<IUserService>().CreateUserAsync(
            userB, "TestPass123", new List<int> { roleB.RoleID }, TestSessionService.TestUserId);
        Assert.True(created.Success, created.Error);

        var leaked = await tenantA.Get<IUserService>().GetUserRoleIdsAsync(userB.Id);
        Assert.Empty(leaked);
    }
}
