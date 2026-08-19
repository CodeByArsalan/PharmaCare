using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Tenancy;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Infrastructure;

/// <summary>
/// Database Context for PharmaCare application
/// </summary>
public class PharmaCareDBContext : IdentityUserContext<User, int>
{
    private readonly ICurrentTenant _currentTenant;

    public PharmaCareDBContext(DbContextOptions<PharmaCareDBContext> options, ICurrentTenant? currentTenant = null)
        : base(options)
    {
        // currentTenant is null only in design-time contexts (dotnet ef), where query filters
        // are recorded but never executed. NullTenant keeps the filter expression NRE-safe.
        _currentTenant = currentTenant ?? NullTenant.Instance;
    }

    // ========== TENANCY ==========
    public DbSet<Pharmacy> Pharmacies { get; set; } = null!;

    // ========== SECURITY ==========
    public DbSet<Role> Roles_Custom { get; set; } = null!;
    public DbSet<UserRole> UserRoles_Custom { get; set; } = null!;
    public DbSet<Page> Pages { get; set; } = null!;
    public DbSet<PageUrl> PageUrls { get; set; } = null!;
    public DbSet<RolePage> RolePages { get; set; } = null!;

    // ========== CONFIGURATION ==========
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<SubCategory> SubCategories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Party> Parties { get; set; } = null!;
    public DbSet<PriceType> PriceTypes { get; set; } = null!;
    public DbSet<ProductPrice> ProductPrices { get; set; } = null!;
    public DbSet<ProductPriceHistory> ProductPriceHistories { get; set; } = null!;
    public DbSet<ProfitSettings> ProfitSettings { get; set; } = null!;

    // ========== ACCOUNTING ==========
    public DbSet<AccountFamily> AccountFamilies { get; set; } = null!;
    public DbSet<AccountHead> AccountHeads { get; set; } = null!;
    public DbSet<AccountSubhead> AccountSubheads { get; set; } = null!;
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AccountType> AccountTypes { get; set; } = null!;
    public DbSet<FinancialPeriod> FinancialPeriods { get; set; } = null!;

    // ========== TRANSACTIONS ==========
    public DbSet<TransactionType> TransactionTypes { get; set; } = null!;
    public DbSet<StockMain> StockMains { get; set; } = null!;
    public DbSet<StockDetail> StockDetails { get; set; } = null!;
    public DbSet<VoucherType> VoucherTypes { get; set; } = null!;
    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<VoucherDetail> VoucherDetails { get; set; } = null!;



