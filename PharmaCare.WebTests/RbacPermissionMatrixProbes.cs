using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Enums;
using PharmaCare.Infrastructure;
using PharmaCare.Web.Utilities;

namespace PharmaCare.WebTests;

/// <summary>
/// The page-permission filter does not read a declared permission for an endpoint. Unless an
/// action carries an explicit <c>[LinkedToPage(PermissionType = ...)]</c>,
/// <c>PageAuthorizationFilter.DeterminePermissionType</c> INFERS one from the action's NAME:
/// it looks for "add"/"create", then "edit"/"update", then "delete"/"toggle", and otherwise falls
/// back to the HTTP verb — where POST means "create".
///
/// <para>
/// "Void", "Approve", "Reverse", "Close", "Open" and "SavePermissions" contain none of those
/// substrings. Every one of them is therefore gated on CREATE. An earlier sweep fixed three
/// endpoints individually by bolting on an explicit PermissionType; these probes ask whether the
/// same hole is still open everywhere else, using a user who holds view+create and nothing more.
/// </para>
///
/// <para>Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.</para>
/// </summary>
[Collection(WebCollection.Name)]
public class RbacPermissionMatrixProbes
{
    private const string TokenPage = "/Home/Index";
    private readonly WebTestFixture _fx;

    public RbacPermissionMatrixProbes(WebTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Create_only_user_cannot_void_an_approved_expense()
    {
        var expense = await SeedApprovedExpenseAsync();
        var client = await _fx.CreateOnlyClientAsync();

        await HttpTestHelpers.PostFormAsync(client, "/Expense/Void",
            new Dictionary<string, string>
            {
                ["id"] = Utility.EncryptId(expense),
                ["voidReason"] = "rbac probe"
            },
            tokenPath: TokenPage);

        var status = await ExpenseStatusAsync(expense);

        Assert.NotEqual(TransactionStatus.Void, status);
    }

    [Fact]
    public async Task Create_only_user_cannot_approve_an_expense_into_the_ledger()
    {
        var expense = await SeedDraftExpenseAsync();
        var client = await _fx.CreateOnlyClientAsync();

        await HttpTestHelpers.PostFormAsync(client, "/Expense/Approve",
            new Dictionary<string, string> { ["id"] = Utility.EncryptId(expense) },
            tokenPath: TokenPage);

        var status = await ExpenseStatusAsync(expense);

        // Approval is the step that posts the expense voucher to the general ledger. Raising a
        // draft and approving your own draft should not be the same permission.
        Assert.NotEqual(TransactionStatus.Approved, status);
    }

    [Fact]
    public async Task Create_only_user_cannot_void_a_supplier_credit_note()
    {
        var noteId = await SeedSupplierCreditNoteAsync();
        var client = await _fx.CreateOnlyClientAsync();

        await HttpTestHelpers.PostFormAsync(client, "/SupplierCreditNote/Void",
            new Dictionary<string, string>
            {
                ["id"] = Utility.EncryptId(noteId),
                ["voidReason"] = "rbac probe"
            },
            tokenPath: TokenPage);

        var status = await _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var note = await db.SupplierCreditNotes.AsNoTracking()
                .FirstAsync(n => n.SupplierCreditNoteID == noteId);
            return note.Status;
        });

