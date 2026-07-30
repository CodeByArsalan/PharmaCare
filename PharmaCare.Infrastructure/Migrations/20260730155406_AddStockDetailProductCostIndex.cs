using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockDetailProductCostIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Product_StockMain",
                table: "StockDetails",
                columns: new[] { "Pharmacy_ID", "Product_ID", "StockMain_ID" })
                .Annotation("SqlServer:Include", new[] { "CostPrice", "Quantity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockDetails_Product_StockMain",
                table: "StockDetails");
        }
    }
}
