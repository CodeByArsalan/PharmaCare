using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreToPurchaseReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Store_ID",
                table: "PurchaseReturns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Store_ID",
                table: "PurchaseReturns",
                column: "Store_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Stores_Store_ID",
                table: "PurchaseReturns",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Stores_Store_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_Store_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "Store_ID",
                table: "PurchaseReturns");
        }
    }
}
