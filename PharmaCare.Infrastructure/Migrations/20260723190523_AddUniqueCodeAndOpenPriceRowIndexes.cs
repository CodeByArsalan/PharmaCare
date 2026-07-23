using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCodeAndOpenPriceRowIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountSubheads_Pharmacy_ID_Code",
                table: "AccountSubheads");

            migrationBuilder.DropIndex(
                name: "IX_AccountHeads_Pharmacy_ID_Code",
                table: "AccountHeads");

            migrationBuilder.CreateIndex(
                name: "UX_ProductPriceHistories_OpenRow",
                table: "ProductPriceHistories",
                columns: new[] { "Product_ID", "PriceType_ID" },
                unique: true,
                filter: "[EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_Pharmacy_ID_Code",
                table: "AccountSubheads",
                columns: new[] { "Pharmacy_ID", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_Pharmacy_ID_Code",
                table: "AccountHeads",
                columns: new[] { "Pharmacy_ID", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ProductPriceHistories_OpenRow",
                table: "ProductPriceHistories");

            migrationBuilder.DropIndex(
                name: "IX_AccountSubheads_Pharmacy_ID_Code",
                table: "AccountSubheads");

            migrationBuilder.DropIndex(
                name: "IX_AccountHeads_Pharmacy_ID_Code",
                table: "AccountHeads");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSubheads_Pharmacy_ID_Code",
                table: "AccountSubheads",
                columns: new[] { "Pharmacy_ID", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountHeads_Pharmacy_ID_Code",
                table: "AccountHeads",
                columns: new[] { "Pharmacy_ID", "Code" });
        }
    }
}
