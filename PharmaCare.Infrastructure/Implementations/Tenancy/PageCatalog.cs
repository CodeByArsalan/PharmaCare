using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations.Tenancy;

/// <summary>
/// The application's feature catalog — the single source of truth for the <c>Pages</c> and
/// <c>PageUrls</c> tables, which drive BOTH the sidebar and <c>PageAuthorizationFilter</c>.
///
/// <para>
/// Why this exists: the catalog used to be populated only by ad-hoc raw-SQL data migrations that
/// covered a fraction of the app, so most of it existed solely as hand-inserted rows in one
/// developer database. A database built purely from migrations left Products, Parties, Users,
/// Roles, Chart of Accounts, Expenses and every report module with no catalog row — which the
/// authorization filter treats as "no permission", making them unreachable and invisible.
/// Anything reachable in the UI must be declared here, not typed into SSMS.
/// </para>
///
/// <para>
/// Seeding is ADDITIVE and idempotent: rows are matched on Controller+Action (groups on Title)
/// and only inserted when missing. Titles, icons, ordering and hierarchy of existing rows are
/// never modified or deleted, so hand-tuning already in a database survives untouched.
/// </para>
/// </summary>
internal static class PageCatalog
{
    /// <summary>A top-level sidebar section. Has no controller/action of its own.</summary>
    private sealed record Group(string Title, string Icon, int DisplayOrder);

    /// <summary>
    /// A catalog page. <paramref name="Urls"/> are the additional "Controller/Action" pairs that
    /// belong to this page's permission — every secondary action (Add/Edit/Delete/AJAX) must be
    /// listed or the filter denies it, since it resolves permissions by controller+action.
    /// </summary>
    private sealed record PageDef(
        string GroupTitle,
        int DisplayOrder,
        string Title,
        string? Icon,
        string Controller,
        string Action,
        string[] Urls,
        bool IsVisible = true);

    private static readonly Group[] Groups =
    {
        new("Administration",      "fas fa-user-shield",    1),
        new("Configuration",       "fas fa-cog",            2),
        new("Account Management",  "fas fa-calculator",     3),
        new("Purchase Management", "fas fa-shopping-cart",  4),
        new("Sale Management",     "fas fa-cash-register",  5),
        new("Expense Management",  "fas fa-wallet",         6),
        new("Report",              "fas fa-book",           7),
        new("Activity Log",        "fas fa-history",        8),
    };

