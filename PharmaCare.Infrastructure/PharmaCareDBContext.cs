using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Infrastructure;

/// <summary>
/// Database Context for PharmaCare application
/// </summary>
public class PharmaCareDBContext : IdentityUserContext<User, int>
{
    public PharmaCareDBContext(DbContextOptions<PharmaCareDBContext> options)
        : base(options)
    {
    }

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

    // ========== ACCOUNTING ==========
    public DbSet<AccountFamily> AccountFamilies { get; set; } = null!;
    public DbSet<AccountHead> AccountHeads { get; set; } = null!;
    public DbSet<AccountSubhead> AccountSubheads { get; set; } = null!;
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AccountType> AccountTypes { get; set; } = null!;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ========== IDENTITY TABLES ==========
        builder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
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
            entity.HasIndex(e => e.Name).IsUnique();
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
            entity.HasIndex(e => e.TransactionNo).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_StockMains_Status_Valid", "[Status] IN ('Draft','Approved','Void')");
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
            entity.HasIndex(e => e.VoucherNo).IsUnique();

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
    }
}
