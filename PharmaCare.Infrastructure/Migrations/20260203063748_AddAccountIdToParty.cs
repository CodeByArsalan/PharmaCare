using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountIdToParty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Account_ID",
                table: "Parties",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Account_ID",
                table: "Parties",
                column: "Account_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Parties_Accounts_Account_ID",
                table: "Parties",
                column: "Account_ID",
                principalTable: "Accounts",
                principalColumn: "AccountID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parties_Accounts_Account_ID",
                table: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Parties_Account_ID",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "Account_ID",
                table: "Parties");
        }
    }
}
