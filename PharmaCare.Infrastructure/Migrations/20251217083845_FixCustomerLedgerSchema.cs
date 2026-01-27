using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCustomerLedgerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerLedgers_Customers_CustomerID",
                table: "CustomerLedgers");

            // Clean up invalid data that violates the new FK constraint
            migrationBuilder.Sql("DELETE FROM CustomerLedgers WHERE Customer_ID NOT IN (SELECT CustomerID FROM Customers)");

            migrationBuilder.DropIndex(
                name: "IX_CustomerLedgers_CustomerID",
                table: "CustomerLedgers");

            migrationBuilder.DropColumn(
                name: "CustomerID",
                table: "CustomerLedgers");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_Customer_ID",
                table: "CustomerLedgers",
                column: "Customer_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedgers_Customers_Customer_ID",
                table: "CustomerLedgers",
                column: "Customer_ID",
                principalTable: "Customers",
                principalColumn: "CustomerID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerLedgers_Customers_Customer_ID",
                table: "CustomerLedgers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerLedgers_Customer_ID",
                table: "CustomerLedgers");

            migrationBuilder.AddColumn<int>(
                name: "CustomerID",
                table: "CustomerLedgers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_CustomerID",
                table: "CustomerLedgers",
                column: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedgers_Customers_CustomerID",
                table: "CustomerLedgers",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID");
        }
    }
}
