using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialSystemRebuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_AccountTypes_AccountType_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_AccountType_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountCategory",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountType_ID",
                table: "ChartOfAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "NormalBalance",
                table: "ChartOfAccounts",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ChartOfAccounts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "ChartOfAccounts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AccountCode",
                table: "ChartOfAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "AccountAddress",
                table: "ChartOfAccounts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountClassification",
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

            migrationBuilder.AddColumn<int>(
                name: "Head_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IBAN",
                table: "ChartOfAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Party_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Subhead_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Heads",
                columns: table => new
                {
                    HeadID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HeadCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HeadName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NormalBalance = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Heads", x => x.HeadID);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    PartyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IBAN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.PartyID);
                });

            migrationBuilder.CreateTable(
                name: "Subheads",
                columns: table => new
                {
                    SubheadID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubheadCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubheadName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Head_ID = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subheads", x => x.SubheadID);
                    table.ForeignKey(
                        name: "FK_Subheads_Heads_Head_ID",
                        column: x => x.Head_ID,
                        principalTable: "Heads",
                        principalColumn: "HeadID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountTypeID",
                table: "ChartOfAccounts",
                column: "AccountTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_Head_ID",
                table: "ChartOfAccounts",
                column: "Head_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_Party_ID",
                table: "ChartOfAccounts",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_Subhead_ID",
                table: "ChartOfAccounts",
                column: "Subhead_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Heads_HeadCode",
                table: "Heads",
                column: "HeadCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subheads_Head_ID",
                table: "Subheads",
                column: "Head_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Subheads_SubheadCode",
                table: "Subheads",
                column: "SubheadCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_AccountTypes_AccountTypeID",
                table: "ChartOfAccounts",
                column: "AccountTypeID",
                principalTable: "AccountTypes",
                principalColumn: "AccountTypeID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_Heads_Head_ID",
                table: "ChartOfAccounts",
                column: "Head_ID",
                principalTable: "Heads",
                principalColumn: "HeadID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_Parties_Party_ID",
                table: "ChartOfAccounts",
                column: "Party_ID",
                principalTable: "Parties",
                principalColumn: "PartyID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_Subheads_Subhead_ID",
                table: "ChartOfAccounts",
                column: "Subhead_ID",
                principalTable: "Subheads",
                principalColumn: "SubheadID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_AccountTypes_AccountTypeID",
                table: "ChartOfAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_Heads_Head_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_Parties_Party_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_Subheads_Subhead_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropTable(
                name: "Subheads");

            migrationBuilder.DropTable(
                name: "Heads");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_AccountTypeID",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_Head_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_Party_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_Subhead_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountAddress",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountClassification",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "AccountTypeID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "Head_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "IBAN",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "Party_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "Subhead_ID",
                table: "ChartOfAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "NormalBalance",
                table: "ChartOfAccounts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ChartOfAccounts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountName",
                table: "ChartOfAccounts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "AccountCode",
                table: "ChartOfAccounts",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "AccountCategory",
                table: "ChartOfAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountType_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
    }
}
