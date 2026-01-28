using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Domain.Models.Prescriptions;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Infrastructure;

public class PharmaCareDBContext : IdentityDbContext<SystemUser, IdentityRole<int>, int>
{
    private readonly PharmaCare.Domain.Interfaces.IStoreContext _storeContext;

    public PharmaCareDBContext(DbContextOptions<PharmaCareDBContext> options,
                               PharmaCare.Domain.Interfaces.IStoreContext storeContext)
        : base(options)
    {
        _storeContext = storeContext;
    }

    #region Membership Tables
    public DbSet<UserWebPages> UserWebPages { get; set; }
    public DbSet<UserTypes> UserTypes { get; set; }
    public DbSet<WebPages> WebPages { get; set; }
    public DbSet<WebPageUrls> WebPageUrls { get; set; }
    #endregion

    #region Product & Inventory Tables
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductBatch> ProductBatches { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<SubCategory> SubCategories { get; set; }
    public DbSet<StoreInventory> StoreInventories { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<StockAlert> StockAlerts { get; set; }
    public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
    public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; }
    #endregion

    #region Sales Tables (Quotations only - Legacy Sales removed)
    public DbSet<PharmaCare.Domain.Models.SaleManagement.Quotation> Quotations { get; set; }
    public DbSet<PharmaCare.Domain.Models.SaleManagement.QuotationLine> QuotationLines { get; set; }
    #endregion

    #region Supporting Tables
    public DbSet<Store> Stores { get; set; }
    public DbSet<Prescription> Prescriptions { get; set; }
    #endregion

    #region Finance Tables
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    #endregion

    #region Accounting Tables
    public DbSet<PharmaCare.Domain.Models.AccountManagement.Head> Heads { get; set; }
    public DbSet<PharmaCare.Domain.Models.AccountManagement.Subhead> Subheads { get; set; }
    public DbSet<Party> Parties { get; set; }
    public DbSet<PharmaCare.Domain.Models.AccountManagement.AccountType> AccountTypes { get; set; }
    public DbSet<PharmaCare.Domain.Models.AccountManagement.ChartOfAccount> ChartOfAccounts { get; set; }


    // Fiscal Period Management
    public DbSet<PharmaCare.Domain.Models.AccountManagement.FiscalYear> FiscalYears { get; set; }
    public DbSet<PharmaCare.Domain.Models.AccountManagement.FiscalPeriod> FiscalPeriods { get; set; }
    public DbSet<PharmaCare.Domain.Models.AccountManagement.StoreFiscalPeriod> StoreFiscalPeriods { get; set; }
    public DbSet<PharmaCare.Domain.Models.AccountManagement.AccountMapping> AccountMappings { get; set; }
    public DbSet<PharmaCare.Domain.Models.Finance.SupplierPayment> SupplierPayments { get; set; }
    public DbSet<PharmaCare.Domain.Models.Finance.CustomerPayment> CustomerPayments { get; set; }
    #endregion

    #region Unified Transaction Tables (GreenEnergy Pattern)
    public DbSet<InvoiceType> InvoiceTypes { get; set; }
    public DbSet<AccountVoucherType> AccountVoucherTypes { get; set; }
    public DbSet<StockMain> StockMains { get; set; }
    public DbSet<StockDetail> StockDetails { get; set; }
    public DbSet<AccountVoucher> AccountVouchers { get; set; }
    public DbSet<AccountVoucherDetail> AccountVoucherDetails { get; set; }
    #endregion


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- GLOBAL QUERY FILTERS ---
        // Automatically filter store data based on CurrentStoreId from IStoreContext
        // Admins (IsAdmin == true) bypass these filters

        // PurchaseReturn
        builder.Entity<PurchaseReturn>().HasQueryFilter(x =>
            _storeContext.IsAdmin ||
            (x.Store_ID == _storeContext.CurrentStoreId)
        );

        // StockMovement
        builder.Entity<StockMovement>().HasQueryFilter(x =>
            _storeContext.IsAdmin ||
            (x.Store_ID == _storeContext.CurrentStoreId)
        );

        // StoreInventory
        builder.Entity<StoreInventory>().HasQueryFilter(x =>
            _storeContext.IsAdmin ||
            (x.Store_ID == _storeContext.CurrentStoreId)
        );

        // StockAlert
        builder.Entity<StockAlert>().HasQueryFilter(x =>
            _storeContext.IsAdmin ||
            (x.Store_ID == _storeContext.CurrentStoreId)
        );

        // Products - now link to SubCategory
        builder.Entity<Product>()
            .HasOne(p => p.SubCategory)
            .WithMany(sc => sc.Products)
            .HasForeignKey(p => p.SubCategory_ID);

        // ProductBatches
        builder.Entity<ProductBatch>()
            .HasOne(pb => pb.Product)
            .WithMany(p => p.ProductBatches)
            .HasForeignKey(pb => pb.Product_ID);

        builder.Entity<ProductBatch>()
            .HasIndex(pb => pb.BatchNumber)
            .IsUnique();

        // SubCategory -> Category
        builder.Entity<SubCategory>()
            .HasOne(sc => sc.Category)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(sc => sc.Category_ID);

        // Category -> ChartOfAccount (multiple FKs)
        builder.Entity<Category>()
            .HasOne(c => c.SaleAccount)
            .WithMany()
            .HasForeignKey(c => c.SaleAccount_ID);

        builder.Entity<Category>()
            .HasOne(c => c.StockAccount)
            .WithMany()
            .HasForeignKey(c => c.StockAccount_ID);

        builder.Entity<Category>()
            .HasOne(c => c.COGSAccount)
            .WithMany()
            .HasForeignKey(c => c.COGSAccount_ID);

        builder.Entity<Category>()
            .HasOne(c => c.DamageExpenseAccount)
            .WithMany()
            .HasForeignKey(c => c.DamageExpenseAccount_ID);

        // StoreInventories
        builder.Entity<StoreInventory>()
            .HasOne(si => si.Store)
            .WithMany()
            .HasForeignKey(si => si.Store_ID);

        builder.Entity<StoreInventory>()
            .HasOne(si => si.ProductBatch)
            .WithMany(pb => pb.StoreInventories)
            .HasForeignKey(si => si.ProductBatch_ID);

        // StockMovements
        builder.Entity<StockMovement>()
            .HasOne(sm => sm.Store)
            .WithMany()
            .HasForeignKey(sm => sm.Store_ID);

        builder.Entity<StockMovement>()
            .HasOne(sm => sm.ProductBatch)
            .WithMany()
            .HasForeignKey(sm => sm.ProductBatch_ID);

        // ProductBatch - Configure decimal precision
        builder.Entity<ProductBatch>()
            .Property(pb => pb.CostPrice)
            .HasColumnType("decimal(18,2)");

        builder.Entity<ProductBatch>()
            .Property(pb => pb.MRP)
            .HasColumnType("decimal(18,2)");

        builder.Entity<ProductBatch>()
            .Property(pb => pb.SellingPrice)
            .HasColumnType("decimal(18,2)");

        // StockAlert - Configure FK relationships
        builder.Entity<StockAlert>()
            .HasOne(sa => sa.Product)
            .WithMany()
            .HasForeignKey(sa => sa.Product_ID);

        builder.Entity<StockAlert>()
            .HasOne(sa => sa.Store)
            .WithMany()
            .HasForeignKey(sa => sa.Store_ID);

        // PurchaseOrder - Decimal Precision
        builder.Entity<PurchaseOrder>()
            .Property(po => po.TotalAmount)
            .HasColumnType("decimal(18,2)");

        // ExpenseCategory (Self-referencing)
        builder.Entity<ExpenseCategory>()
            .HasOne(ec => ec.ParentCategory)
            .WithMany(ec => ec.ChildCategories)
            .HasForeignKey(ec => ec.ParentCategory_ID);

        // Expense
        builder.Entity<Expense>()
            .HasOne(e => e.ExpenseCategory)
            .WithMany(ec => ec.Expenses)
            .HasForeignKey(e => e.ExpenseCategory_ID);

        builder.Entity<Expense>()
            .HasOne(e => e.SourceAccount)
            .WithMany()
            .HasForeignKey(e => e.SourceAccount_ID)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Expense>()
            .HasOne(e => e.ExpenseAccount)
            .WithMany()
            .HasForeignKey(e => e.ExpenseAccount_ID)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Expense>()
            .Property(e => e.Amount)
            .HasColumnType("decimal(18,2)");

        // CustomerPayment - Configure FK relationships explicitly
        builder.Entity<PharmaCare.Domain.Models.Finance.CustomerPayment>()
            .HasOne(cp => cp.Party)
            .WithMany()
            .HasForeignKey(cp => cp.Party_ID);

        builder.Entity<PharmaCare.Domain.Models.Finance.CustomerPayment>()
            .HasOne(cp => cp.StockMain)
            .WithMany()
            .HasForeignKey(cp => cp.StockMain_ID);



        builder.Entity<PharmaCare.Domain.Models.Finance.CustomerPayment>()
            .Property(cp => cp.Amount)
            .HasColumnType("decimal(18,2)");

        // ========== ACCOUNTING MODULE CONFIGURATIONS ==========

        // AccountType
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.AccountType>()
            .HasKey(at => at.AccountTypeID);

        // ChartOfAccount
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.ChartOfAccount>()
            .HasKey(coa => coa.AccountID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.ChartOfAccount>()
            .HasOne(coa => coa.Head)
            .WithMany(h => h.ChartOfAccounts)
            .HasForeignKey(coa => coa.Head_ID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.ChartOfAccount>()
            .HasOne(coa => coa.Subhead)
            .WithMany(s => s.ChartOfAccounts)
            .HasForeignKey(coa => coa.Subhead_ID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.ChartOfAccount>()
            .HasOne(coa => coa.AccountType)
            .WithMany(at => at.ChartOfAccounts)
            .HasForeignKey(coa => coa.AccountType_ID);

        // Head
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.Head>()
            .HasKey(h => h.HeadID);

        // Subhead
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.Subhead>()
            .HasKey(s => s.SubheadID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.Subhead>()
            .HasOne(s => s.Head)
            .WithMany(h => h.Subheads)
            .HasForeignKey(s => s.Head_ID);

        // Party
        builder.Entity<Party>()
            .HasKey(p => p.PartyID);

        // AccountMapping
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.AccountMapping>()
            .HasKey(am => am.AccountMappingID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.AccountMapping>()
            .HasIndex(am => am.PartyType)
            .IsUnique();

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.AccountMapping>()
            .HasOne(am => am.Head)
            .WithMany()
            .HasForeignKey(am => am.Head_ID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.AccountMapping>()
            .HasOne(am => am.Subhead)
            .WithMany()
            .HasForeignKey(am => am.Subhead_ID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.AccountMapping>()
            .HasOne(am => am.Account)
            .WithMany()
            .HasForeignKey(am => am.Account_ID);



        // ========== PURCHASE RETURN - JOURNAL ENTRY LINKS ==========


        // ========== STOCK MOVEMENT - JOURNAL ENTRY LINK ==========


        builder.Entity<StockMovement>()
            .HasOne(sm => sm.RelatedMovement)
            .WithOne()
            .HasForeignKey<StockMovement>(sm => sm.RelatedMovement_ID)
            .OnDelete(DeleteBehavior.NoAction);

        // ========== FISCAL YEAR ==========
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalYear>()
            .HasKey(fy => fy.FiscalYearID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalYear>()
            .HasIndex(fy => fy.YearCode)
            .IsUnique();

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalYear>()
            .Property(fy => fy.Status)
            .HasMaxLength(20);

        // ========== FISCAL PERIOD ==========
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalPeriod>()
            .HasKey(fp => fp.FiscalPeriodID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalPeriod>()
            .HasOne(fp => fp.FiscalYear)
            .WithMany(fy => fy.FiscalPeriods)
            .HasForeignKey(fp => fp.FiscalYear_ID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalPeriod>()
            .HasIndex(fp => fp.PeriodCode)
            .IsUnique();

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalPeriod>()
            .HasIndex(fp => new { fp.StartDate, fp.EndDate });

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.FiscalPeriod>()
            .Property(fp => fp.Status)
            .HasMaxLength(20);

        // ========== STORE FISCAL PERIOD ==========
        builder.Entity<PharmaCare.Domain.Models.AccountManagement.StoreFiscalPeriod>()
            .HasKey(sfp => sfp.StoreFiscalPeriodID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.StoreFiscalPeriod>()
            .HasOne(sfp => sfp.Store)
            .WithMany()
            .HasForeignKey(sfp => sfp.Store_ID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.StoreFiscalPeriod>()
            .HasOne(sfp => sfp.FiscalPeriod)
            .WithMany(fp => fp.StoreFiscalPeriods)
            .HasForeignKey(sfp => sfp.FiscalPeriod_ID);

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.StoreFiscalPeriod>()
            .HasIndex(sfp => new { sfp.Store_ID, sfp.FiscalPeriod_ID })
            .IsUnique();

        builder.Entity<PharmaCare.Domain.Models.AccountManagement.StoreFiscalPeriod>()
            .Property(sfp => sfp.Status)
            .HasMaxLength(20);


        // ========== SUPPLIER PAYMENT - DISABLE OUTPUT CLAUSE FOR TRIGGERS ==========
        builder.Entity<PharmaCare.Domain.Models.Finance.SupplierPayment>()
            .ToTable(t => t.UseSqlOutputClause(false));





        // ========== UNIFIED TRANSACTION TABLES (GreenEnergy Pattern) ==========

        // InvoiceType
        builder.Entity<InvoiceType>()
            .HasKey(it => it.TypeID);

        builder.Entity<InvoiceType>()
            .HasIndex(it => it.Code)
            .IsUnique();

        // AccountVoucherType
        builder.Entity<AccountVoucherType>()
            .HasKey(avt => avt.VoucherTypeID);

        builder.Entity<AccountVoucherType>()
            .HasIndex(avt => avt.Code)
            .IsUnique();

        // StockMain
        builder.Entity<StockMain>()
            .HasKey(sm => sm.StockMainID);

        builder.Entity<StockMain>()
            .HasIndex(sm => sm.InvoiceNo)
            .IsUnique();

        builder.Entity<StockMain>()
            .HasOne(sm => sm.InvoiceType)
            .WithMany()
            .HasForeignKey(sm => sm.InvoiceType_ID);

        builder.Entity<StockMain>()
            .HasOne(sm => sm.Store)
            .WithMany()
            .HasForeignKey(sm => sm.Store_ID);

        builder.Entity<StockMain>()
            .HasOne(sm => sm.Party)
            .WithMany()
            .HasForeignKey(sm => sm.Party_ID);

        builder.Entity<StockMain>()
            .HasOne(sm => sm.PaymentVoucher)
            .WithMany()
            .HasForeignKey(sm => sm.PaymentVoucher_ID)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StockMain>()
            .HasOne(sm => sm.Account)
            .WithMany()
            .HasForeignKey(sm => sm.Account_ID);

        builder.Entity<StockMain>()
            .HasOne(sm => sm.ReferenceStockMain)
            .WithMany()
            .HasForeignKey(sm => sm.ReferenceStockMain_ID)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StockMain>()
            .HasOne(sm => sm.RefundVoucher)
            .WithMany()
            .HasForeignKey(sm => sm.RefundVoucher_ID)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StockMain>()
            .HasOne(sm => sm.DestinationStore)
            .WithMany()
            .HasForeignKey(sm => sm.DestinationStore_ID)
            .OnDelete(DeleteBehavior.NoAction);

        // StockMain global filter
        builder.Entity<StockMain>().HasQueryFilter(x =>
            _storeContext.IsAdmin ||
            (x.Store_ID == _storeContext.CurrentStoreId)
        );

        // StockDetail
        builder.Entity<StockDetail>()
            .HasKey(sd => sd.StockDetailID);

        builder.Entity<StockDetail>()
            .HasOne(sd => sd.StockMain)
            .WithMany(sm => sm.StockDetails)
            .HasForeignKey(sd => sd.StockMain_ID);

        builder.Entity<StockDetail>()
            .HasOne(sd => sd.Product)
            .WithMany()
            .HasForeignKey(sd => sd.Product_ID);

        builder.Entity<StockDetail>()
            .HasOne(sd => sd.ProductBatch)
            .WithMany()
            .HasForeignKey(sd => sd.ProductBatch_ID);

        // AccountVoucher
        builder.Entity<AccountVoucher>()
            .HasKey(av => av.VoucherID);

        builder.Entity<AccountVoucher>()
            .HasIndex(av => av.VoucherCode)
            .IsUnique();

        builder.Entity<AccountVoucher>()
            .ToTable(t => t.UseSqlOutputClause(false));

        builder.Entity<AccountVoucher>()
            .HasOne(av => av.VoucherType)
            .WithMany()
            .HasForeignKey(av => av.VoucherType_ID);

        builder.Entity<AccountVoucher>()
            .HasOne(av => av.Store)
            .WithMany()
            .HasForeignKey(av => av.Store_ID);

        builder.Entity<AccountVoucher>()
            .HasOne(av => av.FiscalPeriod)
            .WithMany()
            .HasForeignKey(av => av.FiscalPeriod_ID);

        builder.Entity<AccountVoucher>()
            .HasOne(av => av.ReversedByVoucher)
            .WithOne()
            .HasForeignKey<AccountVoucher>(av => av.ReversedBy_ID)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<AccountVoucher>()
            .HasOne(av => av.ReversesVoucher)
            .WithOne()
            .HasForeignKey<AccountVoucher>(av => av.Reverses_ID)
            .OnDelete(DeleteBehavior.NoAction);

        // AccountVoucherDetail
        builder.Entity<AccountVoucherDetail>()
            .HasKey(avd => avd.VoucherDetailID);

        builder.Entity<AccountVoucherDetail>()
            .ToTable(t => t.UseSqlOutputClause(false));

        builder.Entity<AccountVoucherDetail>()
            .HasOne(avd => avd.Voucher)
            .WithMany(av => av.VoucherDetails)
            .HasForeignKey(avd => avd.Voucher_ID);

        builder.Entity<AccountVoucherDetail>()
            .HasOne(avd => avd.Account)
            .WithMany()
            .HasForeignKey(avd => avd.Account_ID);

        builder.Entity<AccountVoucherDetail>()
            .HasOne(avd => avd.Product)
            .WithMany()
            .HasForeignKey(avd => avd.Product_ID);

        builder.Entity<AccountVoucherDetail>()
            .HasOne(avd => avd.Store)
            .WithMany()
            .HasForeignKey(avd => avd.Store_ID);


        // --- GLOBAL: CONFIGURE DATABASE SET NULL ---
        // Only apply SET NULL to relationships where the FK column is nullable
        // AND the relationship is not self-referencing (which causes SQL Server cycle errors)
        foreach (var relationship in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            // Check if this is a self-referencing FK (same table references itself)
            var isSelfReferencing = relationship.PrincipalEntityType == relationship.DeclaringEntityType;

            // Check if all FK properties are nullable - only then can we use SetNull
            var allPropertiesNullable = relationship.Properties.All(p => p.IsNullable);

            if (isSelfReferencing)
            {
                // Self-referencing FKs cannot use SET NULL in SQL Server due to cycle detection
                relationship.DeleteBehavior = DeleteBehavior.NoAction;
            }
            else if (allPropertiesNullable)
            {
                relationship.DeleteBehavior = DeleteBehavior.SetNull;
            }
            else
            {
                // For non-nullable FKs (like Identity tables), use Restrict to prevent cascade issues
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
