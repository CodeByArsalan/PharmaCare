using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAccountingNamingConventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountHeads_AccountFamilies_FamilyId",
                table: "AccountHeads");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountSubheads_SubheadId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountTypes_AccountTypeId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSubheads_AccountHeads_HeadId",
                table: "AccountSubheads");

            migrationBuilder.RenameColumn(
                name: "HeadId",
                table: "AccountSubheads",
                newName: "AccountHead_ID");

            migrationBuilder.RenameColumn(
                name: "SubheadId",
                table: "AccountSubheads",
                newName: "AccountSubheadID");

            migrationBuilder.RenameIndex(
                name: "IX_AccountSubheads_HeadId",
                table: "AccountSubheads",
                newName: "IX_AccountSubheads_AccountHead_ID");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Accounts",
                newName: "AccountID");

            migrationBuilder.RenameColumn(
                name: "SubheadId",
                table: "Accounts",
                newName: "AccountType_ID");

            migrationBuilder.RenameColumn(
                name: "AccountTypeId",
                table: "Accounts",
                newName: "AccountSubhead_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_SubheadId",
                table: "Accounts",
                newName: "IX_Accounts_AccountType_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_AccountTypeId",
                table: "Accounts",
                newName: "IX_Accounts_AccountSubhead_ID");

            migrationBuilder.RenameColumn(
                name: "FamilyId",
                table: "AccountHeads",
                newName: "AccountFamily_ID");

            migrationBuilder.RenameColumn(
                name: "HeadId",
                table: "AccountHeads",
                newName: "AccountHeadID");

            migrationBuilder.RenameIndex(
                name: "IX_AccountHeads_FamilyId",
                table: "AccountHeads",
                newName: "IX_AccountHeads_AccountFamily_ID");

            migrationBuilder.RenameColumn(
                name: "FamilyId",
                table: "AccountFamilies",
                newName: "AccountFamilyID");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountHeads_AccountFamilies_AccountFamily_ID",
                table: "AccountHeads",
                column: "AccountFamily_ID",
                principalTable: "AccountFamilies",
                principalColumn: "AccountFamilyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountSubheads_AccountSubhead_ID",
                table: "Accounts",
                column: "AccountSubhead_ID",
                principalTable: "AccountSubheads",
                principalColumn: "AccountSubheadID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountTypes_AccountType_ID",
                table: "Accounts",
                column: "AccountType_ID",
                principalTable: "AccountTypes",
                principalColumn: "AccountTypeID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSubheads_AccountHeads_AccountHead_ID",
                table: "AccountSubheads",
                column: "AccountHead_ID",
                principalTable: "AccountHeads",
                principalColumn: "AccountHeadID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountHeads_AccountFamilies_AccountFamily_ID",
                table: "AccountHeads");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountSubheads_AccountSubhead_ID",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountTypes_AccountType_ID",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountSubheads_AccountHeads_AccountHead_ID",
                table: "AccountSubheads");

            migrationBuilder.RenameColumn(
                name: "AccountHead_ID",
                table: "AccountSubheads",
                newName: "HeadId");

            migrationBuilder.RenameColumn(
                name: "AccountSubheadID",
                table: "AccountSubheads",
                newName: "SubheadId");

            migrationBuilder.RenameIndex(
                name: "IX_AccountSubheads_AccountHead_ID",
                table: "AccountSubheads",
                newName: "IX_AccountSubheads_HeadId");

            migrationBuilder.RenameColumn(
                name: "AccountID",
                table: "Accounts",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "AccountType_ID",
                table: "Accounts",
                newName: "SubheadId");

            migrationBuilder.RenameColumn(
                name: "AccountSubhead_ID",
                table: "Accounts",
                newName: "AccountTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_AccountType_ID",
                table: "Accounts",
                newName: "IX_Accounts_SubheadId");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_AccountSubhead_ID",
                table: "Accounts",
                newName: "IX_Accounts_AccountTypeId");

            migrationBuilder.RenameColumn(
                name: "AccountFamily_ID",
                table: "AccountHeads",
                newName: "FamilyId");

            migrationBuilder.RenameColumn(
                name: "AccountHeadID",
                table: "AccountHeads",
                newName: "HeadId");

            migrationBuilder.RenameIndex(
                name: "IX_AccountHeads_AccountFamily_ID",
                table: "AccountHeads",
                newName: "IX_AccountHeads_FamilyId");

            migrationBuilder.RenameColumn(
                name: "AccountFamilyID",
                table: "AccountFamilies",
                newName: "FamilyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountHeads_AccountFamilies_FamilyId",
                table: "AccountHeads",
                column: "FamilyId",
                principalTable: "AccountFamilies",
                principalColumn: "FamilyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountSubheads_SubheadId",
                table: "Accounts",
                column: "SubheadId",
                principalTable: "AccountSubheads",
                principalColumn: "SubheadId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountTypes_AccountTypeId",
                table: "Accounts",
                column: "AccountTypeId",
                principalTable: "AccountTypes",
                principalColumn: "AccountTypeID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountSubheads_AccountHeads_HeadId",
                table: "AccountSubheads",
                column: "HeadId",
                principalTable: "AccountHeads",
                principalColumn: "HeadId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
