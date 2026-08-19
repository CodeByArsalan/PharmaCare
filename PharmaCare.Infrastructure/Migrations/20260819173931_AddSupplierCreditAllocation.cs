using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCreditAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocations_Source_NotNull",
                table: "PaymentAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations");

            migrationBuilder.AddColumn<int>(
                name: "SupplierCreditNote_ID",
                table: "PaymentAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_SupplierCreditNote_ID",
                table: "PaymentAllocations",
                column: "SupplierCreditNote_ID");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocations_Source_NotNull",
                table: "PaymentAllocations",
                sql: "[Payment_ID] IS NOT NULL OR [CreditNote_ID] IS NOT NULL OR [SupplierCreditNote_ID] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations",
                sql: "[SourceType] IN ('Receipt','CreditNote','Refund','SupplierCredit')");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_SupplierCreditNotes_SupplierCreditNote_ID",
                table: "PaymentAllocations",
                column: "SupplierCreditNote_ID",
                principalTable: "SupplierCreditNotes",
                principalColumn: "SupplierCreditNoteID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_SupplierCreditNotes_SupplierCreditNote_ID",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_SupplierCreditNote_ID",
                table: "PaymentAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocations_Source_NotNull",
                table: "PaymentAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations");

            migrationBuilder.DropColumn(
                name: "SupplierCreditNote_ID",
                table: "PaymentAllocations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocations_Source_NotNull",
                table: "PaymentAllocations",
                sql: "[Payment_ID] IS NOT NULL OR [CreditNote_ID] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations",
                sql: "[SourceType] IN ('Receipt','CreditNote','Refund')");
        }
    }
}
