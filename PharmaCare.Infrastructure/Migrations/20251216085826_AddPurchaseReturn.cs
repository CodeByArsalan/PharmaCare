using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                columns: table => new
                {
                    PurchaseReturnID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Supplier_ID = table.Column<int>(type: "int", nullable: false),
                    Grn_ID = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturns", x => x.PurchaseReturnID);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Grns_Grn_ID",
                        column: x => x.Grn_ID,
                        principalTable: "Grns",
                        principalColumn: "GrnID");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Suppliers_Supplier_ID",
                        column: x => x.Supplier_ID,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierID");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                columns: table => new
                {
                    PurchaseReturnItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseReturn_ID = table.Column<int>(type: "int", nullable: false),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalLineAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.PurchaseReturnItemID);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID");
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturn_ID",
                        column: x => x.PurchaseReturn_ID,
                        principalTable: "PurchaseReturns",
                        principalColumn: "PurchaseReturnID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductBatch_ID",
                table: "PurchaseReturnItems",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturn_ID",
                table: "PurchaseReturnItems",
                column: "PurchaseReturn_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Grn_ID",
                table: "PurchaseReturns",
                column: "Grn_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Supplier_ID",
                table: "PurchaseReturns",
                column: "Supplier_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseReturnItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturns");
        }
    }
}
