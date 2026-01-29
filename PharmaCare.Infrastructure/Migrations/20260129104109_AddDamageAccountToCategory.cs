using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDamageAccountToCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DamageAccount_ID",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DamageAccount_ID",
                table: "Categories",
                column: "DamageAccount_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Accounts_DamageAccount_ID",
                table: "Categories",
                column: "DamageAccount_ID",
                principalTable: "Accounts",
                principalColumn: "AccountID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Accounts_DamageAccount_ID",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_DamageAccount_ID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "DamageAccount_ID",
                table: "Categories");
        }
    }
}
