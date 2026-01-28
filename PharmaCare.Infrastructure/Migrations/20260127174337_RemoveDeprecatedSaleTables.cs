using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeprecatedSaleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_Sales_Sale_ID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Grns_Grn_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Sales_ConvertedSale_ID",
                table: "Quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_Grns_Grn_ID",
                table: "SupplierPayments");

            migrationBuilder.DropTable(
                name: "GrnItems");

            migrationBuilder.DropTable(
                name: "HeldSaleLines");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "SalesReturnLines");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropTable(
                name: "StockTakeItems");

            migrationBuilder.DropTable(
                name: "StockTransferItems");

            migrationBuilder.DropTable(
                name: "Grns");

            migrationBuilder.DropTable(
                name: "HeldSales");

            migrationBuilder.DropTable(
                name: "SaleLines");

            migrationBuilder.DropTable(
                name: "SalesReturns");

            migrationBuilder.DropTable(
                name: "StockTakes");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.RenameColumn(
                name: "Grn_ID",
                table: "SupplierPayments",
                newName: "StockMain_ID");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierPayments_Grn_ID",
                table: "SupplierPayments",
                newName: "IX_SupplierPayments_StockMain_ID");

            migrationBuilder.RenameColumn(
                name: "ConvertedSale_ID",
                table: "Quotations",
                newName: "ConvertedStockMain_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_ConvertedSale_ID",
                table: "Quotations",
                newName: "IX_Quotations_ConvertedStockMain_ID");

            migrationBuilder.RenameColumn(
                name: "Grn_ID",
                table: "PurchaseReturns",
                newName: "StockMain_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseReturns_Grn_ID",
                table: "PurchaseReturns",
                newName: "IX_PurchaseReturns_StockMain_ID");

            migrationBuilder.RenameColumn(
                name: "Grn_ID",
                table: "ProductBatches",
                newName: "StockMain_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_Grn_ID",
                table: "ProductBatches",
                newName: "IX_ProductBatches_StockMain_ID");

            migrationBuilder.RenameColumn(
                name: "Sale_ID",
                table: "CustomerPayments",
                newName: "StockMain_ID");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerPayments_Sale_ID",
                table: "CustomerPayments",
                newName: "IX_CustomerPayments_StockMain_ID");

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedAmount",
                table: "StockMains",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_StockMains_StockMain_ID",
                table: "CustomerPayments",
                column: "StockMain_ID",
                principalTable: "StockMains",
                principalColumn: "StockMainID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_StockMains_StockMain_ID",
                table: "ProductBatches",
                column: "StockMain_ID",
                principalTable: "StockMains",
                principalColumn: "StockMainID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_StockMains_StockMain_ID",
                table: "PurchaseReturns",
                column: "StockMain_ID",
                principalTable: "StockMains",
                principalColumn: "StockMainID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_StockMains_ConvertedStockMain_ID",
                table: "Quotations",
                column: "ConvertedStockMain_ID",
                principalTable: "StockMains",
                principalColumn: "StockMainID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_StockMains_StockMain_ID",
                table: "SupplierPayments",
                column: "StockMain_ID",
                principalTable: "StockMains",
                principalColumn: "StockMainID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_StockMains_StockMain_ID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_StockMains_StockMain_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_StockMains_StockMain_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_StockMains_ConvertedStockMain_ID",
                table: "Quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_StockMains_StockMain_ID",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "ReturnedAmount",
                table: "StockMains");

            migrationBuilder.RenameColumn(
                name: "StockMain_ID",
                table: "SupplierPayments",
                newName: "Grn_ID");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierPayments_StockMain_ID",
                table: "SupplierPayments",
                newName: "IX_SupplierPayments_Grn_ID");

            migrationBuilder.RenameColumn(
                name: "ConvertedStockMain_ID",
                table: "Quotations",
                newName: "ConvertedSale_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_ConvertedStockMain_ID",
                table: "Quotations",
                newName: "IX_Quotations_ConvertedSale_ID");

            migrationBuilder.RenameColumn(
                name: "StockMain_ID",
                table: "PurchaseReturns",
                newName: "Grn_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseReturns_StockMain_ID",
                table: "PurchaseReturns",
                newName: "IX_PurchaseReturns_Grn_ID");

            migrationBuilder.RenameColumn(
                name: "StockMain_ID",
                table: "ProductBatches",
                newName: "Grn_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ProductBatches_StockMain_ID",
                table: "ProductBatches",
                newName: "IX_ProductBatches_Grn_ID");

            migrationBuilder.RenameColumn(
                name: "StockMain_ID",
                table: "CustomerPayments",
                newName: "Sale_ID");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerPayments_StockMain_ID",
                table: "CustomerPayments",
                newName: "IX_CustomerPayments_Sale_ID");

            migrationBuilder.CreateTable(
                name: "Grns",
                columns: table => new
                {
                    GrnID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Party_ID = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrder_ID = table.Column<int>(type: "int", nullable: true),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrnNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReturnedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grns", x => x.GrnID);
                    table.ForeignKey(
                        name: "FK_Grns_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Grns_PurchaseOrders_PurchaseOrder_ID",
                        column: x => x.PurchaseOrder_ID,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PurchaseOrderID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Grns_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HeldSales",
                columns: table => new
                {
                    HeldSaleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Party_ID = table.Column<int>(type: "int", nullable: true),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HoldDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoldNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeldSales", x => x.HeldSaleID);
                    table.ForeignKey(
                        name: "FK_HeldSales_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HeldSales_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    SaleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Party_ID = table.Column<int>(type: "int", nullable: true),
                    Prescription_ID = table.Column<int>(type: "int", nullable: true),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    AccountingError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SaleNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VoidedBy = table.Column<int>(type: "int", nullable: true),
                    VoidedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.SaleID);
                    table.ForeignKey(
                        name: "FK_Sales_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sales_Prescriptions_Prescription_ID",
                        column: x => x.Prescription_ID,
                        principalTable: "Prescriptions",
                        principalColumn: "PrescriptionID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sales_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    StockAdjustmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: false),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    AdjustedBy = table.Column<int>(type: "int", nullable: false),
                    AdjustmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinancialImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityAdjusted = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.StockAdjustmentID);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTakes",
                columns: table => new
                {
                    StockTakeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    CompletedBy = table.Column<int>(type: "int", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    StockTransferID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestinationStore_ID = table.Column<int>(type: "int", nullable: false),
                    SourceStore_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedBy = table.Column<int>(type: "int", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_SourceStore_ID",
                        column: x => x.SourceStore_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GrnItems",
                columns: table => new
                {
                    GrnItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Grn_ID = table.Column<int>(type: "int", nullable: true),
                    Product_ID = table.Column<int>(type: "int", nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrnItems", x => x.GrnItemID);
                    table.ForeignKey(
                        name: "FK_GrnItems_Grns_Grn_ID",
                        column: x => x.Grn_ID,
                        principalTable: "Grns",
                        principalColumn: "GrnID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GrnItems_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HeldSaleLines",
                columns: table => new
                {
                    HeldSaleLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HeldSale_ID = table.Column<int>(type: "int", nullable: false),
                    Product_ID = table.Column<int>(type: "int", nullable: true),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeldSaleLines", x => x.HeldSaleLineID);
                    table.ForeignKey(
                        name: "FK_HeldSaleLines_HeldSales_HeldSale_ID",
                        column: x => x.HeldSale_ID,
                        principalTable: "HeldSales",
                        principalColumn: "HeldSaleID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeldSaleLines_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HeldSaleLines_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sale_ID = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentID);
                    table.ForeignKey(
                        name: "FK_Payments_Sales_Sale_ID",
                        column: x => x.Sale_ID,
                        principalTable: "Sales",
                        principalColumn: "SaleID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SaleLines",
                columns: table => new
                {
                    SaleLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Product_ID = table.Column<int>(type: "int", nullable: true),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: true),
                    Sale_ID = table.Column<int>(type: "int", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleLines", x => x.SaleLineID);
                    table.ForeignKey(
                        name: "FK_SaleLines_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SaleLines_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SaleLines_Sales_Sale_ID",
                        column: x => x.Sale_ID,
                        principalTable: "Sales",
                        principalColumn: "SaleID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SalesReturns",
                columns: table => new
                {
                    SalesReturnID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    Party_ID = table.Column<int>(type: "int", nullable: true),
                    Sale_ID = table.Column<int>(type: "int", nullable: false),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefundMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReturnNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReturnReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturns", x => x.SalesReturnID);
                    table.ForeignKey(
                        name: "FK_SalesReturns_JournalEntries_JournalEntry_ID",
                        column: x => x.JournalEntry_ID,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Sales_Sale_ID",
                        column: x => x.Sale_ID,
                        principalTable: "Sales",
                        principalColumn: "SaleID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTakeItems",
                columns: table => new
                {
                    StockTakeItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: false),
                    StockTake_ID = table.Column<int>(type: "int", nullable: false),
                    PhysicalQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VarianceCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakeItems", x => x.StockTakeItemID);
                    table.ForeignKey(
                        name: "FK_StockTakeItems_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakeItems_StockTakes_StockTake_ID",
                        column: x => x.StockTake_ID,
                        principalTable: "StockTakes",
                        principalColumn: "StockTakeID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferItems",
                columns: table => new
                {
                    StockTransferItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: false),
                    StockTransfer_ID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferItems", x => x.StockTransferItemID);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_StockTransfers_StockTransfer_ID",
                        column: x => x.StockTransfer_ID,
                        principalTable: "StockTransfers",
                        principalColumn: "StockTransferID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesReturnLines",
                columns: table => new
                {
                    SalesReturnLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Product_ID = table.Column<int>(type: "int", nullable: true),
                    ProductBatch_ID = table.Column<int>(type: "int", nullable: true),
                    SaleLine_ID = table.Column<int>(type: "int", nullable: true),
                    SalesReturn_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RestockInventory = table.Column<bool>(type: "bit", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturnLines", x => x.SalesReturnLineID);
                    table.ForeignKey(
                        name: "FK_SalesReturnLines_ProductBatches_ProductBatch_ID",
                        column: x => x.ProductBatch_ID,
                        principalTable: "ProductBatches",
                        principalColumn: "ProductBatchID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesReturnLines_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesReturnLines_SaleLines_SaleLine_ID",
                        column: x => x.SaleLine_ID,
                        principalTable: "SaleLines",
                        principalColumn: "SaleLineID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesReturnLines_SalesReturns_SalesReturn_ID",
                        column: x => x.SalesReturn_ID,
                        principalTable: "SalesReturns",
                        principalColumn: "SalesReturnID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrnItems_Grn_ID",
                table: "GrnItems",
                column: "Grn_ID");

            migrationBuilder.CreateIndex(
                name: "IX_GrnItems_Product_ID",
                table: "GrnItems",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Grns_Party_ID",
                table: "Grns",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Grns_PurchaseOrder_ID",
                table: "Grns",
                column: "PurchaseOrder_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Grns_Store_ID",
                table: "Grns",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HeldSaleLines_HeldSale_ID",
                table: "HeldSaleLines",
                column: "HeldSale_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HeldSaleLines_Product_ID",
                table: "HeldSaleLines",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HeldSaleLines_ProductBatch_ID",
                table: "HeldSaleLines",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HeldSales_Party_ID",
                table: "HeldSales",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_HeldSales_Store_ID",
                table: "HeldSales",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Sale_ID",
                table: "Payments",
                column: "Sale_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_Product_ID",
                table: "SaleLines",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_ProductBatch_ID",
                table: "SaleLines",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_Sale_ID",
                table: "SaleLines",
                column: "Sale_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Party_ID",
                table: "Sales",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Prescription_ID",
                table: "Sales",
                column: "Prescription_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Store_ID",
                table: "Sales",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnLines_Product_ID",
                table: "SalesReturnLines",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnLines_ProductBatch_ID",
                table: "SalesReturnLines",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnLines_SaleLine_ID",
                table: "SalesReturnLines",
                column: "SaleLine_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnLines_SalesReturn_ID",
                table: "SalesReturnLines",
                column: "SalesReturn_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_JournalEntry_ID",
                table: "SalesReturns",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_Party_ID",
                table: "SalesReturns",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_Sale_ID",
                table: "SalesReturns",
                column: "Sale_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_Store_ID",
                table: "SalesReturns",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ProductBatch_ID",
                table: "StockAdjustments",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Store_ID",
                table: "StockAdjustments",
                column: "Store_ID");

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

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_Sales_Sale_ID",
                table: "CustomerPayments",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Grns_Grn_ID",
                table: "PurchaseReturns",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Sales_ConvertedSale_ID",
                table: "Quotations",
                column: "ConvertedSale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_Grns_Grn_ID",
                table: "SupplierPayments",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