    // NOTE: SupplierPayment/AdvancePayment and SupplierPayment/AdvancePaymentsIndex deliberately
    // omitted — rows for them exist in older databases but the actions no longer exist in source.
    private static readonly PageDef[] Pages =
    {
        // ── Administration ────────────────────────────────────────────────────────────────
        new("Administration", 1, "Users", "fas fa-user", "User", "UsersIndex",
            new[] { "User/AddUser", "User/EditUser", "User/ToggleStatus" }),
        new("Administration", 2, "Roles", "fas fa-user-tag", "Role", "RolesIndex",
            new[] { "Role/AddRole", "Role/EditRole", "Role/Permissions", "Role/SavePermissions", "Role/ToggleStatus" }),
        new("Administration", 3, "Profit Setting", null, "ProfitSettings", "Index",
            Array.Empty<string>()),

        // ── Configuration ─────────────────────────────────────────────────────────────────
        new("Configuration", 1, "Parties", "fas fa-users", "Party", "PartiesIndex",
            new[] { "Party/AddParty", "Party/EditParty", "Party/Delete" }),
        new("Configuration", 2, "Categories", "fas fa-tags", "Category", "CategoriesIndex",
            new[] { "Category/AddCategory", "Category/EditCategory", "Category/Delete" }),
        new("Configuration", 3, "Sub Categories", "fas fa-tags", "SubCategory", "SubCategoriesIndex",
            new[] { "SubCategory/AddSubCategory", "SubCategory/EditSubCategory", "SubCategory/Delete" }),
        // Product/PriceHistory was missing from every existing database: the screen and its view
        // exist and are linked from the product list, but every user got Access Denied.
        new("Configuration", 4, "Products", "fas fa-pills", "Product", "ProductsIndex",
            new[] { "Product/AddProduct", "Product/EditProduct", "Product/Delete",
                    "Product/GetSubCategoriesByCategoryId", "Product/PriceHistory" }),
        new("Configuration", 50, "Financial Periods", "fas fa-calendar-alt", "FinancialPeriod", "Index",
            new[] { "FinancialPeriod/Create", "FinancialPeriod/Close", "FinancialPeriod/Open" }),

        // ── Account Management ────────────────────────────────────────────────────────────
        new("Account Management", 1, "Account Head", "fas fa-sitemap", "AccountHead", "AccountHeadsIndex",
            new[] { "AccountHead/AddAccountHead", "AccountHead/EditAccountHead", "AccountHead/Delete" }),
        new("Account Management", 2, "Account Sub Head", "fas fa-sitemap", "AccountSubHead", "AccountSubHeadsIndex",
            new[] { "AccountSubHead/AddAccountSubHead", "AccountSubHead/EditAccountSubHead", "AccountSubHead/Delete" }),
        new("Account Management", 3, "Chart of Accounts", "fas fa-sitemap", "ChartOfAccount", "AccountsIndex",
            new[] { "ChartOfAccount/AddAccount", "ChartOfAccount/EditAccount", "ChartOfAccount/Delete",
                    "ChartOfAccount/GetSubHeadsByHeadId" }),
        // JournalVoucher/ViewJournalVoucher was likewise missing everywhere — an existing screen
        // that no one could open. Note the index action is JournalVoucherIndex, NOT "Index": an
        // old migration seeded the latter, which points at an action that does not exist.
        new("Account Management", 4, "Journal Vouchers", "fas fa-book", "JournalVoucher", "JournalVoucherIndex",
            new[] { "JournalVoucher/AddJournalVoucher", "JournalVoucher/ViewJournalVoucher",
                    "JournalVoucher/ReverseJournalVoucher" }),

        // ── Purchase Management ───────────────────────────────────────────────────────────
        new("Purchase Management", 1, "Purchase Orders", "fas fa-file-alt", "PurchaseOrder", "PurchaseOrdersIndex",
            new[] { "PurchaseOrder/AddPurchaseOrder", "PurchaseOrder/EditPurchaseOrder", "PurchaseOrder/Approve",
                    "PurchaseOrder/Delete", "PurchaseOrder/GetProducts", "PurchaseOrder/ViewPurchaseOrder",
                    "PurchaseOrder/MakePayment" }),
        new("Purchase Management", 2, "Purchases", "fas fa-truck", "Purchase", "PurchasesIndex",
            new[] { "Purchase/AddPurchase", "Purchase/EditPurchase", "Purchase/ViewPurchase", "Purchase/Void",
                    "Purchase/GetPurchaseOrders" }),
        new("Purchase Management", 3, "Purchase Returns", "fas fa-undo", "PurchaseReturn", "PurchaseReturnsIndex",
            new[] { "PurchaseReturn/AddPurchaseReturn", "PurchaseReturn/ViewPurchaseReturn", "PurchaseReturn/Void",
                    "PurchaseReturn/GetGrns", "PurchaseReturn/GetProducts" }),
        new("Purchase Management", 4, "Supplier Payments", "fas fa-money-bill-wave", "SupplierPayment", "PaymentsIndex",
            new[] { "SupplierPayment/MakePayment", "SupplierPayment/ViewPayment", "SupplierPayment/GetAccountsByType",
                    "SupplierPayment/GetPaymentHistory", "SupplierPayment/RefundAdvance" }),
        new("Purchase Management", 50, "Stock Adjustments", "fas fa-balance-scale", "StockAdjustment", "AdjustmentIndex",
            new[] { "StockAdjustment/AddAdjustment", "StockAdjustment/ViewAdjustment", "StockAdjustment/Void" }),
        new("Purchase Management", 51, "Supplier Credit Notes", "fas fa-credit-card", "SupplierCreditNote", "SupplierCreditNotesIndex",
            new[] { "SupplierCreditNote/AddSupplierCreditNote", "SupplierCreditNote/ViewSupplierCreditNote",
                    "SupplierCreditNote/Void" }),
        new("Purchase Management", 52, "Supplier Reconciliation", "fas fa-balance-scale", "SupplierPayment", "SupplierReconciliation",
            Array.Empty<string>()),

        // ── Sale Management ───────────────────────────────────────────────────────────────
        new("Sale Management", 1, "Sales", "fas fa-history", "Sale", "SalesIndex",
            new[] { "Sale/AddSale", "Sale/ViewSale", "Sale/Void", "Sale/Receipt",
                    "Sale/GetProducts", "Sale/GetCustomerInfo" }),
        new("Sale Management", 2, "Sale Returns", "fas fa-exchange-alt", "SaleReturn", "SaleReturnsIndex",
            new[] { "SaleReturn/AddSaleReturn", "SaleReturn/ViewSaleReturn", "SaleReturn/Void",
                    "SaleReturn/GetSales", "SaleReturn/GetProducts" }),
        new("Sale Management", 3, "Customer Payments", "fas fa-money-bill-wave", "CustomerPayment", "ReceiptsIndex",
            new[] { "CustomerPayment/ReceivePayment", "CustomerPayment/ViewReceipt", "CustomerPayment/PendingSales",
                    "CustomerPayment/Refund", "CustomerPayment/RefundsIndex", "CustomerPayment/CreditNotesIndex",
                    "CustomerPayment/Reconciliation", "CustomerPayment/ApplyCreditNote",
                    "CustomerPayment/GetAccountsByType", "CustomerPayment/GetReceiptHistory" }),

        // ── Expense Management ────────────────────────────────────────────────────────────
        new("Expense Management", 1, "Expense Categories", "fas fa-tags", "Expense", "ExpenseCategoriesIndex",
            new[] { "Expense/AddExpenseCategory", "Expense/EditExpenseCategory", "Expense/DeleteExpenseCategory" }),
        // GetAccountsByType and GetCategoryDefaults were missing everywhere: both are AJAX calls
        // on the expense form, and a denied AJAX call redirects to an HTML page, so the account
        // dropdown and category defaults failed silently.
        new("Expense Management", 2, "Expenses", "fas fa-file-invoice-dollar", "Expense", "ExpensesIndex",
            new[] { "Expense/AddExpense", "Expense/ViewExpense", "Expense/Approve", "Expense/Void",
                    "Expense/BudgetManagement", "Expense/GetAccountsByType", "Expense/GetCategoryDefaults" }),

        // ── Report ────────────────────────────────────────────────────────────────────────
        // Hidden from the sidebar (IsVisible: false) but still permission-checked: it is the
        // report hub, reached from the report sub-pages rather than the menu.
        new("Report", 1, "Report", null, "Report", "Index",
            Array.Empty<string>(), IsVisible: false),
        new("Report", 2, "PartyReport", "fas fa-user-friends", "PartyReport", "PartyReportIndex",
            new[] { "PartyReport/CustomerLedger", "PartyReport/SupplierLedger" }),
        new("Report", 3, "PurchaseReport", "fas fa-shopping-cart", "PurchaseReport", "PurchaseReportIndex",
            new[] { "PurchaseReport/PurchaseReport", "PurchaseReport/PurchaseBySupplier" }),
        new("Report", 4, "SalesReport", "fas fa-chart-pie", "SalesReport", "SalesReportIndex",
            new[] { "SalesReport/SalesReport", "SalesReport/DailySalesSummary", "SalesReport/SalesByProduct",
                    "SalesReport/SalesByCustomer", "SalesReport/CustomerBalanceSummary" }),
        new("Report", 5, "InventoryReport", "fas fa-boxes", "InventoryReport", "InventoryReportIndex",
            new[] { "InventoryReport/CurrentStock", "InventoryReport/LowStock", "InventoryReport/DeadStock",
                    "InventoryReport/ProductMovement" }),
        new("Report", 6, "FinancialReport", "fas fa-university", "FinancialReport", "FinancialReportIndex",
            new[] { "FinancialReport/ProfitLoss", "FinancialReport/CashFlow", "FinancialReport/TrialBalance",
                    "FinancialReport/GeneralLedger", "FinancialReport/ReceivablesAging",
                    "FinancialReport/PayablesAging", "FinancialReport/ExpenseReport" }),

        // ── Activity Log ──────────────────────────────────────────────────────────────────
        new("Activity Log", 1, "Activities", "fas fa-book", "ActivityLog", "Dashboard",
            new[] { "ActivityLog/Index", "ActivityLog/Details", "ActivityLog/EntityHistory",
                    "ActivityLog/UserHistory" }),
    };

