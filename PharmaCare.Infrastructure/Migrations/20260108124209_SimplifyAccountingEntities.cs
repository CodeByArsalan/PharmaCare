using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAccountingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_ChartOfAccounts_ParentAccount_ID",
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

            migrationBuilder.DropIndex(
                name: "IX_Subheads_SubheadCode",
                table: "Subheads");

            migrationBuilder.DropIndex(
                name: "IX_Heads_HeadCode",
                table: "Heads");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_AccountCode",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_ParentAccount_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ChartOfAccounts_Party_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Subheads");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Subheads");

            migrationBuilder.DropColumn(
                name: "SubheadCode",
                table: "Subheads");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Heads");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Heads");

            migrationBuilder.DropColumn(
                name: "HeadCode",
                table: "Heads");

            migrationBuilder.DropColumn(
                name: "NormalBalance",
                table: "Heads");

            migrationBuilder.DropColumn(
                name: "AccountCode",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "IsControlAccount",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "NormalBalance",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "ParentAccount_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "Party_ID",
                table: "ChartOfAccounts");

            migrationBuilder.RenameColumn(
                name: "AccountClassification",
                table: "ChartOfAccounts",
                newName: "AccountType");

            migrationBuilder.AddColumn<string>(
                name: "Family",
                table: "Heads",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "Subhead_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Head_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountNo",
                table: "ChartOfAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Insert a default Head if none exists for orphaned ChartOfAccounts
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Heads WHERE HeadID = 1)
                BEGIN
                    SET IDENTITY_INSERT Heads ON;
                    INSERT INTO Heads (HeadID, HeadName, Family, IsActive, CreatedBy, CreatedDate)
                    VALUES (1, 'Uncategorized', 'Assets', 1, 1, GETDATE());
                    SET IDENTITY_INSERT Heads OFF;
                END
            ");

            // Insert a default Subhead if none exists for orphaned ChartOfAccounts
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Subheads WHERE SubheadID = 1)
                BEGIN
                    SET IDENTITY_INSERT Subheads ON;
                    INSERT INTO Subheads (SubheadID, SubheadName, Head_ID, IsActive, CreatedBy, CreatedDate)
                    VALUES (1, 'Uncategorized', 1, 1, 1, GETDATE());
                    SET IDENTITY_INSERT Subheads OFF;
                END
            ");

            // Update any ChartOfAccounts with Head_ID = 0 to use the default Head
            migrationBuilder.Sql(@"
                UPDATE ChartOfAccounts SET Head_ID = 1 WHERE Head_ID = 0 OR Head_ID IS NULL;
                UPDATE ChartOfAccounts SET Subhead_ID = 1 WHERE Subhead_ID = 0 OR Subhead_ID IS NULL;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_Heads_Head_ID",
                table: "ChartOfAccounts",
                column: "Head_ID",
                principalTable: "Heads",
                principalColumn: "HeadID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_Subheads_Subhead_ID",
                table: "ChartOfAccounts",
                column: "Subhead_ID",
                principalTable: "Subheads",
                principalColumn: "SubheadID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_Heads_Head_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ChartOfAccounts_Subheads_Subhead_ID",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "Family",
                table: "Heads");

            migrationBuilder.DropColumn(
                name: "AccountNo",
                table: "ChartOfAccounts");

            migrationBuilder.RenameColumn(
                name: "AccountType",
                table: "ChartOfAccounts",
                newName: "AccountClassification");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Subheads",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Subheads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubheadCode",
                table: "Subheads",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Parties",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Heads",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Heads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HeadCode",
                table: "Heads",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalBalance",
                table: "Heads",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "Subhead_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Head_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AccountCode",
                table: "ChartOfAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ChartOfAccounts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "ChartOfAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsControlAccount",
                table: "ChartOfAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalBalance",
                table: "ChartOfAccounts",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ParentAccount_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Party_ID",
                table: "ChartOfAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subheads_SubheadCode",
                table: "Subheads",
                column: "SubheadCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Heads_HeadCode",
                table: "Heads",
                column: "HeadCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountCode",
                table: "ChartOfAccounts",
                column: "AccountCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_ParentAccount_ID",
                table: "ChartOfAccounts",
                column: "ParentAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_Party_ID",
                table: "ChartOfAccounts",
                column: "Party_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_ChartOfAccounts_ChartOfAccounts_ParentAccount_ID",
                table: "ChartOfAccounts",
                column: "ParentAccount_ID",
                principalTable: "ChartOfAccounts",
                principalColumn: "AccountID");

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
    }
}
