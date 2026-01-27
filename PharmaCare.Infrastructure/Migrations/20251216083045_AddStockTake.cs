using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockTakes",
                columns: table => new
                {
                    StockTakeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakes", x => x.StockTakeID);
                    table.ForeignKey(
                        name: "FK_StockTakes_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID");
                });

            migrationBuilder.CreateTable(
                name: "StockTakeItems",
                columns: table => new
                {
                    StockTakeItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTake_ID = table.Column<int>(type: "int", nullable: false),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: false),
                    SystemQuantity = table.Column<int>(type: "int", nullable: false),
                    PhysicalQuantity = table.Column<int>(type: "int", nullable: false),
                    VarianceCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakeItems", x => x.StockTakeItemID);
                    table.ForeignKey(
                        name: "FK_StockTakeItems_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID");
                    table.ForeignKey(
                        name: "FK_StockTakeItems_StockTakes_StockTake_ID",
                        column: x => x.StockTake_ID,
                        principalTable: "StockTakes",
                        principalColumn: "StockTakeID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItems_ProductBatch_ID",
                table: "StockTakeItems",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItems_StockTake_ID",
                table: "StockTakeItems",
                column: "StockTake_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakes_Store_ID",
                table: "StockTakes",
                column: "Store_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTakeItems");

            migrationBuilder.DropTable(
                name: "StockTakes");
        }
    }
}
