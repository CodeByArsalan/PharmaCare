using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryToProductAndAccountHeadToAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category_ID",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountHead_ID",
                table: "Accounts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_ID",
                table: "Products",
                column: "Category_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountHead_ID",
                table: "Accounts",
                column: "AccountHead_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountHeads_AccountHead_ID",
                table: "Accounts",
                column: "AccountHead_ID",
                principalTable: "AccountHeads",
                principalColumn: "AccountHeadID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products",
                column: "Category_ID",
                principalTable: "Categories",
                principalColumn: "CategoryID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountHeads_AccountHead_ID",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Category_ID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountHead_ID",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Category_ID",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AccountHead_ID",
                table: "Accounts");
        }
    }
}
