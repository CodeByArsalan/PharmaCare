using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixNamingConventions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryCategoryID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sales_SaleID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Products_ProductID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryID",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_Sales_SaleID",
                table: "SaleLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_CustomerID",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Prescriptions_PrescriptionID",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Stores_StoreID",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatchID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Stores_StoreID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatchID",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Stores_StoreID",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "IX_StoreInventories_ProductBatchID",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "IX_StoreInventories_StoreID",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductBatchID",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_StoreID",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CustomerID",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_PrescriptionID",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_StoreID",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleLines_SaleID",
                table: "SaleLines");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_ProductID",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SaleID",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryCategoryID",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ProductBatchID",
                table: "StoreInventories");

            migrationBuilder.DropColumn(
                name: "StoreID",
                table: "StoreInventories");

            migrationBuilder.DropColumn(
                name: "ProductBatchID",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "StoreID",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CustomerID",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PrescriptionID",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "StoreID",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SaleID",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "CategoryID",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductID",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "SaleID",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ParentCategoryCategoryID",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_ProductBatch_ID",
                table: "StoreInventories",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_Store_ID",
                table: "StoreInventories",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductBatch_ID",
                table: "StockMovements",
                column: "ProductBatch_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Store_ID",
                table: "StockMovements",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Customer_ID",
                table: "Sales",
                column: "Customer_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Prescription_ID",
                table: "Sales",
                column: "Prescription_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Store_ID",
                table: "Sales",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_Sale_ID",
                table: "SaleLines",
                column: "Sale_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_ID",
                table: "Products",
                column: "Category_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_Product_ID",
                table: "ProductBatches",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Sale_ID",
                table: "Payments",
                column: "Sale_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategory_ID",
                table: "Categories",
                column: "ParentCategory_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategory_ID",
                table: "Categories",
                column: "ParentCategory_ID",
                principalTable: "Categories",
                principalColumn: "CategoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products",
                column: "Category_ID",
                principalTable: "Categories",
                principalColumn: "CategoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_Customer_ID",
                table: "Sales",
                column: "Customer_ID",
                principalTable: "Customers",
                principalColumn: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Prescriptions_Prescription_ID",
                table: "Sales",
                column: "Prescription_ID",
                principalTable: "Prescriptions",
                principalColumn: "PrescriptionID");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Stores_Store_ID",
                table: "Sales",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatch_ID",
                table: "StockMovements",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Stores_Store_ID",
                table: "StockMovements",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatch_ID",
                table: "StoreInventories",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_Stores_Store_ID",
                table: "StoreInventories",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategory_ID",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_Customer_ID",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Prescriptions_Prescription_ID",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Stores_Store_ID",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatch_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Stores_Store_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatch_ID",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Stores_Store_ID",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "IX_StoreInventories_ProductBatch_ID",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "IX_StoreInventories_Store_ID",
                table: "StoreInventories");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductBatch_ID",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_Store_ID",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Customer_ID",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Prescription_ID",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Store_ID",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleLines_Sale_ID",
                table: "SaleLines");

            migrationBuilder.DropIndex(
                name: "IX_Products_Category_ID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_Product_ID",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Sale_ID",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategory_ID",
                table: "Categories");

            migrationBuilder.AddColumn<int>(
                name: "ProductBatchID",
                table: "StoreInventories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StoreID",
                table: "StoreInventories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProductBatchID",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StoreID",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CustomerID",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrescriptionID",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreID",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SaleID",
                table: "SaleLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CategoryID",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductID",
                table: "ProductBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SaleID",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentCategoryCategoryID",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_ProductBatchID",
                table: "StoreInventories",
                column: "ProductBatchID");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_StoreID",
                table: "StoreInventories",
                column: "StoreID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductBatchID",
                table: "StockMovements",
                column: "ProductBatchID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StoreID",
                table: "StockMovements",
                column: "StoreID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerID",
                table: "Sales",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_PrescriptionID",
                table: "Sales",
                column: "PrescriptionID");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_StoreID",
                table: "Sales",
                column: "StoreID");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLines_SaleID",
                table: "SaleLines",
                column: "SaleID");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryID",
                table: "Products",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductID",
                table: "ProductBatches",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SaleID",
                table: "Payments",
                column: "SaleID");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryCategoryID",
                table: "Categories",
                column: "ParentCategoryCategoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryCategoryID",
                table: "Categories",
                column: "ParentCategoryCategoryID",
                principalTable: "Categories",
                principalColumn: "CategoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Sales_SaleID",
                table: "Payments",
                column: "SaleID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Products_ProductID",
                table: "ProductBatches",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryID",
                table: "Products",
                column: "CategoryID",
                principalTable: "Categories",
                principalColumn: "CategoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Sales_SaleID",
                table: "SaleLines",
                column: "SaleID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_CustomerID",
                table: "Sales",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Prescriptions_PrescriptionID",
                table: "Sales",
                column: "PrescriptionID",
                principalTable: "Prescriptions",
                principalColumn: "PrescriptionID");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Stores_StoreID",
                table: "Sales",
                column: "StoreID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatchID",
                table: "StockMovements",
                column: "ProductBatchID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Stores_StoreID",
                table: "StockMovements",
                column: "StoreID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatchID",
                table: "StoreInventories",
                column: "ProductBatchID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_Stores_StoreID",
                table: "StoreInventories",
                column: "StoreID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
