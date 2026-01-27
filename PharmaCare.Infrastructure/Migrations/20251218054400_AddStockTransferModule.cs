using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTransferModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    StockTransferID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceStore_ID = table.Column<int>(type: "int", nullable: false),
                    DestinationStore_ID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedBy = table.Column<int>(type: "int", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.StockTransferID);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_DestinationStore_ID",
                        column: x => x.DestinationStore_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_SourceStore_ID",
                        column: x => x.SourceStore_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID");
                });

            migrationBuilder.CreateTable(
                name: "StockTransferItems",
                columns: table => new
                {
                    StockTransferItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockTransfer_ID = table.Column<int>(type: "int", nullable: false),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferItems", x => x.StockTransferItemID);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID");
                    table.ForeignKey(
                        name: "FK_StockTransferItems_StockTransfers_StockTransfer_ID",
                        column: x => x.StockTransfer_ID,
                        principalTable: "StockTransfers",
                        principalColumn: "StockTransferID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_ProductBatch_ID",
                table: "StockTransferItems",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_StockTransfer_ID",
                table: "StockTransferItems",
                column: "StockTransfer_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationStore_ID",
                table: "StockTransfers",
                column: "DestinationStore_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_SourceStore_ID",
                table: "StockTransfers",
                column: "SourceStore_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTransferItems");

            migrationBuilder.DropTable(
                name: "StockTransfers");
        }
    }
}
