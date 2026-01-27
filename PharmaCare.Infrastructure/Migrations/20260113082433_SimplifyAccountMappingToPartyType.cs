using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAccountMappingToPartyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountMappings_EntityType_EntityID",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "EntityID",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "AccountMappings");

            migrationBuilder.AddColumn<string>(
                name: "PartyType",
                table: "AccountMappings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_PartyType",
                table: "AccountMappings",
                column: "PartyType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountMappings_PartyType",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "PartyType",
                table: "AccountMappings");

            migrationBuilder.AddColumn<int>(
                name: "EntityID",
                table: "AccountMappings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "AccountMappings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "AccountMappings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_EntityType_EntityID",
                table: "AccountMappings",
                columns: new[] { "EntityType", "EntityID" },
                unique: true);
        }
    }
}
