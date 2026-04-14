using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfitSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfitSettings",
                columns: table => new
                {
                    SettingsID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetailProfitPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    WholesaleProfitPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfitSettings", x => x.SettingsID);
                });

            migrationBuilder.InsertData(
                table: "ProfitSettings",
                columns: new[] { "SettingsID", "RetailProfitPercent", "UpdatedAt", "UpdatedBy", "WholesaleProfitPercent" },
                values: new object[] { 1, 20m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, 10m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfitSettings");
        }
    }
}
