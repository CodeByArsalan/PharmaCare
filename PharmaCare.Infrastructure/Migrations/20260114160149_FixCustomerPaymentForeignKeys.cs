using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCustomerPaymentForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_JournalEntries_JournalEntryID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_Parties_PartyID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_Sales_SaleID",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_JournalEntryID",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_PartyID",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_SaleID",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "JournalEntryID",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "PartyID",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "SaleID",
                table: "CustomerPayments");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_JournalEntry_ID",
                table: "CustomerPayments",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Party_ID",
                table: "CustomerPayments",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Sale_ID",
                table: "CustomerPayments",
                column: "Sale_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_JournalEntries_JournalEntry_ID",
                table: "CustomerPayments",
                column: "JournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_Parties_Party_ID",
                table: "CustomerPayments",
                column: "Party_ID",
                principalTable: "Parties",
                principalColumn: "PartyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_Sales_Sale_ID",
                table: "CustomerPayments",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_JournalEntries_JournalEntry_ID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_Parties_Party_ID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_Sales_Sale_ID",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_JournalEntry_ID",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_Party_ID",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_Sale_ID",
                table: "CustomerPayments");

            migrationBuilder.AddColumn<int>(
                name: "JournalEntryID",
                table: "CustomerPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartyID",
                table: "CustomerPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleID",
                table: "CustomerPayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_JournalEntryID",
                table: "CustomerPayments",
                column: "JournalEntryID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_PartyID",
                table: "CustomerPayments",
                column: "PartyID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_SaleID",
                table: "CustomerPayments",
                column: "SaleID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_JournalEntries_JournalEntryID",
                table: "CustomerPayments",
                column: "JournalEntryID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_Parties_PartyID",
                table: "CustomerPayments",
                column: "PartyID",
                principalTable: "Parties",
                principalColumn: "PartyID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_Sales_SaleID",
                table: "CustomerPayments",
                column: "SaleID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
