using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMappingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountMappings",
                columns: table => new
                {
                    AccountMappingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityID = table.Column<int>(type: "int", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Head_ID = table.Column<int>(type: "int", nullable: true),
                    Subhead_ID = table.Column<int>(type: "int", nullable: true),
                    Account_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMappings", x => x.AccountMappingID);
                    table.ForeignKey(
                        name: "FK_AccountMappings_ChartOfAccounts_Account_ID",
                        column: x => x.Account_ID,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountMappings_Heads_Head_ID",
                        column: x => x.Head_ID,
                        principalTable: "Heads",
                        principalColumn: "HeadID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountMappings_Subheads_Subhead_ID",
                        column: x => x.Subhead_ID,
                        principalTable: "Subheads",
                        principalColumn: "SubheadID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_Account_ID",
                table: "AccountMappings",
                column: "Account_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_EntityType_EntityID",
                table: "AccountMappings",
                columns: new[] { "EntityType", "EntityID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_Head_ID",
                table: "AccountMappings",
                column: "Head_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_Subhead_ID",
                table: "AccountMappings",
                column: "Subhead_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMappings");
        }
    }
}