    /// <summary>
    /// Inserts any catalog rows that do not exist yet, then grants the newly created pages to each
    /// pharmacy's system Administrator role. Existing rows are never modified or removed.
    /// </summary>
    public static async Task SeedAsync(PharmaCareDBContext context, ILogger logger)
    {
        var newPageIds = new List<int>();

        // ---- 1. Top-level groups, matched by Title (they have no controller/action). ----
        var existingByTitle = await context.Pages
            .Where(p => p.Parent_ID == null && p.Controller == null)
            .ToDictionaryAsync(p => p.Title, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var group in Groups)
        {
            if (existingByTitle.ContainsKey(group.Title)) continue;

            var page = new Page
            {
                Title = group.Title,
                Icon = group.Icon,
                Parent_ID = null,
                DisplayOrder = group.DisplayOrder,
                IsActive = true,
                IsVisible = true,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            context.Pages.Add(page);
            existingByTitle[group.Title] = page;
        }

        await context.SaveChangesAsync();

        // ---- 2. Pages, matched by Controller + Action. ----
        var existingPages = await context.Pages
            .Where(p => p.Controller != null && p.Action != null)
            .ToListAsync();

        var pageKey = (string c, string a) => $"{c}/{a}".ToLowerInvariant();
        var existingPageKeys = existingPages
            .ToDictionary(p => pageKey(p.Controller!, p.Action!), p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var def in Pages)
        {
            if (existingPageKeys.ContainsKey(pageKey(def.Controller, def.Action))) continue;

            if (!existingByTitle.TryGetValue(def.GroupTitle, out var parent))
            {
                logger.LogWarning("Page catalog: group '{Group}' not found for {Controller}/{Action}; skipped.",
                    def.GroupTitle, def.Controller, def.Action);
                continue;
            }

            var page = new Page
            {
                Title = def.Title,
                Icon = def.Icon,
                Parent_ID = parent.PageID,
                DisplayOrder = def.DisplayOrder,
                Controller = def.Controller,
                Action = def.Action,
                IsActive = true,
                IsVisible = def.IsVisible,
                CreatedAt = AppTime.Now,
                CreatedBy = 0
            };
            context.Pages.Add(page);
            existingPageKeys[pageKey(def.Controller, def.Action)] = page;
        }

        await context.SaveChangesAsync();

        // Anything added above now has a real identity value.
        newPageIds.AddRange(existingPageKeys.Values
            .Where(p => !existingPages.Any(e => e.PageID == p.PageID))
            .Select(p => p.PageID));

        // ---- 3. PageUrls (secondary actions), matched by Controller + Action. ----
        var existingUrlKeys = (await context.PageUrls.Select(u => new { u.Controller, u.Action }).ToListAsync())
            .Select(u => $"{u.Controller}/{u.Action}".ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedUrls = 0;
        foreach (var def in Pages)
        {
            if (def.Urls.Length == 0) continue;
            if (!existingPageKeys.TryGetValue(pageKey(def.Controller, def.Action), out var owner)) continue;

            foreach (var url in def.Urls)
            {
                var parts = url.Split('/', 2);
                if (parts.Length != 2) continue;
                if (!existingUrlKeys.Add(url.ToLowerInvariant())) continue;

                context.PageUrls.Add(new PageUrl
                {
                    Page_ID = owner.PageID,
                    Controller = parts[0],
                    Action = parts[1]
                });
                addedUrls++;
            }
        }

        await context.SaveChangesAsync();

        // ---- 4. Grant newly created pages to each pharmacy's Administrator role. ----
        var grants = 0;
        if (newPageIds.Count > 0)
        {
            grants = await GrantPagesToAdminRolesAsync(context, newPageIds);
        }

        if (newPageIds.Count > 0 || addedUrls > 0 || grants > 0)
        {
            logger.LogInformation(
                "Page catalog seeded: {Pages} page(s), {Urls} page url(s), {Grants} admin permission(s) added.",
                newPageIds.Count, addedUrls, grants);
        }
    }

    /// <summary>
    /// Grants full permissions on <paramref name="pageIds"/> to every pharmacy's system
    /// Administrator role — mirroring what TenantProvisioningService does for a new tenant.
    /// Non-admin roles are deliberately untouched so nobody silently gains access.
    /// </summary>
    private static async Task<int> GrantPagesToAdminRolesAsync(PharmaCareDBContext context, List<int> pageIds)
    {
        // Roles and RolePages are tenant-filtered, and this runs at startup with no tenant in
        // scope — IgnoreQueryFilters is required or every query comes back empty.
        var adminRoles = await context.Set<Role>()
            .IgnoreQueryFilters()
            .Where(r => r.IsSystemRole && r.IsActive)
            .Select(r => new { r.RoleID, r.Pharmacy_ID })
            .ToListAsync();

        if (adminRoles.Count == 0) return 0;

        var roleIds = adminRoles.Select(r => r.RoleID).ToList();
        var existing = (await context.Set<RolePage>()
                .IgnoreQueryFilters()
                .Where(rp => roleIds.Contains(rp.Role_ID) && pageIds.Contains(rp.Page_ID))
                .Select(rp => new { rp.Role_ID, rp.Page_ID })
                .ToListAsync())
            .Select(rp => (rp.Role_ID, rp.Page_ID))
            .ToHashSet();

        var added = 0;
        foreach (var role in adminRoles)
        {
            foreach (var pageId in pageIds)
            {
                if (!existing.Add((role.RoleID, pageId))) continue;

                context.Set<RolePage>().Add(new RolePage
                {
                    // Set explicitly: DbContext.StampTenant throws for tenant rows inserted with
                    // no ambient tenant, which is exactly the case at startup.
                    Pharmacy_ID = role.Pharmacy_ID,
                    Role_ID = role.RoleID,
                    Page_ID = pageId,
                    CanView = true,
                    CanCreate = true,
                    CanEdit = true,
                    CanDelete = true
                });
                added++;
            }
        }

        await context.SaveChangesAsync();
        return added;
    }
}
