using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureSmartDeleteBehavior : Migration
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
                name: "FK_BankTransactions_BankAccounts_BankAccount_ID",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_BankAccounts_TransferTo_BankAccount_ID",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_CashDrawers_Stores_Store_ID",
                table: "CashDrawers");

            migrationBuilder.DropForeignKey(
                name: "FK_CashShifts_CashDrawers_CashDrawer_ID",
                table: "CashShifts");

            migrationBuilder.DropForeignKey(
                name: "FK_CashTransactions_CashShifts_CashShift_ID",
                table: "CashTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerLedgers_Customers_Customer_ID",
                table: "CustomerLedgers");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccount_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ExpenseCategories_ExpenseCategory_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_PettyCashFunds_PettyCashFund_ID",
                table: "Expenses");

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
                name: "FK_LoyaltyTransactions_Customers_CustomerID",
                table: "LoyaltyTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentReconciliations_BankTransactions_BankTransaction_ID",
                table: "PaymentReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentReconciliations_Sales_Sale_ID",
                table: "PaymentReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PettyCashFunds_Stores_Store_ID",
                table: "PettyCashFunds");

            migrationBuilder.DropForeignKey(
                name: "FK_PettyCashTransactions_PettyCashFunds_PettyCashFund_ID",
                table: "PettyCashTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products");

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
                name: "FK_PurchaseReturnItems_ProductBatches_ProductBatch_ID",
                table: "PurchaseReturnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturn_ID",
                table: "PurchaseReturnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Grns_Grn_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Stores_Store_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Suppliers_Supplier_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_ProductBatches_ProductBatch_ID",
                table: "SaleLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_Products_Product_ID",
                table: "SaleLines");

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
                name: "FK_StockAdjustments_ProductBatches_ProductBatch_ID",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustments_Stores_Store_ID",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAlerts_Products_Product_ID",
                table: "StockAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAlerts_Stores_Store_ID",
                table: "StockAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatch_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Stores_Store_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItems_ProductBatches_ProductBatch_ID",
                table: "StockTakeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItems_StockTakes_StockTake_ID",
                table: "StockTakeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakes_Stores_Store_ID",
                table: "StockTakes");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransferItems_ProductBatches_ProductBatch_ID",
                table: "StockTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransferItems_StockTransfers_StockTransfer_ID",
                table: "StockTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransfers_Stores_DestinationStore_ID",
                table: "StockTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransfers_Stores_SourceStore_ID",
                table: "StockTransfers");

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

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_UserTypes_UserType_ID",
                table: "AspNetUsers",
                column: "UserType_ID",
                principalTable: "UserTypes",
                principalColumn: "UserTypeID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_BankAccounts_BankAccount_ID",
                table: "BankTransactions",
                column: "BankAccount_ID",
                principalTable: "BankAccounts",
                principalColumn: "BankAccountID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_BankAccounts_TransferTo_BankAccount_ID",
                table: "BankTransactions",
                column: "TransferTo_BankAccount_ID",
                principalTable: "BankAccounts",
                principalColumn: "BankAccountID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CashDrawers_Stores_Store_ID",
                table: "CashDrawers",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashShifts_CashDrawers_CashDrawer_ID",
                table: "CashShifts",
                column: "CashDrawer_ID",
                principalTable: "CashDrawers",
                principalColumn: "CashDrawerID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransactions_CashShifts_CashShift_ID",
                table: "CashTransactions",
                column: "CashShift_ID",
                principalTable: "CashShifts",
                principalColumn: "CashShiftID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedgers_Customers_Customer_ID",
                table: "CustomerLedgers",
                column: "Customer_ID",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccount_ID",
                table: "Expenses",
                column: "BankAccount_ID",
                principalTable: "BankAccounts",
                principalColumn: "BankAccountID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ExpenseCategories_ExpenseCategory_ID",
                table: "Expenses",
                column: "ExpenseCategory_ID",
                principalTable: "ExpenseCategories",
                principalColumn: "ExpenseCategoryID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_PettyCashFunds_PettyCashFund_ID",
                table: "Expenses",
                column: "PettyCashFund_ID",
                principalTable: "PettyCashFunds",
                principalColumn: "PettyCashFundID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GrnItems_Grns_Grn_ID",
                table: "GrnItems",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GrnItems_Products_Product_ID",
                table: "GrnItems",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_PurchaseOrders_PurchaseOrder_ID",
                table: "Grns",
                column: "PurchaseOrder_ID",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTransactions_Customers_CustomerID",
                table: "LoyaltyTransactions",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentReconciliations_BankTransactions_BankTransaction_ID",
                table: "PaymentReconciliations",
                column: "BankTransaction_ID",
                principalTable: "BankTransactions",
                principalColumn: "BankTransactionID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentReconciliations_Sales_Sale_ID",
                table: "PaymentReconciliations",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashFunds_Stores_Store_ID",
                table: "PettyCashFunds",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashTransactions_PettyCashFunds_PettyCashFund_ID",
                table: "PettyCashTransactions",
                column: "PettyCashFund_ID",
                principalTable: "PettyCashFunds",
                principalColumn: "PettyCashFundID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products",
                column: "Category_ID",
                principalTable: "Categories",
                principalColumn: "CategoryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_Products_Product_ID",
                table: "PurchaseOrderItems",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrder_ID",
                table: "PurchaseOrderItems",
                column: "PurchaseOrder_ID",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_Supplier_ID",
                table: "PurchaseOrders",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnItems_ProductBatches_ProductBatch_ID",
                table: "PurchaseReturnItems",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturn_ID",
                table: "PurchaseReturnItems",
                column: "PurchaseReturn_ID",
                principalTable: "PurchaseReturns",
                principalColumn: "PurchaseReturnID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Grns_Grn_ID",
                table: "PurchaseReturns",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Stores_Store_ID",
                table: "PurchaseReturns",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Suppliers_Supplier_ID",
                table: "PurchaseReturns",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_ProductBatches_ProductBatch_ID",
                table: "SaleLines",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Products_Product_ID",
                table: "SaleLines",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_Customer_ID",
                table: "Sales",
                column: "Customer_ID",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Prescriptions_Prescription_ID",
                table: "Sales",
                column: "Prescription_ID",
                principalTable: "Prescriptions",
                principalColumn: "PrescriptionID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Stores_Store_ID",
                table: "Sales",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustments_ProductBatches_ProductBatch_ID",
                table: "StockAdjustments",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustments_Stores_Store_ID",
                table: "StockAdjustments",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockAlerts_Products_Product_ID",
                table: "StockAlerts",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockAlerts_Stores_Store_ID",
                table: "StockAlerts",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatch_ID",
                table: "StockMovements",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Stores_Store_ID",
                table: "StockMovements",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItems_ProductBatches_ProductBatch_ID",
                table: "StockTakeItems",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItems_StockTakes_StockTake_ID",
                table: "StockTakeItems",
                column: "StockTake_ID",
                principalTable: "StockTakes",
                principalColumn: "StockTakeID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakes_Stores_Store_ID",
                table: "StockTakes",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransferItems_ProductBatches_ProductBatch_ID",
                table: "StockTransferItems",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransferItems_StockTransfers_StockTransfer_ID",
                table: "StockTransferItems",
                column: "StockTransfer_ID",
                principalTable: "StockTransfers",
                principalColumn: "StockTransferID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransfers_Stores_DestinationStore_ID",
                table: "StockTransfers",
                column: "DestinationStore_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransfers_Stores_SourceStore_ID",
                table: "StockTransfers",
                column: "SourceStore_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatch_ID",
                table: "StoreInventories",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_Stores_Store_ID",
                table: "StoreInventories",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWebPages_AspNetUsers_SystemUser_ID",
                table: "UserWebPages",
                column: "SystemUser_ID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWebPages_WebPages_WebPage_ID",
                table: "UserWebPages",
                column: "WebPage_ID",
                principalTable: "WebPages",
                principalColumn: "WebPageID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPage_ID",
                table: "WebPageUrls",
                column: "WebPage_ID",
                principalTable: "WebPages",
                principalColumn: "WebPageID",
                onDelete: ReferentialAction.Restrict);
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
                name: "FK_BankTransactions_BankAccounts_BankAccount_ID",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_BankAccounts_TransferTo_BankAccount_ID",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_CashDrawers_Stores_Store_ID",
                table: "CashDrawers");

            migrationBuilder.DropForeignKey(
                name: "FK_CashShifts_CashDrawers_CashDrawer_ID",
                table: "CashShifts");

            migrationBuilder.DropForeignKey(
                name: "FK_CashTransactions_CashShifts_CashShift_ID",
                table: "CashTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerLedgers_Customers_Customer_ID",
                table: "CustomerLedgers");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccount_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ExpenseCategories_ExpenseCategory_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_PettyCashFunds_PettyCashFund_ID",
                table: "Expenses");

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
                name: "FK_LoyaltyTransactions_Customers_CustomerID",
                table: "LoyaltyTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentReconciliations_BankTransactions_BankTransaction_ID",
                table: "PaymentReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentReconciliations_Sales_Sale_ID",
                table: "PaymentReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PettyCashFunds_Stores_Store_ID",
                table: "PettyCashFunds");

            migrationBuilder.DropForeignKey(
                name: "FK_PettyCashTransactions_PettyCashFunds_PettyCashFund_ID",
                table: "PettyCashTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products");

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
                name: "FK_PurchaseReturnItems_ProductBatches_ProductBatch_ID",
                table: "PurchaseReturnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturn_ID",
                table: "PurchaseReturnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Grns_Grn_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Stores_Store_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Suppliers_Supplier_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_ProductBatches_ProductBatch_ID",
                table: "SaleLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleLines_Products_Product_ID",
                table: "SaleLines");

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
                name: "FK_StockAdjustments_ProductBatches_ProductBatch_ID",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAdjustments_Stores_Store_ID",
                table: "StockAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAlerts_Products_Product_ID",
                table: "StockAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_StockAlerts_Stores_Store_ID",
                table: "StockAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductBatches_ProductBatch_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Stores_Store_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItems_ProductBatches_ProductBatch_ID",
                table: "StockTakeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItems_StockTakes_StockTake_ID",
                table: "StockTakeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakes_Stores_Store_ID",
                table: "StockTakes");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransferItems_ProductBatches_ProductBatch_ID",
                table: "StockTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransferItems_StockTransfers_StockTransfer_ID",
                table: "StockTransferItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransfers_Stores_DestinationStore_ID",
                table: "StockTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransfers_Stores_SourceStore_ID",
                table: "StockTransfers");

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
                name: "FK_BankTransactions_BankAccounts_BankAccount_ID",
                table: "BankTransactions",
                column: "BankAccount_ID",
                principalTable: "BankAccounts",
                principalColumn: "BankAccountID");

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_BankAccounts_TransferTo_BankAccount_ID",
                table: "BankTransactions",
                column: "TransferTo_BankAccount_ID",
                principalTable: "BankAccounts",
                principalColumn: "BankAccountID");

            migrationBuilder.AddForeignKey(
                name: "FK_CashDrawers_Stores_Store_ID",
                table: "CashDrawers",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_CashShifts_CashDrawers_CashDrawer_ID",
                table: "CashShifts",
                column: "CashDrawer_ID",
                principalTable: "CashDrawers",
                principalColumn: "CashDrawerID");

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransactions_CashShifts_CashShift_ID",
                table: "CashTransactions",
                column: "CashShift_ID",
                principalTable: "CashShifts",
                principalColumn: "CashShiftID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerLedgers_Customers_Customer_ID",
                table: "CustomerLedgers",
                column: "Customer_ID",
                principalTable: "Customers",
                principalColumn: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccount_ID",
                table: "Expenses",
                column: "BankAccount_ID",
                principalTable: "BankAccounts",
                principalColumn: "BankAccountID");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ExpenseCategories_ExpenseCategory_ID",
                table: "Expenses",
                column: "ExpenseCategory_ID",
                principalTable: "ExpenseCategories",
                principalColumn: "ExpenseCategoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_PettyCashFunds_PettyCashFund_ID",
                table: "Expenses",
                column: "PettyCashFund_ID",
                principalTable: "PettyCashFunds",
                principalColumn: "PettyCashFundID");

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
                name: "FK_LoyaltyTransactions_Customers_CustomerID",
                table: "LoyaltyTransactions",
                column: "CustomerID",
                principalTable: "Customers",
                principalColumn: "CustomerID");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentReconciliations_BankTransactions_BankTransaction_ID",
                table: "PaymentReconciliations",
                column: "BankTransaction_ID",
                principalTable: "BankTransactions",
                principalColumn: "BankTransactionID");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentReconciliations_Sales_Sale_ID",
                table: "PaymentReconciliations",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Sales_Sale_ID",
                table: "Payments",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID");

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashFunds_Stores_Store_ID",
                table: "PettyCashFunds",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashTransactions_PettyCashFunds_PettyCashFund_ID",
                table: "PettyCashTransactions",
                column: "PettyCashFund_ID",
                principalTable: "PettyCashFunds",
                principalColumn: "PettyCashFundID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Products_Product_ID",
                table: "ProductBatches",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_Category_ID",
                table: "Products",
                column: "Category_ID",
                principalTable: "Categories",
                principalColumn: "CategoryID");

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
                name: "FK_PurchaseReturnItems_ProductBatches_ProductBatch_ID",
                table: "PurchaseReturnItems",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturn_ID",
                table: "PurchaseReturnItems",
                column: "PurchaseReturn_ID",
                principalTable: "PurchaseReturns",
                principalColumn: "PurchaseReturnID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Grns_Grn_ID",
                table: "PurchaseReturns",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Stores_Store_ID",
                table: "PurchaseReturns",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Suppliers_Supplier_ID",
                table: "PurchaseReturns",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_ProductBatches_ProductBatch_ID",
                table: "SaleLines",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Products_Product_ID",
                table: "SaleLines",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleLines_Sales_Sale_ID",
                table: "SaleLines",
                column: "Sale_ID",
                principalTable: "Sales",
                principalColumn: "SaleID");

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
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustments_ProductBatches_ProductBatch_ID",
                table: "StockAdjustments",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockAdjustments_Stores_Store_ID",
                table: "StockAdjustments",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockAlerts_Products_Product_ID",
                table: "StockAlerts",
                column: "Product_ID",
                principalTable: "Products",
                principalColumn: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockAlerts_Stores_Store_ID",
                table: "StockAlerts",
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
                name: "FK_StockTakeItems_ProductBatches_ProductBatch_ID",
                table: "StockTakeItems",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItems_StockTakes_StockTake_ID",
                table: "StockTakeItems",
                column: "StockTake_ID",
                principalTable: "StockTakes",
                principalColumn: "StockTakeID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakes_Stores_Store_ID",
                table: "StockTakes",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransferItems_ProductBatches_ProductBatch_ID",
                table: "StockTransferItems",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransferItems_StockTransfers_StockTransfer_ID",
                table: "StockTransferItems",
                column: "StockTransfer_ID",
                principalTable: "StockTransfers",
                principalColumn: "StockTransferID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransfers_Stores_DestinationStore_ID",
                table: "StockTransfers",
                column: "DestinationStore_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransfers_Stores_SourceStore_ID",
                table: "StockTransfers",
                column: "SourceStore_ID",
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
    }
}
