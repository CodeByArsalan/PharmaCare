using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseSourceAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceAccountAccountID",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceAccount_ID",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ChartOfAccounts_SourceAccountAccountID",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_SourceAccountAccountID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SourceAccountAccountID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SourceAccount_ID",
                table: "Expenses");
        }
    }
}
