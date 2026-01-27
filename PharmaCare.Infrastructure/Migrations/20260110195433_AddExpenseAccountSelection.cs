using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseAccountSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpenseAccount_ID",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseAccount_ID",
                table: "Expenses",
                column: "ExpenseAccount_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ChartOfAccounts_ExpenseAccount_ID",
                table: "Expenses",
                column: "ExpenseAccount_ID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ChartOfAccounts_ExpenseAccount_ID",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ExpenseAccount_ID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ExpenseAccount_ID",
                table: "Expenses");
        }
    }
}
