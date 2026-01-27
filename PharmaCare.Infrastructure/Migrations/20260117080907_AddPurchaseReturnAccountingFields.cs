using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReturnAccountingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JournalEntry_ID",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "PurchaseReturns",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RefundJournalEntry_ID",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundMethod",
                table: "PurchaseReturns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundStatus",
                table: "PurchaseReturns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_JournalEntry_ID",
                table: "PurchaseReturns",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_RefundJournalEntry_ID",
                table: "PurchaseReturns",
                column: "RefundJournalEntry_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_JournalEntry_ID",
                table: "PurchaseReturns",
                column: "JournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_RefundJournalEntry_ID",
                table: "PurchaseReturns",
                column: "RefundJournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_JournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_RefundJournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_JournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_RefundJournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "JournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "RefundJournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "RefundMethod",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "RefundStatus",
                table: "PurchaseReturns");
        }
    }
}
