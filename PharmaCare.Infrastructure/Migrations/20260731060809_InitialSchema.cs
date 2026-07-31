using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    AccountTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.AccountTypeID);
                });

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    PageID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Parent_ID = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Controller = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.PageID);
                    table.ForeignKey(
                        name: "FK_Pages_Pages_Parent_ID",
                        column: x => x.Parent_ID,
                        principalTable: "Pages",
                        principalColumn: "PageID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pharmacies",
                columns: table => new
                {
                    PharmacyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pharmacies", x => x.PharmacyID);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTypes",
                columns: table => new
                {
                    TransactionTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StockDirection = table.Column<int>(type: "int", nullable: false),
                    AffectsStock = table.Column<bool>(type: "bit", nullable: false),
                    CreatesVoucher = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTypes", x => x.TransactionTypeID);
                });

            migrationBuilder.CreateTable(
                name: "VoucherTypes",
                columns: table => new
                {
                    VoucherTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsAutoGenerated = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherTypes", x => x.VoucherTypeID);
                });

            migrationBuilder.CreateTable(
                name: "PageUrls",
                columns: table => new
                {
                    PageUrlID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Page_ID = table.Column<int>(type: "int", nullable: false),
                    Controller = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageUrls", x => x.PageUrlID);
                    table.ForeignKey(
                        name: "FK_PageUrls_Pages_Page_ID",
                        column: x => x.Page_ID,
                        principalTable: "Pages",
                        principalColumn: "PageID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountFamilies",
                columns: table => new
                {
                    AccountFamilyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    FamilyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountFamilies", x => x.AccountFamilyID);
                    table.ForeignKey(
                        name: "FK_AccountFamilies_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialPeriods",
                columns: table => new
                {
                    PeriodID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<int>(type: "int", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialPeriods", x => x.PeriodID);
                    table.ForeignKey(
                        name: "FK_FinancialPeriods_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceTypes",
                columns: table => new
                {
                    PriceTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    PriceTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTypes", x => x.PriceTypeID);
                    table.ForeignKey(
                        name: "FK_PriceTypes_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfitSettings",
                columns: table => new
                {
                    SettingsID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    RetailProfitPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    WholesaleProfitPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PriceRoundingStep = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfitSettings", x => x.SettingsID);
                    table.ForeignKey(
                        name: "FK_ProfitSettings_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleID);
                    table.ForeignKey(
                        name: "FK_Roles_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: true),
                    IsPlatformAdmin = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    VoucherID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    VoucherType_ID = table.Column<int>(type: "int", nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VoucherDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceTable = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceID = table.Column<int>(type: "int", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedByVoucher_ID = table.Column<int>(type: "int", nullable: true),
                    ReversesVoucher_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.VoucherID);
                    table.ForeignKey(
                        name: "FK_Vouchers_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_VoucherTypes_VoucherType_ID",
                        column: x => x.VoucherType_ID,
                        principalTable: "VoucherTypes",
                        principalColumn: "VoucherTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Vouchers_ReversedByVoucher_ID",
                        column: x => x.ReversedByVoucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Vouchers_ReversesVoucher_ID",
                        column: x => x.ReversesVoucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountHeads",
                columns: table => new
                {
                    AccountHeadID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    HeadName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountFamily_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountHeads", x => x.AccountHeadID);
                    table.ForeignKey(
                        name: "FK_AccountHeads_AccountFamilies_AccountFamily_ID",
                        column: x => x.AccountFamily_ID,
                        principalTable: "AccountFamilies",
                        principalColumn: "AccountFamilyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountHeads_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePages",
                columns: table => new
                {
                    RolePageID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Role_ID = table.Column<int>(type: "int", nullable: false),
                    Page_ID = table.Column<int>(type: "int", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanCreate = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePages", x => x.RolePageID);
                    table.ForeignKey(
                        name: "FK_RolePages_Pages_Page_ID",
                        column: x => x.Page_ID,
                        principalTable: "Pages",
                        principalColumn: "PageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePages_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePages_Roles_Role_ID",
                        column: x => x.Role_ID,
                        principalTable: "Roles",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdentityUserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_IdentityUserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_IdentityUserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserRoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    User_ID = table.Column<int>(type: "int", nullable: false),
                    Role_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.UserRoleID);
                    table.ForeignKey(
                        name: "FK_UserRoles_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_Role_ID",
                        column: x => x.Role_ID,
                        principalTable: "Roles",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_User_ID",
                        column: x => x.User_ID,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountSubheads",
                columns: table => new
                {
                    AccountSubheadID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    SubheadName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountHead_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSubheads", x => x.AccountSubheadID);
                    table.ForeignKey(
                        name: "FK_AccountSubheads_AccountHeads_AccountHead_ID",
                        column: x => x.AccountHead_ID,
                        principalTable: "AccountHeads",
                        principalColumn: "AccountHeadID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSubheads_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountHead_ID = table.Column<int>(type: "int", nullable: true),
                    AccountSubhead_ID = table.Column<int>(type: "int", nullable: false),
                    AccountType_ID = table.Column<int>(type: "int", nullable: false),
                    IsSystemAccount = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountID);
                    table.ForeignKey(
                        name: "FK_Accounts_AccountHeads_AccountHead_ID",
                        column: x => x.AccountHead_ID,
                        principalTable: "AccountHeads",
                        principalColumn: "AccountHeadID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accounts_AccountSubheads_AccountSubhead_ID",
                        column: x => x.AccountSubhead_ID,
                        principalTable: "AccountSubheads",
                        principalColumn: "AccountSubheadID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accounts_AccountTypes_AccountType_ID",
                        column: x => x.AccountType_ID,
                        principalTable: "AccountTypes",
                        principalColumn: "AccountTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accounts_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SaleAccount_ID = table.Column<int>(type: "int", nullable: false),
                    StockAccount_ID = table.Column<int>(type: "int", nullable: false),
                    COGSAccount_ID = table.Column<int>(type: "int", nullable: false),
                    DamageAccount_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryID);
                    table.ForeignKey(
                        name: "FK_Categories_Accounts_COGSAccount_ID",
                        column: x => x.COGSAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Categories_Accounts_DamageAccount_ID",
                        column: x => x.DamageAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Categories_Accounts_SaleAccount_ID",
                        column: x => x.SaleAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Categories_Accounts_StockAccount_ID",
                        column: x => x.StockAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Categories_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    ExpenseCategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Parent_ID = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultExpenseAccount_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.ExpenseCategoryID);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_Accounts_DefaultExpenseAccount_ID",
                        column: x => x.DefaultExpenseAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_ExpenseCategories_Parent_ID",
                        column: x => x.Parent_ID,
                        principalTable: "ExpenseCategories",
                        principalColumn: "ExpenseCategoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    PartyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsWholeSale = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IBAN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Account_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.PartyID);
                    table.ForeignKey(
                        name: "FK_Parties_Accounts_Account_ID",
                        column: x => x.Account_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID");
                    table.ForeignKey(
                        name: "FK_Parties_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    SubCategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Category_ID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.SubCategoryID);
                    table.ForeignKey(
                        name: "FK_SubCategories_Categories_Category_ID",
                        column: x => x.Category_ID,
                        principalTable: "Categories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubCategories_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseBudgets",
                columns: table => new
                {
                    ExpenseBudgetID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    ExpenseCategory_ID = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseBudgets", x => x.ExpenseBudgetID);
                    table.ForeignKey(
                        name: "FK_ExpenseBudgets_ExpenseCategories_ExpenseCategory_ID",
                        column: x => x.ExpenseCategory_ID,
                        principalTable: "ExpenseCategories",
                        principalColumn: "ExpenseCategoryID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpenseBudgets_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    ExpenseID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    ExpenseCategory_ID = table.Column<int>(type: "int", nullable: false),
                    SourceAccount_ID = table.Column<int>(type: "int", nullable: false),
                    ExpenseAccount_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Voucher_ID = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy_ID = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.ExpenseID);
                    table.ForeignKey(
                        name: "FK_Expenses_Accounts_ExpenseAccount_ID",
                        column: x => x.ExpenseAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Accounts_SourceAccount_ID",
                        column: x => x.SourceAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_ExpenseCategory_ID",
                        column: x => x.ExpenseCategory_ID,
                        principalTable: "ExpenseCategories",
                        principalColumn: "ExpenseCategoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Vouchers_Voucher_ID",
                        column: x => x.Voucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StockMains",
                columns: table => new
                {
                    StockMainID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    TransactionType_ID = table.Column<int>(type: "int", nullable: false),
                    TransactionNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Party_ID = table.Column<int>(type: "int", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Voucher_ID = table.Column<int>(type: "int", nullable: true),
                    ReferenceStockMain_ID = table.Column<int>(type: "int", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdjustmentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedBy = table.Column<int>(type: "int", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMains", x => x.StockMainID);
                    table.CheckConstraint("CK_StockMains_PaymentStatus_Valid", "[PaymentStatus] IN ('Unpaid','Partial','Paid')");
                    table.CheckConstraint("CK_StockMains_Status_Valid", "[Status] IN ('Draft','Approved','Completed','Void')");
                    table.ForeignKey(
                        name: "FK_StockMains_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockMains_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMains_StockMains_ReferenceStockMain_ID",
                        column: x => x.ReferenceStockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMains_TransactionTypes_TransactionType_ID",
                        column: x => x.TransactionType_ID,
                        principalTable: "TransactionTypes",
                        principalColumn: "TransactionTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMains_Vouchers_Voucher_ID",
                        column: x => x.Voucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Category_ID = table.Column<int>(type: "int", nullable: true),
                    SubCategory_ID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OpeningPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpeningQuantity = table.Column<int>(type: "int", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false),
                    UnitsInPack = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductID);
                    table.CheckConstraint("CK_Products_UnitsInPack_Positive", "[UnitsInPack] > 0");
                    table.ForeignKey(
                        name: "FK_Products_Categories_Category_ID",
                        column: x => x.Category_ID,
                        principalTable: "Categories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_SubCategories_SubCategory_ID",
                        column: x => x.SubCategory_ID,
                        principalTable: "SubCategories",
                        principalColumn: "SubCategoryID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                columns: table => new
                {
                    CreditNoteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    CreditNoteNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Party_ID = table.Column<int>(type: "int", nullable: false),
                    SourceStockMain_ID = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedBy = table.Column<int>(type: "int", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Voucher_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNotes", x => x.CreditNoteID);
                    table.CheckConstraint("CK_CreditNotes_Status_Valid", "[Status] IN ('Open','Applied','Void')");
                    table.ForeignKey(
                        name: "FK_CreditNotes_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_StockMains_SourceStockMain_ID",
                        column: x => x.SourceStockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Vouchers_Voucher_ID",
                        column: x => x.Voucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Party_ID = table.Column<int>(type: "int", nullable: false),
                    StockMain_ID = table.Column<int>(type: "int", nullable: true),
                    Account_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChequeNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChequeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedBy = table.Column<int>(type: "int", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Voucher_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentID);
                    table.CheckConstraint("CK_Payments_PaymentType_Valid", "[PaymentType] IN ('RECEIPT','PAYMENT','REFUND','ADJUSTMENT')");
                    table.ForeignKey(
                        name: "FK_Payments_Accounts_Account_ID",
                        column: x => x.Account_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_StockMains_StockMain_ID",
                        column: x => x.StockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Vouchers_Voucher_ID",
                        column: x => x.Voucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCreditNotes",
                columns: table => new
                {
                    SupplierCreditNoteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    CreditNoteNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Party_ID = table.Column<int>(type: "int", nullable: false),
                    SourceStockMain_ID = table.Column<int>(type: "int", nullable: true),
                    AdjustmentAccount_ID = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedBy = table.Column<int>(type: "int", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Voucher_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditNotes", x => x.SupplierCreditNoteID);
                    table.CheckConstraint("CK_SupplierCreditNotes_Status_Valid", "[Status] IN ('Open','Applied','Void')");
                    table.ForeignKey(
                        name: "FK_SupplierCreditNotes_Accounts_AdjustmentAccount_ID",
                        column: x => x.AdjustmentAccount_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID");
                    table.ForeignKey(
                        name: "FK_SupplierCreditNotes_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCreditNotes_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCreditNotes_StockMains_SourceStockMain_ID",
                        column: x => x.SourceStockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierCreditNotes_Vouchers_Voucher_ID",
                        column: x => x.Voucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductPriceHistories",
                columns: table => new
                {
                    ProductPriceHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Product_ID = table.Column<int>(type: "int", nullable: false),
                    PriceType_ID = table.Column<int>(type: "int", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPriceAtChange = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangedBy = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceHistories", x => x.ProductPriceHistoryID);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_PriceTypes_PriceType_ID",
                        column: x => x.PriceType_ID,
                        principalTable: "PriceTypes",
                        principalColumn: "PriceTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                columns: table => new
                {
                    ProductPriceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Product_ID = table.Column<int>(type: "int", nullable: false),
                    PriceType_ID = table.Column<int>(type: "int", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => x.ProductPriceID);
                    table.ForeignKey(
                        name: "FK_ProductPrices_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPrices_PriceTypes_PriceType_ID",
                        column: x => x.PriceType_ID,
                        principalTable: "PriceTypes",
                        principalColumn: "PriceTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPrices_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockDetails",
                columns: table => new
                {
                    StockDetailID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    StockMain_ID = table.Column<int>(type: "int", nullable: false),
                    Product_ID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDetails", x => x.StockDetailID);
                    table.ForeignKey(
                        name: "FK_StockDetails_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDetails_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDetails_StockMains_StockMain_ID",
                        column: x => x.StockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VoucherDetails",
                columns: table => new
                {
                    VoucherDetailID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Voucher_ID = table.Column<int>(type: "int", nullable: false),
                    Account_ID = table.Column<int>(type: "int", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Party_ID = table.Column<int>(type: "int", nullable: true),
                    Product_ID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherDetails", x => x.VoucherDetailID);
                    table.ForeignKey(
                        name: "FK_VoucherDetails_Accounts_Account_ID",
                        column: x => x.Account_ID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherDetails_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VoucherDetails_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherDetails_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VoucherDetails_Vouchers_Voucher_ID",
                        column: x => x.Voucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    PaymentAllocationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Payment_ID = table.Column<int>(type: "int", nullable: true),
                    CreditNote_ID = table.Column<int>(type: "int", nullable: true),
                    StockMain_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.PaymentAllocationID);
                    table.CheckConstraint("CK_PaymentAllocations_Source_NotNull", "[Payment_ID] IS NOT NULL OR [CreditNote_ID] IS NOT NULL");
                    table.CheckConstraint("CK_PaymentAllocations_Source_Valid", "[SourceType] IN ('Receipt','CreditNote')");
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_CreditNotes_CreditNote_ID",
                        column: x => x.CreditNote_ID,
                        principalTable: "CreditNotes",
                        principalColumn: "CreditNoteID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_Payment_ID",
                        column: x => x.Payment_ID,
                        principalTable: "Payments",
                        principalColumn: "PaymentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_StockMains_StockMain_ID",
                        column: x => x.StockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountFamilies_Pharmacy_ID",
                table: "AccountFamilies",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_AccountFamily_ID",
                table: "AccountHeads",
                column: "AccountFamily_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_Pharmacy_ID",
                table: "AccountHeads",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_Pharmacy_ID_Code",
                table: "AccountHeads",
                columns: new[] { "Pharmacy_ID", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountHead_ID",
                table: "Accounts",
                column: "AccountHead_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountSubhead_ID",
                table: "Accounts",
                column: "AccountSubhead_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountType_ID",
                table: "Accounts",
                column: "AccountType_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Pharmacy_ID",
                table: "Accounts",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_AccountHead_ID",
                table: "AccountSubheads",
                column: "AccountHead_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_Pharmacy_ID",
                table: "AccountSubheads",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_Pharmacy_ID_Code",
                table: "AccountSubheads",
                columns: new[] { "Pharmacy_ID", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_Code",
                table: "AccountTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_COGSAccount_ID",
                table: "Categories",
                column: "COGSAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DamageAccount_ID",
                table: "Categories",
                column: "DamageAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Pharmacy_ID",
                table: "Categories",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_SaleAccount_ID",
                table: "Categories",
                column: "SaleAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_StockAccount_ID",
                table: "Categories",
                column: "StockAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Party_ID",
                table: "CreditNotes",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Pharmacy_ID",
                table: "CreditNotes",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Pharmacy_ID_CreditNoteNo",
                table: "CreditNotes",
                columns: new[] { "Pharmacy_ID", "CreditNoteNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_SourceStockMain_ID",
                table: "CreditNotes",
                column: "SourceStockMain_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Voucher_ID",
                table: "CreditNotes",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseBudgets_ExpenseCategory_ID",
                table: "ExpenseBudgets",
                column: "ExpenseCategory_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseBudgets_Pharmacy_ID",
                table: "ExpenseBudgets",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseBudgets_Pharmacy_ID_ExpenseCategory_ID_Year_Month",
                table: "ExpenseBudgets",
                columns: new[] { "Pharmacy_ID", "ExpenseCategory_ID", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_DefaultExpenseAccount_ID",
                table: "ExpenseCategories",
                column: "DefaultExpenseAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Parent_ID",
                table: "ExpenseCategories",
                column: "Parent_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Pharmacy_ID",
                table: "ExpenseCategories",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseAccount_ID",
                table: "Expenses",
                column: "ExpenseAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseCategory_ID",
                table: "Expenses",
                column: "ExpenseCategory_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Pharmacy_ID",
                table: "Expenses",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_SourceAccount_ID",
                table: "Expenses",
                column: "SourceAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Voucher_ID",
                table: "Expenses",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPeriods_Pharmacy_ID",
                table: "FinancialPeriods",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityUserClaims_UserId",
                table: "IdentityUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityUserLogins_UserId",
                table: "IdentityUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_Parent_ID",
                table: "Pages",
                column: "Parent_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PageUrls_Controller_Action",
                table: "PageUrls",
                columns: new[] { "Controller", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_PageUrls_Page_ID",
                table: "PageUrls",
                column: "Page_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Account_ID",
                table: "Parties",
                column: "Account_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Pharmacy_ID",
                table: "Parties",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CreditNote_ID",
                table: "PaymentAllocations",
                column: "CreditNote_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_Payment_ID",
                table: "PaymentAllocations",
                column: "Payment_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_Pharmacy_ID",
                table: "PaymentAllocations",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_StockMain_ID",
                table: "PaymentAllocations",
                column: "StockMain_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Account_ID",
                table: "Payments",
                column: "Account_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Party_ID",
                table: "Payments",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Pharmacy_ID",
                table: "Payments",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StockMain_ID",
                table: "Payments",
                column: "StockMain_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Voucher_ID",
                table: "Payments",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Pharmacies_Code",
                table: "Pharmacies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceTypes_Pharmacy_ID",
                table: "PriceTypes",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_Pharmacy_ID",
                table: "ProductPriceHistories",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_PriceType_ID",
                table: "ProductPriceHistories",
                column: "PriceType_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_Product_ID_PriceType_ID_EffectiveTo",
                table: "ProductPriceHistories",
                columns: new[] { "Product_ID", "PriceType_ID", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "UX_ProductPriceHistories_OpenRow",
                table: "ProductPriceHistories",
                columns: new[] { "Product_ID", "PriceType_ID" },
                unique: true,
                filter: "[EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_Pharmacy_ID",
                table: "ProductPrices",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_PriceType_ID",
                table: "ProductPrices",
                column: "PriceType_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_Product_ID",
                table: "ProductPrices",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_ID",
                table: "Products",
                column: "Category_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Pharmacy_ID",
                table: "Products",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ShortCode",
                table: "Products",
                column: "ShortCode");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SubCategory_ID",
                table: "Products",
                column: "SubCategory_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProfitSettings_Pharmacy_ID",
                table: "ProfitSettings",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RolePages_Page_ID",
                table: "RolePages",
                column: "Page_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RolePages_Pharmacy_ID",
                table: "RolePages",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RolePages_Role_ID_Page_ID",
                table: "RolePages",
                columns: new[] { "Role_ID", "Page_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Pharmacy_ID",
                table: "Roles",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Pharmacy_ID_Name",
                table: "Roles",
                columns: new[] { "Pharmacy_ID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Pharmacy_ID",
                table: "StockDetails",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Product_ID",
                table: "StockDetails",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Product_StockMain",
                table: "StockDetails",
                columns: new[] { "Pharmacy_ID", "Product_ID", "StockMain_ID" })
                .Annotation("SqlServer:Include", new[] { "CostPrice", "Quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_StockMain_ID",
                table: "StockDetails",
                column: "StockMain_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Party_ID",
                table: "StockMains",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Pharmacy_ID",
                table: "StockMains",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Pharmacy_ID_TransactionNo",
                table: "StockMains",
                columns: new[] { "Pharmacy_ID", "TransactionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_ReferenceStockMain_ID",
                table: "StockMains",
                column: "ReferenceStockMain_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_TransactionType_ID",
                table: "StockMains",
                column: "TransactionType_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Type_Date_Status",
                table: "StockMains",
                columns: new[] { "Pharmacy_ID", "TransactionType_ID", "TransactionDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Voucher_ID",
                table: "StockMains",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_Category_ID",
                table: "SubCategories",
                column: "Category_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_Pharmacy_ID",
                table: "SubCategories",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_AdjustmentAccount_ID",
                table: "SupplierCreditNotes",
                column: "AdjustmentAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_Party_ID",
                table: "SupplierCreditNotes",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_Pharmacy_ID",
                table: "SupplierCreditNotes",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_Pharmacy_ID_CreditNoteNo",
                table: "SupplierCreditNotes",
                columns: new[] { "Pharmacy_ID", "CreditNoteNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_SourceStockMain_ID",
                table: "SupplierCreditNotes",
                column: "SourceStockMain_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_Voucher_ID",
                table: "SupplierCreditNotes",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_Code",
                table: "TransactionTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_Pharmacy_ID",
                table: "UserRoles",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_Role_ID",
                table: "UserRoles",
                column: "Role_ID");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_User_ID_Role_ID",
                table: "UserRoles",
                columns: new[] { "User_ID", "Role_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Pharmacy_ID",
                table: "Users",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherDetails_Account_ID",
                table: "VoucherDetails",
                column: "Account_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherDetails_Party_ID",
                table: "VoucherDetails",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherDetails_Pharmacy_ID",
                table: "VoucherDetails",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherDetails_Product_ID",
                table: "VoucherDetails",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherDetails_Voucher_ID",
                table: "VoucherDetails",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Pharmacy_ID",
                table: "Vouchers",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Pharmacy_ID_VoucherNo",
                table: "Vouchers",
                columns: new[] { "Pharmacy_ID", "VoucherNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ReversedByVoucher_ID",
                table: "Vouchers",
                column: "ReversedByVoucher_ID",
                unique: true,
                filter: "[ReversedByVoucher_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ReversesVoucher_ID",
                table: "Vouchers",
                column: "ReversesVoucher_ID",
                unique: true,
                filter: "[ReversesVoucher_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_VoucherType_ID",
                table: "Vouchers",
                column: "VoucherType_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherTypes_Code",
                table: "VoucherTypes",
                column: "Code",
                unique: true);

            // Carried over from the pre-squash migration chain: DEFAULT constraints left behind by
            // AddColumn(..., defaultValue: ...) calls. They are not in the EF model, but every
            // database migrated under the old chain has them, so they are recreated here to keep
            // fresh databases schema-identical to existing ones. Guarded per column.
            migrationBuilder.Sql(@"
                DECLARE @defaults TABLE (Tbl sysname, Col sysname, Val nvarchar(20));
                INSERT INTO @defaults VALUES
                    ('AccountFamilies','Pharmacy_ID','1'), ('AccountHeads','Pharmacy_ID','1'),
                    ('Accounts','IsActive','1'), ('Accounts','IsSystemAccount','0'), ('Accounts','Pharmacy_ID','1'),
                    ('AccountSubheads','Pharmacy_ID','1'),
                    ('Categories','COGSAccount_ID','0'), ('Categories','DamageAccount_ID','0'), ('Categories','Pharmacy_ID','1'),
                    ('Categories','SaleAccount_ID','0'), ('Categories','StockAccount_ID','0'),
                    ('CreditNotes','Pharmacy_ID','1'), ('ExpenseBudgets','Pharmacy_ID','1'), ('ExpenseCategories','Pharmacy_ID','1'),
                    ('Expenses','Pharmacy_ID','1'), ('Expenses','Status','0'), ('FinancialPeriods','Pharmacy_ID','1'),
                    ('Parties','IsWholeSale','0'), ('Parties','Pharmacy_ID','1'), ('PaymentAllocations','Pharmacy_ID','1'),
                    ('Payments','Pharmacy_ID','1'), ('PriceTypes','Pharmacy_ID','1'), ('ProductPrices','Pharmacy_ID','1'),
                    ('Products','OpeningQuantity','0'), ('Products','Pharmacy_ID','1'),
                    ('ProfitSettings','Pharmacy_ID','1'), ('ProfitSettings','PriceRoundingStep','0.0'),
                    ('RolePages','Pharmacy_ID','1'), ('Roles','Pharmacy_ID','1'), ('StockDetails','Pharmacy_ID','1'),
                    ('StockMains','Pharmacy_ID','1'), ('SubCategories','Pharmacy_ID','1'),
                    ('SupplierCreditNotes','Pharmacy_ID','1'), ('UserRoles','Pharmacy_ID','1'),
                    ('Users','IsPlatformAdmin','0'), ('VoucherDetails','Pharmacy_ID','1'), ('Vouchers','Pharmacy_ID','1');

                DECLARE @sql nvarchar(max) = N'';
                SELECT @sql = @sql +
                    N'ALTER TABLE ' + QUOTENAME(d.Tbl) + N' ADD CONSTRAINT ' +
                    QUOTENAME(N'DF_' + d.Tbl + N'_' + d.Col) + N' DEFAULT (' + d.Val + N') FOR ' + QUOTENAME(d.Col) + N';'
                FROM @defaults d
                WHERE NOT EXISTS (
                    SELECT 1 FROM sys.default_constraints dc
                    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(d.Tbl) AND c.name = d.Col);
                EXEC sp_executesql @sql;
            ");

            // Carried over from the pre-squash migration chain (AddMultiTenancy / AddProfitSettings):
            // the default tenant and its profit settings are the only seed rows not owned by
            // DbInitializer or TenantProvisioningService, so they must stay in the migration.
            // Guarded so the migration is safe on databases that already contain them.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Pharmacies] WHERE [PharmacyID] = 1)
                BEGIN
                    SET IDENTITY_INSERT [Pharmacies] ON;
                    INSERT INTO [Pharmacies] ([PharmacyID], [Name], [Code], [Status], [CreatedAt], [CreatedBy], [IsActive])
                    VALUES (1, N'Default Pharmacy', N'DEFAULT', N'Active', '2024-01-01T00:00:00', 1, 1);
                    SET IDENTITY_INSERT [Pharmacies] OFF;
                END;

                IF NOT EXISTS (SELECT 1 FROM [ProfitSettings] WHERE [Pharmacy_ID] = 1)
                BEGIN
                    SET IDENTITY_INSERT [ProfitSettings] ON;
                    INSERT INTO [ProfitSettings] ([SettingsID], [Pharmacy_ID], [RetailProfitPercent], [WholesaleProfitPercent], [PriceRoundingStep], [UpdatedAt], [UpdatedBy])
                    VALUES (1, 1, 20.00, 10.00, 1.00, '2024-01-01T00:00:00', 1);
                    SET IDENTITY_INSERT [ProfitSettings] OFF;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseBudgets");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "FinancialPeriods");

            migrationBuilder.DropTable(
                name: "IdentityUserClaims");

            migrationBuilder.DropTable(
                name: "IdentityUserLogins");

            migrationBuilder.DropTable(
                name: "IdentityUserTokens");

            migrationBuilder.DropTable(
                name: "PageUrls");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "ProductPriceHistories");

            migrationBuilder.DropTable(
                name: "ProductPrices");

            migrationBuilder.DropTable(
                name: "ProfitSettings");

            migrationBuilder.DropTable(
                name: "RolePages");

            migrationBuilder.DropTable(
                name: "StockDetails");

            migrationBuilder.DropTable(
                name: "SupplierCreditNotes");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "VoucherDetails");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "CreditNotes");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PriceTypes");

            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "StockMains");

            migrationBuilder.DropTable(
                name: "SubCategories");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropTable(
                name: "TransactionTypes");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "VoucherTypes");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "AccountSubheads");

            migrationBuilder.DropTable(
                name: "AccountTypes");

            migrationBuilder.DropTable(
                name: "AccountHeads");

            migrationBuilder.DropTable(
                name: "AccountFamilies");

            migrationBuilder.DropTable(
                name: "Pharmacies");
        }
    }
}
