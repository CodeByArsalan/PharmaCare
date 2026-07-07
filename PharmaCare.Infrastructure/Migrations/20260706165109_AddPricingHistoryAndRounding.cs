using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingHistoryAndRounding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceRoundingStep",
                table: "ProfitSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill existing (already-provisioned) tenants with the default "nearest 1.00" rounding
            // rather than the column default of 0 (which would silently disable rounding for them).
            migrationBuilder.Sql("UPDATE ProfitSettings SET PriceRoundingStep = 1.00 WHERE PriceRoundingStep = 0;");

            migrationBuilder.CreateTable(
                name: "ProductPriceHistories",
                columns: table => new
                {
                    ProductPriceHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pharmacy_ID = table.Column<int>(type: "int", nullable: false),
                    Product_ID = table.Column<int>(type: "int", nullable: false),
                    PriceType_ID = table.Column<int>(type: "int", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPriceAtChange = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangedBy = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceHistories", x => x.ProductPriceHistoryID);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_Pharmacies_Pharmacy_ID",
                        column: x => x.Pharmacy_ID,
                        principalTable: "Pharmacies",
                        principalColumn: "PharmacyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_PriceTypes_PriceType_ID",
                        column: x => x.PriceType_ID,
                        principalTable: "PriceTypes",
                        principalColumn: "PriceTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_Pharmacy_ID",
                table: "ProductPriceHistories",
                column: "Pharmacy_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_PriceType_ID",
                table: "ProductPriceHistories",
                column: "PriceType_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_Product_ID_PriceType_ID_EffectiveTo",
                table: "ProductPriceHistories",
                columns: new[] { "Product_ID", "PriceType_ID", "EffectiveTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPriceHistories");

            migrationBuilder.DropColumn(
                name: "PriceRoundingStep",
                table: "ProfitSettings");
        }
    }
}
