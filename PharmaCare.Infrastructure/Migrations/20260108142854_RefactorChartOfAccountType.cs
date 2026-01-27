using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorChartOfAccountType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new column (nullable initially)
            migrationBuilder.AddColumn<int>(
                name: "AccountType_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true);

            // 2. Migrate Data
            migrationBuilder.Sql(@"
                UPDATE ChartOfAccounts 
                SET AccountType_ID = (SELECT TOP 1 AccountTypeID FROM AccountTypes WHERE TypeName = ChartOfAccounts.AccountType)
            ");

            // Ensure no nulls before making required (Default to 1 - Cash if missing)
            migrationBuilder.Sql("UPDATE ChartOfAccounts SET AccountType_ID = 1 WHERE AccountType_ID IS NULL");

            // 3. Make non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "AccountType_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 4. Drop old columns and constraints
            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_AccountTypes_AccountTypeID",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_AccountTypeID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountTypeID",
                table: "ChartOfAccounts");

            // 5. Add new index and FK
            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountType_ID",
                table: "ChartOfAccounts",
                column: "AccountType_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_AccountTypes_AccountType_ID",
                table: "ChartOfAccounts",
                column: "AccountType_ID",
                principalTable: "AccountTypes",
                principalColumn: "AccountTypeID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_AccountTypes_AccountType_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_AccountType_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountType_ID",
                table: "ChartOfAccounts");

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "ChartOfAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AccountTypeID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountTypeID",
                table: "ChartOfAccounts",
                column: "AccountTypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_AccountTypes_AccountTypeID",
                table: "ChartOfAccounts",
                column: "AccountTypeID",
                principalTable: "AccountTypes",
                principalColumn: "AccountTypeID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
