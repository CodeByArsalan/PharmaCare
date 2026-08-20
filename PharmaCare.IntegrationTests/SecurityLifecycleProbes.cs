using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// User and role administration probes.
///
/// <para>
/// Every earlier sweep attacked the money: transactions first, then the master data those
/// transactions post against. Nothing has yet attacked the layer that decides WHO is allowed to
/// touch either. <c>UserService</c> and <c>RoleService</c> between them own account creation, role
/// assignment, activation and the permission matrix, and until now they had two tests each — both
/// about cross-tenant leakage, neither about the lifecycle.
/// </para>
///
/// <para>
/// The recurring shape here is an administrative gesture that is harmless in isolation and
/// irreversible in aggregate: deactivating "a" user when it happens to be the last one who can
/// administer the tenant, clearing "a" permission when the role holding it is the only route back,
/// handing a user a role that no longer works. None of these fail loudly — they succeed and leave
/// the pharmacy worse off.
/// </para>
///
/// <para>Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.</para>
/// </summary>
[Collection(Collections.Database)]
public class SecurityLifecycleProbes
{
    private readonly DatabaseFixture _fixture;

    public SecurityLifecycleProbes(DatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------------------------------
    // Lock-out: the tenant administers itself, so nothing outside it can undo these.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_last_administrator_of_a_pharmacy_cannot_be_deactivated()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var users = tenant.Get<IUserService>();

        // Provisioning leaves exactly one user: the pharmacy's administrator.
        var admin = await SoleUserAsync(tenant);

        // Refusing with an explanatory exception is the correct outcome; what must never happen
        // is the deactivation going through.
        await Record.ExceptionAsync(() => users.ToggleUserStatusAsync(admin.Id, TenantData.TestUserId));

        var stillActive = await tenant.Db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Pharmacy_ID == tenant.PharmacyId && u.IsActive);

