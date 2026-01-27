using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SetNullDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_UserTypes_UserType_ID",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_GrnItems_Grns_Grn_ID",
                table: "GrnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_GrnItems_Products_Product_ID",
                table: "GrnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Grns_PurchaseOrders_PurchaseOrder_ID",
                table: "Grns");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_Products_Product_ID",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrder_ID",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_Supplier_ID",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines");

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

            migrationBuilder.DropForeignKey(
                name: "FK_UserWebPages_AspNetUsers_SystemUser_ID",
                table: "UserWebPages");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWebPages_WebPages_WebPage_ID",
                table: "UserWebPages");

            migrationBuilder.DropForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPage_ID",
                table: "WebPageUrls");

            migrationBuilder.AlterColumn<int>(
                name: "Store_ID",
                table: "StoreInventories",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ProductBatch_ID",
                table: "StoreInventories",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Store_ID",
                table: "StockMovements",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ProductBatch_ID",
                table: "StockMovements",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Store_ID",
                table: "Sales",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Sale_ID",
                table: "SaleLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "SaleLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ProductBatch_ID",
                table: "SaleLines",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrder_ID",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "ProductBatches",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Sale_ID",
                table: "Payments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrder_ID",
                table: "Grns",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "GrnItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Grn_ID",
                table: "GrnItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_UserTypes_UserType_ID",
                table: "AspNetUsers",
                column: "UserType_ID",
                principalTable: "UserTypes",
                principalColumn: "UserTypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GrnItems_Grns_Grn_ID",
                table: "GrnItems",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID");

            migrationBuilder.AddForeignKey(
                name: "FK_GrnItems_Products_Product_ID",
                table: "GrnItems",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_PurchaseOrders_PurchaseOrder_ID",
                table: "Grns",
                column: "PurchaseOrder_ID",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_Products_Product_ID",
                table: "PurchaseOrderItems",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrder_ID",
                table: "PurchaseOrderItems",
                column: "PurchaseOrder_ID",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_Supplier_ID",
                table: "PurchaseOrders",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Stores_Store_ID",
                table: "Sales",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatch_ID",
                table: "StockMovements",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Stores_Store_ID",
                table: "StockMovements",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatch_ID",
                table: "StoreInventories",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_Stores_Store_ID",
                table: "StoreInventories",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_UserWebPages_AspNetUsers_SystemUser_ID",
                table: "UserWebPages",
                column: "SystemUser_ID",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserWebPages_WebPages_WebPage_ID",
                table: "UserWebPages",
                column: "WebPage_ID",
                principalTable: "WebPages",
                principalColumn: "WebPageID");

            migrationBuilder.AddForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPage_ID",
                table: "WebPageUrls",
                column: "WebPage_ID",
                principalTable: "WebPages",
                principalColumn: "WebPageID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_UserTypes_UserType_ID",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_GrnItems_Grns_Grn_ID",
                table: "GrnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_GrnItems_Products_Product_ID",
                table: "GrnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Grns_PurchaseOrders_PurchaseOrder_ID",
                table: "Grns");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_Products_Product_ID",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrder_ID",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_Supplier_ID",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines");

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

            migrationBuilder.DropForeignKey(
                name: "FK_UserWebPages_AspNetUsers_SystemUser_ID",
                table: "UserWebPages");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWebPages_WebPages_WebPage_ID",
                table: "UserWebPages");

            migrationBuilder.DropForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPage_ID",
                table: "WebPageUrls");

            migrationBuilder.AlterColumn<int>(
                name: "Store_ID",
                table: "StoreInventories",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductBatch_ID",
                table: "StoreInventories",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Store_ID",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductBatch_ID",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Store_ID",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Sale_ID",
                table: "SaleLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "SaleLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductBatch_ID",
                table: "SaleLines",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrder_ID",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "ProductBatches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Sale_ID",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseOrder_ID",
                table: "Grns",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Product_ID",
                table: "GrnItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Grn_ID",
                table: "GrnItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_UserTypes_UserType_ID",
                table: "AspNetUsers",
                column: "UserType_ID",
                principalTable: "UserTypes",
                principalColumn: "UserTypeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GrnItems_Grns_Grn_ID",
                table: "GrnItems",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GrnItems_Products_Product_ID",
                table: "GrnItems",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_PurchaseOrders_PurchaseOrder_ID",
                table: "Grns",
                column: "PurchaseOrder_ID",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderID",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_PurchaseOrderItems_Products_Product_ID",
                table: "PurchaseOrderItems",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrder_ID",
                table: "PurchaseOrderItems",
                column: "PurchaseOrder_ID",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_Supplier_ID",
                table: "PurchaseOrders",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.Cascade);

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

            migrationBuilder.AddForeignKey(
                name: "FK_UserWebPages_AspNetUsers_SystemUser_ID",
                table: "UserWebPages",
                column: "SystemUser_ID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWebPages_WebPages_WebPage_ID",
                table: "UserWebPages",
                column: "WebPage_ID",
                principalTable: "WebPages",
                principalColumn: "WebPageID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPage_ID",
                table: "WebPageUrls",
                column: "WebPage_ID",
                principalTable: "WebPages",
                principalColumn: "WebPageID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
