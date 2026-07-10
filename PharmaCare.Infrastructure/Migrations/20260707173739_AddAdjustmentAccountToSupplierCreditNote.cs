using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjustmentAccountToSupplierCreditNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdjustmentAccount_ID",
                table: "SupplierCreditNotes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_AdjustmentAccount_ID",
                table: "SupplierCreditNotes",
                column: "AdjustmentAccount_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierCreditNotes_Accounts_AdjustmentAccount_ID",
                table: "SupplierCreditNotes",
                column: "AdjustmentAccount_ID",
                principalTable: "Accounts",
                principalColumn: "AccountID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierCreditNotes_Accounts_AdjustmentAccount_ID",
                table: "SupplierCreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCreditNotes_AdjustmentAccount_ID",
                table: "SupplierCreditNotes");

            migrationBuilder.DropColumn(
                name: "AdjustmentAccount_ID",
                table: "SupplierCreditNotes");
        }
    }
}