        Assert.NotEqual("Void", status);
    }

    [Fact]
    public async Task Create_only_user_cannot_reopen_a_closed_financial_period()
    {
        var periodId = await CurrentPeriodIdAsync();

        // Close it as the administrator first.
        var admin = await _fx.AdminClientAsync();
        await HttpTestHelpers.PostFormAsync(admin, "/FinancialPeriod/Close",
            new Dictionary<string, string> { ["id"] = periodId.ToString(), ["remarks"] = "closed by admin" },
            tokenPath: TokenPage);

        Assert.True(await IsPeriodClosedAsync(periodId), "Setup: the period should be closed.");

        // The period lock is the single control that stops anyone back-dating into settled books.
        var client = await _fx.CreateOnlyClientAsync();
        await HttpTestHelpers.PostFormAsync(client, "/FinancialPeriod/Open",
            new Dictionary<string, string> { ["id"] = periodId.ToString() },
            tokenPath: TokenPage);

        var stillClosed = await IsPeriodClosedAsync(periodId);

        // Leave the tenant as we found it so later probes are unaffected.
        if (!stillClosed)
        {
            await HttpTestHelpers.PostFormAsync(admin, "/FinancialPeriod/Close",
                new Dictionary<string, string> { ["id"] = periodId.ToString(), ["remarks"] = "re-closed by probe" },
                tokenPath: TokenPage);
        }
        await ReopenAsAdminAsync(admin, periodId);

        Assert.True(stillClosed,
            "A view+create user re-opened a CLOSED accounting period. That single POST unlocks " +
            "back-dated posting into books that were already signed off.");
    }

    [Fact]
    public async Task Create_only_user_cannot_rewrite_a_roles_permissions()
    {
        var client = await _fx.CreateOnlyClientAsync();

        var (roleId, pageId) = await _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var role = await db.Roles_Custom.AsNoTracking().FirstAsync(r => r.Name == "Web Test Data Entry");
            var page = await db.Pages.AsNoTracking().FirstAsync(p => p.Controller == "Sale");
            return (role.RoleID, page.PageID);
        });

        // Grant this user's own role full rights on a page it holds no permission for at all.
        await HttpTestHelpers.PostFormAsync(client, "/Role/SavePermissions",
            new Dictionary<string, string>
            {
                ["roleId"] = roleId.ToString(),
                ["permissions[0].PageId"] = pageId.ToString(),
                ["permissions[0].CanView"] = "true",
                ["permissions[0].CanCreate"] = "true",
                ["permissions[0].CanEdit"] = "true",
                ["permissions[0].CanDelete"] = "true"
            },
            tokenPath: TokenPage);

        var escalated = await _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            return await db.RolePages.AsNoTracking()
                .AnyAsync(rp => rp.Role_ID == roleId && rp.Page_ID == pageId && rp.CanDelete);
        });

        Assert.False(escalated,
            "A view+create user rewrote its OWN role's permission matrix, granting itself delete " +
            "rights on a page it could not previously see. This is self-service privilege escalation.");
    }

    // ------------------------------------------------------------------------------------------
    // Seeding / inspection helpers.
    // ------------------------------------------------------------------------------------------

    private Task<int> SeedDraftExpenseAsync()
        => _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var expenseService = sp.GetRequiredService<IExpenseService>();

            var expenseAccount = await db.Accounts.FirstAsync(a => a.Name == "Damage & Loss");
            var cash = await db.Accounts.FirstAsync(a => a.Name == "Cash in Hand");

            var category = await db.ExpenseCategories.FirstOrDefaultAsync();
            if (category == null)
            {
                category = new ExpenseCategory
                {
                    Name = "Utilities",
                    DefaultExpenseAccount_ID = expenseAccount.AccountID,
                    IsActive = true,
                    CreatedAt = AppTime.Now,
                    CreatedBy = TenantConstants.SeedUserId
                };
                db.ExpenseCategories.Add(category);
                await db.SaveChangesAsync();
            }

            var expense = await expenseService.CreateAsync(new Expense
            {
                ExpenseCategory_ID = category.ExpenseCategoryID,
                ExpenseAccount_ID = expenseAccount.AccountID,
                SourceAccount_ID = cash.AccountID,
                Amount = 250m,
                ExpenseDate = AppTime.Now,
                Description = "RBAC probe expense"
            }, TenantConstants.SeedUserId);

            return expense.ExpenseID;
        });

    private async Task<int> SeedApprovedExpenseAsync()
    {
        var id = await SeedDraftExpenseAsync();
        await _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            await sp.GetRequiredService<IExpenseService>().ApproveAsync(id, TenantConstants.SeedUserId);
        });
        return id;
    }

    private Task<TransactionStatus> ExpenseStatusAsync(int expenseId)
        => _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var e = await db.Expenses.AsNoTracking().FirstAsync(x => x.ExpenseID == expenseId);
            return e.Status;
        });

    private Task<int> SeedSupplierCreditNoteAsync()
        => _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var noteService = sp.GetRequiredService<ISupplierCreditNoteService>();
            var partyService = sp.GetRequiredService<PharmaCare.Application.Interfaces.Configuration.IPartyService>();

            var supplier = await partyService.CreateAsync(new PharmaCare.Domain.Entities.Configuration.Party
            {
                Name = $"SupCN-{Guid.NewGuid():N}".Substring(0, 12),
                PartyType = "Supplier",
                OpeningBalance = 0m
            }, TenantConstants.SeedUserId);

            var adjustment = await db.Accounts.FirstAsync(a => a.Name == "Damage & Loss");

            var note = await noteService.CreateAsync(new SupplierCreditNote
            {
                Party_ID = supplier.PartyID,
                AdjustmentAccount_ID = adjustment.AccountID,
                TotalAmount = 300m,
                CreditDate = AppTime.Now,
                Remarks = "RBAC probe credit note"
            }, TenantConstants.SeedUserId);

            return note.SupplierCreditNoteID;
        });

    private Task<int> CurrentPeriodIdAsync()
        => _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var period = await db.FinancialPeriods.AsNoTracking()
                .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);
            return period.PeriodID;
        });

    private Task<bool> IsPeriodClosedAsync(int periodId)
        => _fx.Factory.RunInTenantScopeAsync(_fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var period = await db.FinancialPeriods.AsNoTracking().FirstAsync(p => p.PeriodID == periodId);
            return period.IsClosed;
        });

    /// <summary>Restores the shared tenant to "period open" so unrelated probes still post.</summary>
    private async Task ReopenAsAdminAsync(HttpClient admin, int periodId)
    {
        await HttpTestHelpers.PostFormAsync(admin, "/FinancialPeriod/Open",
            new Dictionary<string, string> { ["id"] = periodId.ToString() },
            tokenPath: TokenPage);
    }
}
