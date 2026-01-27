using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCategorySubCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add CategoryName column first (before any destructive changes)
            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "Categories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Step 2: Copy existing Name values to CategoryName for root categories only
            migrationBuilder.Sql(@"UPDATE Categories SET CategoryName = Name WHERE ParentCategory_ID IS NULL");

            // Step 3: Now drop the old foreign keys and indexes
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategory_ID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategory_ID",
                table: "Categories");

            // Step 4: Delete child categories (they will be recreated as SubCategories)
            migrationBuilder.Sql(@"DELETE FROM Categories WHERE ParentCategory_ID IS NOT NULL");

            // Step 5: Drop the old Name column
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "Category_ID",
                table: "Products",
                newName: "SubCategory_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Products_Category_ID",
                table: "Products",
                newName: "IX_Products_SubCategory_ID");

            migrationBuilder.RenameColumn(
                name: "ParentCategory_ID",
                table: "Categories",
                newName: "UpdatedBy");

            migrationBuilder.AddColumn<int>(
                name: "COGSAccount_ID",
                table: "Categories",
                type: "int",
                nullable: true);

            // Step 6: Make CategoryName NOT NULL now that data is copied
            migrationBuilder.AlterColumn<string>(
                name: "CategoryName",
                table: "Categories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Categories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DamageExpenseAccount_ID",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SaleAccount_ID",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockAccount_ID",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Categories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    SubCategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category_ID = table.Column<int>(type: "int", nullable: false),
                    SubCategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_COGSAccount_ID",
                table: "Categories",
                column: "COGSAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DamageExpenseAccount_ID",
                table: "Categories",
                column: "DamageExpenseAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_SaleAccount_ID",
                table: "Categories",
                column: "SaleAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_StockAccount_ID",
                table: "Categories",
                column: "StockAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_Category_ID",
                table: "SubCategories",
                column: "Category_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_ChartOfAccounts_COGSAccount_ID",
                table: "Categories",
                column: "COGSAccount_ID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_ChartOfAccounts_DamageExpenseAccount_ID",
                table: "Categories",
                column: "DamageExpenseAccount_ID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_ChartOfAccounts_SaleAccount_ID",
                table: "Categories",
                column: "SaleAccount_ID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_ChartOfAccounts_StockAccount_ID",
                table: "Categories",
                column: "StockAccount_ID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_SubCategories_SubCategory_ID",
                table: "Products",
                column: "SubCategory_ID",
                principalTable: "SubCategories",
                principalColumn: "SubCategoryID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_ChartOfAccounts_COGSAccount_ID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_ChartOfAccounts_DamageExpenseAccount_ID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_ChartOfAccounts_SaleAccount_ID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_ChartOfAccounts_StockAccount_ID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_SubCategories_SubCategory_ID",
                table: "Products");

            migrationBuilder.DropTable(
                name: "SubCategories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_COGSAccount_ID",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_DamageExpenseAccount_ID",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_SaleAccount_ID",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_StockAccount_ID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "COGSAccount_ID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "DamageExpenseAccount_ID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "SaleAccount_ID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "StockAccount_ID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "SubCategory_ID",
                table: "Products",
                newName: "Category_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Products_SubCategory_ID",
                table: "Products",
                newName: "IX_Products_Category_ID");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "Categories",
                newName: "ParentCategory_ID");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategory_ID",
                table: "Categories",
                column: "ParentCategory_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategory_ID",
                table: "Categories",
                column: "ParentCategory_ID",
                principalTable: "Categories",
                principalColumn: "CategoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products",
                column: "Category_ID",
                principalTable: "Categories",
                principalColumn: "CategoryID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