        Assert.True(stillActive,
            "The pharmacy's only administrator was deactivated. Login refuses inactive users, and " +
            "users are administered from inside the tenant, so nobody can undo this: the pharmacy " +
            "is permanently locked out of its own books.");
    }

    [Fact]
    public async Task A_pharmacys_only_administrator_role_cannot_be_stripped_of_every_permission()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();

        var adminRole = await tenant.Db.Roles_Custom.AsNoTracking()
            .FirstAsync(r => r.IsSystemRole);

        // The permissions form posts back every page with its checkboxes. Clearing them all is a
        // single click away, and RoleService deletes the RolePage row for any page left unticked.
        var cleared = (await roles.GetPermissionsForRoleAsync(adminRole.RoleID))
            .Select(p => new RolePagePermissionDTO
            {
                PageId = p.PageId,
                CanView = false,
                CanCreate = false,
                CanEdit = false,
                CanDelete = false
            })
            .ToList();

        // Refusal-by-exception is fine; the rows surviving is what matters.
        await Record.ExceptionAsync(() => roles.UpdatePermissionsAsync(adminRole.RoleID, cleared));

        var remaining = await tenant.Db.RolePages.AsNoTracking()
            .CountAsync(rp => rp.Role_ID == adminRole.RoleID);

        Assert.True(remaining > 0,
            "Every permission was removed from the tenant's system Administrator role. Reaching the " +
            "role screen to put them back requires the very permission that was just deleted, so the " +
            "pharmacy can no longer administer itself.");
    }

    [Fact]
    public async Task A_system_role_cannot_be_deactivated_or_renamed_out_of_existence()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();

        var adminRole = await tenant.Db.Roles_Custom.AsNoTracking().FirstAsync(r => r.IsSystemRole);

        // ToggleRoleStatusAsync explicitly refuses system roles. UpdateRoleAsync — which writes the
        // same row — carries no such guard, and the role NAME is what the dashboard keys its
        // "is this an administrator" decision on.
        await roles.UpdateRoleAsync(new Role
        {
            RoleID = adminRole.RoleID,
            Name = "Front Desk",
            Description = adminRole.Description
        }, TenantData.TestUserId);

        var storedName = await tenant.Db.Roles_Custom.AsNoTracking()
            .Where(r => r.RoleID == adminRole.RoleID)
            .Select(r => r.Name)
            .FirstAsync();

        Assert.Equal("Administrator", storedName);
    }

    // ------------------------------------------------------------------------------------------
    // Role assignment: a role that cannot grant anything must not be assignable as though it can.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_user_cannot_be_created_holding_a_deactivated_role()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();
        var users = tenant.Get<IUserService>();

        var cashier = new Role { Name = "Cashier", Description = "Till operator." };
        await roles.CreateRoleAsync(cashier, TenantData.TestUserId);
        await roles.ToggleRoleStatusAsync(cashier.RoleID, TenantData.TestUserId);

        Assert.False(await tenant.Db.Roles_Custom.AsNoTracking()
            .Where(r => r.RoleID == cashier.RoleID).Select(r => r.IsActive).FirstAsync());

        var (success, _) = await users.CreateUserAsync(
            NewUser(tenant.PharmacyId, "retired-role"),
            "ProbePass123",
            new List<int> { cashier.RoleID },
            TenantData.TestUserId);

        Assert.False(success,
            "A user was created holding a DEACTIVATED role. Deactivating a role is the only way to " +
            "retire a job function, and the role dropdown itself excludes inactive roles — but the " +
            "id is accepted anyway, so the retired role stays in circulation.");
    }

    [Fact]
    public async Task Deactivating_a_role_actually_withdraws_the_permissions_it_grants()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();
        var users = tenant.Get<IUserService>();

        var cashier = new Role { Name = "Till", Description = "Till operator." };
        await roles.CreateRoleAsync(cashier, TenantData.TestUserId);

        var firstPage = await tenant.Db.Pages.AsNoTracking().OrderBy(p => p.PageID).FirstAsync();
        await roles.UpdatePermissionsAsync(cashier.RoleID, new List<RolePagePermissionDTO>
        {
            new() { PageId = firstPage.PageID, CanView = true, CanCreate = true, CanEdit = true, CanDelete = true }
        });

        // A user must hold the role WHILE it is active, then keep holding it after deactivation.
        var holder = NewUser(tenant.PharmacyId, "till-holder");
        var (created, error) = await users.CreateUserAsync(
            holder, "ProbePass123", new List<int> { cashier.RoleID }, TenantData.TestUserId);
        Assert.True(created, error);

        await roles.ToggleRoleStatusAsync(cashier.RoleID, TenantData.TestUserId);

        // PermissionResolution is THE production implementation of "what may this user do?" —
        // SessionService snapshots its answer and AuthService serves it ad hoc. A deactivated
        // role's grants must not survive into it.
        var effective = await PharmaCare.Infrastructure.Implementations.Security.PermissionResolution
            .EffectivePermissionsAsync(tenant.Db, holder.Id);

        Assert.DoesNotContain(effective, p => p.PageId == firstPage.PageID
            && (p.CanView || p.CanCreate || p.CanEdit || p.CanDelete));
    }

    [Fact]
    public async Task Creating_a_user_is_all_or_nothing()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();
        var users = tenant.Get<IUserService>();

        var role = new Role { Name = "Stock Keeper" };
        await roles.CreateRoleAsync(role, TenantData.TestUserId);

        // A multi-select that posts the same role twice — the form offers no protection against it,
        // and RolesBelongToCurrentPharmacyAsync distinct-counts for validation but the insert loop
        // does not. UserService.CreateUserAsync commits the Identity user FIRST and the role links
        // in a separate save afterwards, so a failure in the second half cannot undo the first.
        var user = NewUser(tenant.PharmacyId, "double-role");
        var error = await Record.ExceptionAsync(() => users.CreateUserAsync(
            user, "ProbePass123", new List<int> { role.RoleID, role.RoleID }, TenantData.TestUserId));

        var userExists = await tenant.Db.Users.AsNoTracking().AnyAsync(u => u.Email == user.Email);
        var links = await tenant.Db.UserRoles_Custom.AsNoTracking()
            .CountAsync(ur => ur.User_ID == user.Id);

        Assert.True(error is null && links == 1,
            $"Creating a user with the same role selected twice threw {error?.GetType().Name}. " +
            $"The user row survives (exists: {userExists}) with {links} role links, so the account " +
            "is left in the database with no permissions and no way for the caller to tell — and the " +
            "email address is now taken, so the same user cannot simply be created again.");
    }

    [Fact]
    public async Task Two_roles_in_one_pharmacy_cannot_share_a_name()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();

        await roles.CreateRoleAsync(new Role { Name = "Pharmacist" }, TenantData.TestUserId);

        try
        {
            await roles.CreateRoleAsync(new Role { Name = "Pharmacist" }, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return; // Refusing the duplicate is the correct outcome.
        }

        var count = await tenant.Db.Roles_Custom.AsNoTracking().CountAsync(r => r.Name == "Pharmacist");

        Assert.Equal(1, count);
    }

    // ------------------------------------------------------------------------------------------
    // The permission matrix is written straight from a posted form.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_permission_row_cannot_be_written_against_a_page_that_does_not_exist()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();

        var role = new Role { Name = "Auditor" };
        await roles.CreateRoleAsync(role, TenantData.TestUserId);

        // Page ids arrive from the permissions form as plain integers with no validation.
        var bogus = new List<RolePagePermissionDTO>
        {
            new() { PageId = 987654321, CanView = true }
        };

        var error = await Record.ExceptionAsync(() => roles.UpdatePermissionsAsync(role.RoleID, bogus));

        Assert.True(error is null or InvalidOperationException or ArgumentException,
            "A permission row naming a non-existent page reached the database and surfaced as " +
            $"{error?.GetType().Name}. Only the application's own validation exceptions are shown to " +
            "the user; anything else becomes a generic 500 with no indication of what was wrong.");
    }

    [Fact]
    public async Task Permissions_written_for_a_role_stay_inside_the_owning_pharmacy()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        var role = new Role { Name = "Locum" };
        await first.Get<IRoleService>().CreateRoleAsync(role, TenantData.TestUserId);

        var page = await first.Db.Pages.AsNoTracking().OrderBy(p => p.PageID).FirstAsync();
        await first.Get<IRoleService>().UpdatePermissionsAsync(role.RoleID, new List<RolePagePermissionDTO>
        {
            new() { PageId = page.PageID, CanView = true }
        });

        // The second pharmacy must not be able to read, and therefore must not be able to reason
        // about, another pharmacy's permission grants.
        var leaked = await second.Get<IRoleService>().GetPermissionsForRoleAsync(role.RoleID);

        Assert.DoesNotContain(leaked, p => p.PageId == page.PageID && p.CanView);
    }

    // ------------------------------------------------------------------------------------------
    // Identity edges.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_email_that_differs_only_by_case_is_refused()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var users = tenant.Get<IUserService>();

        var first = NewUser(tenant.PharmacyId, "casing");
        var (created, error) = await users.CreateUserAsync(
            first, "ProbePass123", new List<int>(), TenantData.TestUserId);
        Assert.True(created, error);

        var second = NewUser(tenant.PharmacyId, "casing");
        second.Email = first.Email!.ToUpperInvariant();

        var (duplicate, _) = await users.CreateUserAsync(
            second, "ProbePass123", new List<int>(), TenantData.TestUserId);

        Assert.False(duplicate,
            "Two accounts now differ only by the capitalisation of their email address. Login " +
            "resolves against the normalized column, so one of them can never sign in.");
    }

    [Fact]
    public async Task A_users_password_cannot_be_reset_below_the_configured_policy()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var users = tenant.Get<IUserService>();

        var user = NewUser(tenant.PharmacyId, "weak-reset");
        var (created, error) = await users.CreateUserAsync(
            user, "ProbePass123", new List<int>(), TenantData.TestUserId);
        Assert.True(created, error);

        // The edit form's optional "new password" box goes straight to ResetPasswordAsync.
        var (updated, _) = await users.UpdateUserAsync(
            new User { Id = user.Id, Email = user.Email, FullName = user.FullName },
            newPassword: "abc",
            roleIds: new List<int>(),
            updatedBy: TenantData.TestUserId);

        Assert.False(updated,
            "An administrator reset a colleague's password to 'abc'. The policy configured in " +
            "Program.cs (8 chars, upper, lower, digit) is enforced at creation but must hold on " +
            "every later reset too.");
    }

    [Fact]
    public async Task A_failed_role_update_does_not_leave_the_user_with_no_roles_at_all()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var roles = tenant.Get<IRoleService>();
        var users = tenant.Get<IUserService>();

        var keeper = new Role { Name = "Dispenser" };
        await roles.CreateRoleAsync(keeper, TenantData.TestUserId);

        var user = NewUser(tenant.PharmacyId, "role-wipe");
        var (created, error) = await users.CreateUserAsync(
            user, "ProbePass123", new List<int> { keeper.RoleID }, TenantData.TestUserId);
        Assert.True(created, error);

        // An edit that must be rejected: the password fails the policy. UpdateUserAsync removes the
        // user's existing role links BEFORE it reaches that failure.
        await users.UpdateUserAsync(
            new User { Id = user.Id, Email = user.Email, FullName = user.FullName },
            newPassword: "abc",
            roleIds: new List<int> { keeper.RoleID },
            updatedBy: TenantData.TestUserId);

        var roleCount = await tenant.Db.UserRoles_Custom.AsNoTracking()
            .CountAsync(ur => ur.User_ID == user.Id);

        Assert.Equal(1, roleCount);
    }

    // ------------------------------------------------------------------------------------------

    private static async Task<User> SoleUserAsync(TenantScope tenant) =>
        await tenant.Db.Users.AsNoTracking()
            .Where(u => u.Pharmacy_ID == tenant.PharmacyId)
            .OrderBy(u => u.Id)
            .FirstAsync();

    private static User NewUser(int pharmacyId, string label)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new User
        {
            Email = $"{label}-{suffix}@probe.local",
            FullName = $"Probe {label}",
            Pharmacy_ID = pharmacyId
        };
    }
}