    // ========== FINANCE ==========
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
    public DbSet<Expense> Expenses { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<CreditNote> CreditNotes { get; set; } = null!;
    public DbSet<SupplierCreditNote> SupplierCreditNotes { get; set; } = null!;
    public DbSet<PaymentAllocation> PaymentAllocations { get; set; } = null!;
    public DbSet<ExpenseBudget> ExpenseBudgets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ========== TENANCY ==========
        builder.Entity<Pharmacy>(entity =>
        {
            entity.ToTable("Pharmacies");
            entity.HasKey(e => e.PharmacyID);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
        });

        // ========== IDENTITY TABLES ==========
        builder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            // Users are tenant-scoped by column but NOT globally query-filtered: login must
            // resolve a user by (globally unique) email before any tenant is known, and platform
            // super-admins have a NULL Pharmacy_ID. User listings filter by tenant explicitly.
            entity.HasOne<Pharmacy>()
                  .WithMany()
                  .HasForeignKey(e => e.Pharmacy_ID)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.Pharmacy_ID);
        });

        builder.Entity<IdentityUserClaim<int>>(entity =>
        {
            entity.ToTable("IdentityUserClaims");
        });

        builder.Entity<IdentityUserLogin<int>>(entity =>
        {
            entity.ToTable("IdentityUserLogins");
        });

        builder.Entity<IdentityUserToken<int>>(entity =>
        {
            entity.ToTable("IdentityUserTokens");
        });

        // ========== SECURITY ==========
        builder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.RoleID);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            // Role names are unique per pharmacy (each tenant can have its own "Admin").
            entity.HasIndex(e => new { e.Pharmacy_ID, e.Name }).IsUnique();
        });

        builder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(e => e.UserRoleID);
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.User_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.Role_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.User_ID, e.Role_ID }).IsUnique();
        });

        builder.Entity<Page>(entity =>
        {
            entity.ToTable("Pages");
            entity.HasKey(e => e.PageID);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.ParentPage)
                .WithMany(p => p.ChildPages)
                .HasForeignKey(e => e.Parent_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RolePage>(entity =>
        {
            entity.ToTable("RolePages");
            entity.HasKey(e => e.RolePageID);
            entity.HasOne(e => e.Role)
                .WithMany(r => r.RolePages)
                .HasForeignKey(e => e.Role_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Page)
                .WithMany(p => p.RolePages)
                .HasForeignKey(e => e.Page_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.Role_ID, e.Page_ID }).IsUnique();
        });

        builder.Entity<PageUrl>(entity =>
        {
            entity.ToTable("PageUrls");
            entity.HasKey(e => e.PageUrlID);
            entity.Property(e => e.Controller).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Page)
                .WithMany(p => p.PageUrls)
                .HasForeignKey(e => e.Page_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.Controller, e.Action });
        });

        // ========== CONFIGURATION ==========

        builder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(e => e.CategoryID);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.StockAccount)
                .WithMany()
                .HasForeignKey(e => e.StockAccount_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SaleAccount)
                .WithMany()
                .HasForeignKey(e => e.SaleAccount_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.COGSAccount)
                .WithMany()
                .HasForeignKey(e => e.COGSAccount_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DamageAccount)
                .WithMany()
                .HasForeignKey(e => e.DamageAccount_ID)
                .OnDelete(DeleteBehavior.Restrict);

        });

        builder.Entity<SubCategory>(entity =>
        {
            entity.ToTable("SubCategories");
            entity.HasKey(e => e.SubCategoryID);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Category)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(e => e.Category_ID)
                .OnDelete(DeleteBehavior.Restrict);

        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(e => e.ProductID);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UnitsInPack).HasDefaultValue(1);
            entity.ToTable(t => t.HasCheckConstraint("CK_Products_UnitsInPack_Positive", "[UnitsInPack] > 0"));
            entity.HasIndex(e => e.ShortCode);

            entity.HasOne(e => e.Category)
                .WithMany(s => s.Products)
                .HasForeignKey(e => e.Category_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SubCategory)
                .WithMany(s => s.Products)
                .HasForeignKey(e => e.SubCategory_ID)
                .OnDelete(DeleteBehavior.Restrict);

        });

        builder.Entity<Party>(entity =>
        {
            entity.ToTable("Parties");
            entity.HasKey(e => e.PartyID);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PartyType).IsRequired().HasMaxLength(20);

        });

        builder.Entity<PriceType>(entity =>
        {
            entity.ToTable("PriceTypes");
            entity.HasKey(e => e.PriceTypeID);
            entity.Property(e => e.PriceTypeName).IsRequired().HasMaxLength(100);
        });

        builder.Entity<ProductPrice>(entity =>
        {
            entity.ToTable("ProductPrices");
            entity.HasKey(e => e.ProductPriceID);
            
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.Product_ID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PriceType)
                .WithMany()
                .HasForeignKey(e => e.PriceType_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductPriceHistory>(entity =>
        {
            entity.ToTable("ProductPriceHistories");
            entity.HasKey(e => e.ProductPriceHistoryID);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.Product_ID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PriceType)
                .WithMany()
                .HasForeignKey(e => e.PriceType_ID)
                .OnDelete(DeleteBehavior.Restrict);

            // Fast lookup of the currently-effective row per product + price type.
            entity.HasIndex(e => new { e.Product_ID, e.PriceType_ID, e.EffectiveTo });

            // Invariant: at most ONE open (EffectiveTo IS NULL) row per product + price type.
            // Without this, two open rows make "the current price" ambiguous.
            entity.HasIndex(e => new { e.Product_ID, e.PriceType_ID })
                .IsUnique()
                .HasFilter("[EffectiveTo] IS NULL")
                .HasDatabaseName("UX_ProductPriceHistories_OpenRow");
        });

        builder.Entity<ProfitSettings>(entity =>
        {
            entity.ToTable("ProfitSettings");
            entity.HasKey(e => e.SettingsID);
            // No global seed: ProfitSettings is now per-pharmacy and seeded during tenant provisioning.
        });

        // ========== ACCOUNTING ==========
        builder.Entity<AccountFamily>(entity =>
        {
            entity.ToTable("AccountFamilies");
            entity.HasKey(e => e.AccountFamilyID);
            entity.Property(e => e.FamilyName).IsRequired().HasMaxLength(100);
        });

        builder.Entity<AccountHead>(entity =>
        {
            entity.ToTable("AccountHeads");
            entity.HasKey(e => e.AccountHeadID);
            entity.Property(e => e.HeadName).IsRequired().HasMaxLength(100);
            // Lookup by well-known code within a tenant (account resolution replaces hardcoded IDs).
            // UNIQUE (filtered): duplicate codes would make well-known-code resolution ambiguous.
            entity.HasIndex(e => new { e.Pharmacy_ID, e.Code })
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");
            entity.HasOne(e => e.AccountFamily)
                .WithMany(f => f.AccountHeads)
                .HasForeignKey(e => e.AccountFamily_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountSubhead>(entity =>
        {
            entity.ToTable("AccountSubheads");
            entity.HasKey(e => e.AccountSubheadID);
            entity.Property(e => e.SubheadName).IsRequired().HasMaxLength(100);
            // Lookup by well-known code within a tenant (account resolution replaces hardcoded IDs).
            // UNIQUE (filtered): duplicate codes would make well-known-code resolution ambiguous.
            entity.HasIndex(e => new { e.Pharmacy_ID, e.Code })
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");
            entity.HasOne(e => e.AccountHead)
                .WithMany(h => h.AccountSubheads)
                .HasForeignKey(e => e.AccountHead_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountType>(entity =>
        {
            entity.ToTable("AccountTypes");
            entity.HasKey(e => e.AccountTypeID);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        builder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(e => e.AccountID);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.AccountHead)
                .WithMany(s => s.Accounts)
                .HasForeignKey(e => e.AccountHead_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountSubhead)
                .WithMany(s => s.Accounts)
                .HasForeignKey(e => e.AccountSubhead_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountType)
                .WithMany(t => t.Accounts)
                .HasForeignKey(e => e.AccountType_ID)
                .OnDelete(DeleteBehavior.Restrict);


        });

        // ========== TRANSACTIONS ==========
        builder.Entity<TransactionType>(entity =>
        {
            entity.ToTable("TransactionTypes");
            entity.HasKey(e => e.TransactionTypeID);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        builder.Entity<StockMain>(entity =>
        {
            entity.ToTable("StockMains");
            entity.HasKey(e => e.StockMainID);
            entity.Property(e => e.TransactionNo).IsRequired().HasMaxLength(50);
            // Transaction numbers are unique per pharmacy (numbering restarts per tenant).
            entity.HasIndex(e => new { e.Pharmacy_ID, e.TransactionNo }).IsUnique();
            // Composite index for the dominant list/report query shape, tenant-first:
            // filter by pharmacy + transaction type (equality) + date range, with Status predicate.
            entity.HasIndex(e => new { e.Pharmacy_ID, e.TransactionType_ID, e.TransactionDate, e.Status })
                  .HasDatabaseName("IX_StockMains_Type_Date_Status");
            entity.ToTable(t =>
            {
                // 'Completed' is a real, persisted Purchase Order status: a PO auto-completes once
                // every ordered line has been received, and re-opens if a GRN against it is later
                // voided or reduced. PurchaseService owns both transitions.
                t.HasCheckConstraint("CK_StockMains_Status_Valid", "[Status] IN ('Draft','Approved','Completed','Void')");
                t.HasCheckConstraint("CK_StockMains_PaymentStatus_Valid", "[PaymentStatus] IN ('Unpaid','Partial','Paid')");
            });

            entity.HasOne(e => e.TransactionType)
                .WithMany()
                .HasForeignKey(e => e.TransactionType_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Party)
                .WithMany()
                .HasForeignKey(e => e.Party_ID)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Voucher)
                .WithMany()
                .HasForeignKey(e => e.Voucher_ID)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ReferenceStockMain)
                .WithMany()
                .HasForeignKey(e => e.ReferenceStockMain_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockDetail>(entity =>
        {
            entity.ToTable("StockDetails");
            entity.HasKey(e => e.StockDetailID);

            // Covers the per-product cost/stock lookups (ProductService.GetLastGrnCostPricesAsync
            // and the stock-movement aggregates), which filter by tenant + product and pick the
            // newest StockMain. Including CostPrice/Quantity keeps them index-only.
            entity.HasIndex(e => new { e.Pharmacy_ID, e.Product_ID, e.StockMain_ID })
                  .IncludeProperties(e => new { e.CostPrice, e.Quantity })
                  .HasDatabaseName("IX_StockDetails_Product_StockMain");
            entity.HasOne(e => e.StockMain)
                .WithMany(m => m.StockDetails)
                .HasForeignKey(e => e.StockMain_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.Product_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VoucherType>(entity =>
        {
            entity.ToTable("VoucherTypes");
            entity.HasKey(e => e.VoucherTypeID);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        builder.Entity<Voucher>(entity =>
        {
            entity.ToTable("Vouchers");
            entity.HasKey(e => e.VoucherID);
            entity.Property(e => e.VoucherNo).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.Pharmacy_ID, e.VoucherNo }).IsUnique();

            entity.HasOne(e => e.VoucherType)
                .WithMany()
                .HasForeignKey(e => e.VoucherType_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReversedByVoucher)
                .WithOne()
                .HasForeignKey<Voucher>(e => e.ReversedByVoucher_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReversesVoucher)
                .WithOne()
                .HasForeignKey<Voucher>(e => e.ReversesVoucher_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VoucherDetail>(entity =>
        {
            entity.ToTable("VoucherDetails");
            entity.HasKey(e => e.VoucherDetailID);
            entity.HasOne(e => e.Voucher)
                .WithMany(v => v.VoucherDetails)
                .HasForeignKey(e => e.Voucher_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.Account_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Party)
                .WithMany()
                .HasForeignKey(e => e.Party_ID)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.Product_ID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ========== FINANCE ==========
        builder.Entity<ExpenseCategory>(entity =>
        {
            entity.ToTable("ExpenseCategories");
            entity.HasKey(e => e.ExpenseCategoryID);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.ParentCategory)
                .WithMany(c => c.ChildCategories)
                .HasForeignKey(e => e.Parent_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DefaultExpenseAccount)
                .WithMany()
                .HasForeignKey(e => e.DefaultExpenseAccount_ID)
                .OnDelete(DeleteBehavior.SetNull);

        });

        builder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(e => e.ExpenseID);
            entity.HasOne(e => e.ExpenseCategory)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.ExpenseCategory_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SourceAccount)
                .WithMany()
                .HasForeignKey(e => e.SourceAccount_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExpenseAccount)
                .WithMany()
                .HasForeignKey(e => e.ExpenseAccount_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Voucher)
                .WithMany()
                .HasForeignKey(e => e.Voucher_ID)
                .OnDelete(DeleteBehavior.SetNull);

        });

        builder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.PaymentID);
            entity.Property(e => e.PaymentType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.IsVoided).HasDefaultValue(false);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Payments_PaymentType_Valid",
                    $"[PaymentType] IN ('{PaymentType.RECEIPT}','{PaymentType.PAYMENT}','{PaymentType.REFUND}','{PaymentType.ADJUSTMENT}')");
            });
            entity.HasOne(e => e.Party)
                .WithMany()
                .HasForeignKey(e => e.Party_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.StockMain)
                .WithMany()
                .HasForeignKey(e => e.StockMain_ID)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.Account_ID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Voucher)
                .WithMany()
                .HasForeignKey(e => e.Voucher_ID)
                .OnDelete(DeleteBehavior.SetNull);

        });

        builder.Entity<CreditNote>(entity =>
        {
            entity.ToTable("CreditNotes");
            entity.HasKey(e => e.CreditNoteID);
            entity.Property(e => e.CreditNoteNo).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.Pharmacy_ID, e.CreditNoteNo }).IsUnique();
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_CreditNotes_Status_Valid", "[Status] IN ('Open','Applied','Void')");
            });

            entity.HasOne(e => e.Party)
                .WithMany()
                .HasForeignKey(e => e.Party_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SourceStockMain)
                .WithMany()
                .HasForeignKey(e => e.SourceStockMain_ID)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Voucher)
                .WithMany()
                .HasForeignKey(e => e.Voucher_ID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SupplierCreditNote>(entity =>
        {
            entity.ToTable("SupplierCreditNotes");
            entity.HasKey(e => e.SupplierCreditNoteID);
            entity.Property(e => e.CreditNoteNo).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.Pharmacy_ID, e.CreditNoteNo }).IsUnique();
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SupplierCreditNotes_Status_Valid", "[Status] IN ('Open','Applied','Void')");
            });

            entity.HasOne(e => e.Party)
                .WithMany()
                .HasForeignKey(e => e.Party_ID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SourceStockMain)
                .WithMany()
                .HasForeignKey(e => e.SourceStockMain_ID)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Voucher)
                .WithMany()
                .HasForeignKey(e => e.Voucher_ID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("PaymentAllocations");
            entity.HasKey(e => e.PaymentAllocationID);
            entity.Property(e => e.SourceType).HasMaxLength(20);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_PaymentAllocations_Source_Valid", "[SourceType] IN ('Receipt','CreditNote','Refund')");
                t.HasCheckConstraint("CK_PaymentAllocations_Source_NotNull", "[Payment_ID] IS NOT NULL OR [CreditNote_ID] IS NOT NULL");
            });

            entity.HasOne(e => e.Payment)
                .WithMany(p => p.PaymentAllocations)
                .HasForeignKey(e => e.Payment_ID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreditNote)
                .WithMany(c => c.PaymentAllocations)
                .HasForeignKey(e => e.CreditNote_ID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.StockMain)
                .WithMany()
                .HasForeignKey(e => e.StockMain_ID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseBudget>(entity =>
        {
            entity.ToTable("ExpenseBudgets");
            entity.HasKey(e => e.ExpenseBudgetID);
            entity.HasOne(e => e.ExpenseCategory)
                .WithMany()
                .HasForeignKey(e => e.ExpenseCategory_ID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.Pharmacy_ID, e.ExpenseCategory_ID, e.Year, e.Month }).IsUnique();
        });

        // ========== TENANT ISOLATION (applied to every ITenantEntity) ==========
        // Single choke point: for each tenant-owned entity, require Pharmacy_ID, index it, add a
        // FK to Pharmacy, and apply the global query filter. Adding a new tenant table later only
        // requires implementing ITenantEntity — it is picked up here automatically, so no per-table
        // filtering can be forgotten.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ITenantEntity).IsAssignableFrom(clrType) || clrType == typeof(ITenantEntity))
            {
                continue;
            }

            builder.Entity(clrType).Property(nameof(ITenantEntity.Pharmacy_ID)).IsRequired();
            builder.Entity(clrType).HasIndex(nameof(ITenantEntity.Pharmacy_ID));
            builder.Entity(clrType)
                   .HasOne(typeof(Pharmacy))
                   .WithMany()
                   .HasForeignKey(nameof(ITenantEntity.Pharmacy_ID))
                   .OnDelete(DeleteBehavior.Restrict);

            SetTenantFilterMethod.MakeGenericMethod(clrType).Invoke(this, new object[] { builder });
        }
    }

    private static readonly MethodInfo SetTenantFilterMethod =
        typeof(PharmaCareDBContext).GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private void SetTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity
    {
        // The instance member access (_currentTenant.TenantId) is re-evaluated by EF Core per query,
        // so the tenant set at login/provisioning is honoured. A null tenant matches no rows.
        builder.Entity<TEntity>().HasQueryFilter(e => e.Pharmacy_ID == _currentTenant.TenantId);
    }

    // ========== TENANT STAMPING ON WRITE ==========
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Stamps Pharmacy_ID on inserted tenant rows from the current tenant, and blocks moving an
    /// existing row to another tenant. Throws if a tenant row is inserted with no tenant in context
    /// (prevents accidentally unowned financial records).
    /// </summary>
    private void StampTenant()
    {
        var tenantId = _currentTenant.TenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (tenantId is > 0)
                {
                    // With a tenant in context the inbound value is NEVER trusted: a non-zero
                    // foreign id here is an over-posted form field trying to plant a row inside
                    // another pharmacy. Refuse loudly rather than silently re-stamping, so the
                    // attempt is visible.
                    if (entry.Entity.Pharmacy_ID != 0 && entry.Entity.Pharmacy_ID != tenantId.Value)
                    {
                        throw new InvalidOperationException(
                            $"Cannot insert {entry.Entity.GetType().Name}: its Pharmacy_ID does not match the current pharmacy.");
                    }

                    entry.Entity.Pharmacy_ID = tenantId.Value;
                }
                else if (entry.Entity.Pharmacy_ID == 0)
                {
                    throw new InvalidOperationException(
                        $"Cannot insert {entry.Entity.GetType().Name}: no current pharmacy (tenant) in context. " +
                        "Ensure the request is authenticated, or wrap the operation in ICurrentTenant.BeginScope(pharmacyId).");
                }
                // else: explicit Pharmacy_ID with no ambient tenant — the provisioning/seeding path.
            }
            else if (entry.State == EntityState.Modified)
            {
                // A row can never change its owning pharmacy.
                entry.Property(nameof(ITenantEntity.Pharmacy_ID)).IsModified = false;
            }
        }
    }

    /// <summary>Null-object tenant used only by design-time contexts (dotnet ef), which build the
    /// model but never execute queries, so the filter expression is never evaluated.</summary>
    private sealed class NullTenant : ICurrentTenant
    {
        public static readonly NullTenant Instance = new();
        public int? TenantId => null;
        public bool HasValue => false;
        public void SetTenant(int pharmacyId) { }
        public IDisposable BeginScope(int pharmacyId) => new NoopScope();
        private sealed class NoopScope : IDisposable { public void Dispose() { } }
    }
}
