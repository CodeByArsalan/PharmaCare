using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_VoucherNo",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCreditNotes_CreditNoteNo",
                table: "SupplierCreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_TransactionNo",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_Type_Date_Status",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseBudgets_ExpenseCategory_ID_Year_Month",
                table: "ExpenseBudgets");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_CreditNoteNo",
                table: "CreditNotes");

            // NOTE (multi-tenancy): the existing single-tenant ProfitSettings row (SettingsID=1)
            // is intentionally KEPT and backfilled to the default pharmacy below, not deleted.

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "VoucherDetails",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "UserRoles",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "SupplierCreditNotes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "SubCategories",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "StockMains",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "StockDetails",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Roles",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "RolePages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "ProfitSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "ProductPrices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "PriceTypes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "PaymentAllocations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Parties",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "FinancialPeriods",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "ExpenseCategories",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "ExpenseBudgets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "CreditNotes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "AccountSubheads",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "AccountSubheads",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "AccountHeads",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "AccountHeads",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Pharmacy_ID",
                table: "AccountFamilies",
                type: "int",
                nullable: false,
                defaultValue: 1);

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

            // Default pharmacy that owns all pre-existing (single-tenant) data. Every tenant
            // column above was backfilled to PharmacyID = 1, so this row must exist before the
            // foreign keys below are validated.
            migrationBuilder.InsertData(
                table: "Pharmacies",
                columns: new[] { "PharmacyID", "Name", "Code", "Status", "CreatedAt", "CreatedBy", "IsActive" },
                values: new object[] { 1, "Default Pharmacy", "DEFAULT", "Active", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, true });

            // Backfill well-known chart-of-accounts codes for the existing pharmacy so that
            // PartyService (which now resolves accounts by code, not hardcoded id) keeps working.
            // These ids match the values PartyService previously hardcoded (customer AR head=1/
            // subhead=2, supplier AP head=6/subhead=5). Conditional so they are safe no-ops if the
            // codes were already set or the ids differ.
            migrationBuilder.Sql("UPDATE [AccountHeads]    SET [Code] = 'AR_HEAD' WHERE [AccountHeadID]    = 1 AND [Code] IS NULL;");
            migrationBuilder.Sql("UPDATE [AccountSubheads] SET [Code] = 'AR_SUB'  WHERE [AccountSubheadID] = 2 AND [Code] IS NULL;");
            migrationBuilder.Sql("UPDATE [AccountHeads]    SET [Code] = 'AP_HEAD' WHERE [AccountHeadID]    = 6 AND [Code] IS NULL;");
            migrationBuilder.Sql("UPDATE [AccountSubheads] SET [Code] = 'AP_SUB'  WHERE [AccountSubheadID] = 5 AND [Code] IS NULL;");

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
                name: "IX_VoucherDetails_Pharmacy_ID",
                table: "VoucherDetails",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Pharmacy_ID",
                table: "Users",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_Pharmacy_ID",
                table: "UserRoles",
                column: "Pharmacy_ID");

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
                name: "IX_SubCategories_Pharmacy_ID",
                table: "SubCategories",
                column: "Pharmacy_ID");

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
                name: "IX_StockMains_TransactionType_ID",
                table: "StockMains",
                column: "TransactionType_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Type_Date_Status",
                table: "StockMains",
                columns: new[] { "Pharmacy_ID", "TransactionType_ID", "TransactionDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Pharmacy_ID",
                table: "StockDetails",
                column: "Pharmacy_ID");

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
                name: "IX_RolePages_Pharmacy_ID",
                table: "RolePages",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProfitSettings_Pharmacy_ID",
                table: "ProfitSettings",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Pharmacy_ID",
                table: "Products",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_Pharmacy_ID",
                table: "ProductPrices",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PriceTypes_Pharmacy_ID",
                table: "PriceTypes",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Pharmacy_ID",
                table: "Payments",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_Pharmacy_ID",
                table: "PaymentAllocations",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Pharmacy_ID",
                table: "Parties",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPeriods_Pharmacy_ID",
                table: "FinancialPeriods",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Pharmacy_ID",
                table: "Expenses",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Pharmacy_ID",
                table: "ExpenseCategories",
                column: "Pharmacy_ID");

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
                name: "IX_CreditNotes_Pharmacy_ID",
                table: "CreditNotes",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Pharmacy_ID_CreditNoteNo",
                table: "CreditNotes",
                columns: new[] { "Pharmacy_ID", "CreditNoteNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Pharmacy_ID",
                table: "Categories",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_Pharmacy_ID",
                table: "AccountSubheads",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_Pharmacy_ID_Code",
                table: "AccountSubheads",
                columns: new[] { "Pharmacy_ID", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Pharmacy_ID",
                table: "Accounts",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_Pharmacy_ID",
                table: "AccountHeads",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_Pharmacy_ID_Code",
                table: "AccountHeads",
                columns: new[] { "Pharmacy_ID", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountFamilies_Pharmacy_ID",
                table: "AccountFamilies",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Pharmacies_Code",
                table: "Pharmacies",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountFamilies_Pharmacies_Pharmacy_ID",
                table: "AccountFamilies",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountHeads_Pharmacies_Pharmacy_ID",
                table: "AccountHeads",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Pharmacies_Pharmacy_ID",
                table: "Accounts",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSubheads_Pharmacies_Pharmacy_ID",
                table: "AccountSubheads",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Pharmacies_Pharmacy_ID",
                table: "Categories",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditNotes_Pharmacies_Pharmacy_ID",
                table: "CreditNotes",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseBudgets_Pharmacies_Pharmacy_ID",
                table: "ExpenseBudgets",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseCategories_Pharmacies_Pharmacy_ID",
                table: "ExpenseCategories",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Pharmacies_Pharmacy_ID",
                table: "Expenses",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialPeriods_Pharmacies_Pharmacy_ID",
                table: "FinancialPeriods",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Parties_Pharmacies_Pharmacy_ID",
                table: "Parties",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_Pharmacies_Pharmacy_ID",
                table: "PaymentAllocations",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Pharmacies_Pharmacy_ID",
                table: "Payments",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceTypes_Pharmacies_Pharmacy_ID",
                table: "PriceTypes",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPrices_Pharmacies_Pharmacy_ID",
                table: "ProductPrices",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Pharmacies_Pharmacy_ID",
                table: "Products",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfitSettings_Pharmacies_Pharmacy_ID",
                table: "ProfitSettings",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePages_Pharmacies_Pharmacy_ID",
                table: "RolePages",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Pharmacies_Pharmacy_ID",
                table: "Roles",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockDetails_Pharmacies_Pharmacy_ID",
                table: "StockDetails",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMains_Pharmacies_Pharmacy_ID",
                table: "StockMains",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubCategories_Pharmacies_Pharmacy_ID",
                table: "SubCategories",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierCreditNotes_Pharmacies_Pharmacy_ID",
                table: "SupplierCreditNotes",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Pharmacies_Pharmacy_ID",
                table: "UserRoles",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Pharmacies_Pharmacy_ID",
                table: "Users",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VoucherDetails_Pharmacies_Pharmacy_ID",
                table: "VoucherDetails",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Pharmacies_Pharmacy_ID",
                table: "Vouchers",
                column: "Pharmacy_ID",
                principalTable: "Pharmacies",
                principalColumn: "PharmacyID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountFamilies_Pharmacies_Pharmacy_ID",
                table: "AccountFamilies");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountHeads_Pharmacies_Pharmacy_ID",
                table: "AccountHeads");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Pharmacies_Pharmacy_ID",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSubheads_Pharmacies_Pharmacy_ID",
                table: "AccountSubheads");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Pharmacies_Pharmacy_ID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditNotes_Pharmacies_Pharmacy_ID",
                table: "CreditNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseBudgets_Pharmacies_Pharmacy_ID",
                table: "ExpenseBudgets");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseCategories_Pharmacies_Pharmacy_ID",
                table: "ExpenseCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Pharmacies_Pharmacy_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialPeriods_Pharmacies_Pharmacy_ID",
                table: "FinancialPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_Parties_Pharmacies_Pharmacy_ID",
                table: "Parties");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_Pharmacies_Pharmacy_ID",
                table: "PaymentAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Pharmacies_Pharmacy_ID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceTypes_Pharmacies_Pharmacy_ID",
                table: "PriceTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductPrices_Pharmacies_Pharmacy_ID",
                table: "ProductPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Pharmacies_Pharmacy_ID",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfitSettings_Pharmacies_Pharmacy_ID",
                table: "ProfitSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePages_Pharmacies_Pharmacy_ID",
                table: "RolePages");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Pharmacies_Pharmacy_ID",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_StockDetails_Pharmacies_Pharmacy_ID",
                table: "StockDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMains_Pharmacies_Pharmacy_ID",
                table: "StockMains");

            migrationBuilder.DropForeignKey(
                name: "FK_SubCategories_Pharmacies_Pharmacy_ID",
                table: "SubCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierCreditNotes_Pharmacies_Pharmacy_ID",
                table: "SupplierCreditNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Pharmacies_Pharmacy_ID",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Pharmacies_Pharmacy_ID",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_VoucherDetails_Pharmacies_Pharmacy_ID",
                table: "VoucherDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Pharmacies_Pharmacy_ID",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "Pharmacies");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_Pharmacy_ID",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_Pharmacy_ID_VoucherNo",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_VoucherDetails_Pharmacy_ID",
                table: "VoucherDetails");

            migrationBuilder.DropIndex(
                name: "IX_Users_Pharmacy_ID",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_Pharmacy_ID",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCreditNotes_Pharmacy_ID",
                table: "SupplierCreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCreditNotes_Pharmacy_ID_CreditNoteNo",
                table: "SupplierCreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_SubCategories_Pharmacy_ID",
                table: "SubCategories");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_Pharmacy_ID",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_Pharmacy_ID_TransactionNo",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_TransactionType_ID",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_Type_Date_Status",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_StockDetails_Pharmacy_ID",
                table: "StockDetails");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Pharmacy_ID",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Pharmacy_ID_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RolePages_Pharmacy_ID",
                table: "RolePages");

            migrationBuilder.DropIndex(
                name: "IX_ProfitSettings_Pharmacy_ID",
                table: "ProfitSettings");

            migrationBuilder.DropIndex(
                name: "IX_Products_Pharmacy_ID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductPrices_Pharmacy_ID",
                table: "ProductPrices");

            migrationBuilder.DropIndex(
                name: "IX_PriceTypes_Pharmacy_ID",
                table: "PriceTypes");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Pharmacy_ID",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_Pharmacy_ID",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_Parties_Pharmacy_ID",
                table: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_FinancialPeriods_Pharmacy_ID",
                table: "FinancialPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_Pharmacy_ID",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_Pharmacy_ID",
                table: "ExpenseCategories");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseBudgets_ExpenseCategory_ID",
                table: "ExpenseBudgets");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseBudgets_Pharmacy_ID",
                table: "ExpenseBudgets");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseBudgets_Pharmacy_ID_ExpenseCategory_ID_Year_Month",
                table: "ExpenseBudgets");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_Pharmacy_ID",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_Pharmacy_ID_CreditNoteNo",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Pharmacy_ID",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_AccountSubheads_Pharmacy_ID",
                table: "AccountSubheads");

            migrationBuilder.DropIndex(
                name: "IX_AccountSubheads_Pharmacy_ID_Code",
                table: "AccountSubheads");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_Pharmacy_ID",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_AccountHeads_Pharmacy_ID",
                table: "AccountHeads");

            migrationBuilder.DropIndex(
                name: "IX_AccountHeads_Pharmacy_ID_Code",
                table: "AccountHeads");

            migrationBuilder.DropIndex(
                name: "IX_AccountFamilies_Pharmacy_ID",
                table: "AccountFamilies");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "VoucherDetails");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "SupplierCreditNotes");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "SubCategories");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "StockMains");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "StockDetails");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "RolePages");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "ProfitSettings");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "ProductPrices");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "PriceTypes");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "FinancialPeriods");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "ExpenseBudgets");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "AccountSubheads");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "AccountSubheads");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "AccountHeads");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "AccountHeads");

            migrationBuilder.DropColumn(
                name: "Pharmacy_ID",
                table: "AccountFamilies");

            // (Up no longer deletes the ProfitSettings row, so Down does not re-insert it.)

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_VoucherNo",
                table: "Vouchers",
                column: "VoucherNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_CreditNoteNo",
                table: "SupplierCreditNotes",
                column: "CreditNoteNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_TransactionNo",
                table: "StockMains",
                column: "TransactionNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Type_Date_Status",
                table: "StockMains",
                columns: new[] { "TransactionType_ID", "TransactionDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseBudgets_ExpenseCategory_ID_Year_Month",
                table: "ExpenseBudgets",
                columns: new[] { "ExpenseCategory_ID", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CreditNoteNo",
                table: "CreditNotes",
                column: "CreditNoteNo",
                unique: true);
        }
    }
}
