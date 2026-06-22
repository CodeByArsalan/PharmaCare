using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMainTypeDateStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMains_TransactionType_ID",
                table: "StockMains");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Type_Date_Status",
                table: "StockMains",
                columns: new[] { "TransactionType_ID", "TransactionDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMains_Type_Date_Status",
                table: "StockMains");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_TransactionType_ID",
                table: "StockMains",
                column: "TransactionType_ID");
        }
    }
}
