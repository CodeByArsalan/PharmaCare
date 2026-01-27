using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupExpenseLegacyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ChartOfAccounts_SourceAccountAccountID",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_SourceAccountAccountID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BankAccount_ID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PettyCashFund_ID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SourceAccountAccountID",
                table: "Expenses");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_SourceAccount_ID",
                table: "Expenses",
                column: "SourceAccount_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ChartOfAccounts_SourceAccount_ID",
                table: "Expenses",
                column: "SourceAccount_ID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ChartOfAccounts_SourceAccount_ID",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_SourceAccount_ID",
                table: "Expenses");

            migrationBuilder.AddColumn<int>(
                name: "BankAccount_ID",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PettyCashFund_ID",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceAccountAccountID",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_SourceAccountAccountID",
                table: "Expenses",
                column: "SourceAccountAccountID");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ChartOfAccounts_SourceAccountAccountID",
                table: "Expenses",
                column: "SourceAccountAccountID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
