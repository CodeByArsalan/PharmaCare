using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyPartyEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccount_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_PettyCashFunds_PettyCashFund_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Grns_Suppliers_Supplier_ID",
                table: "Grns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_Supplier_ID",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Suppliers_Supplier_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_Customer_ID",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "BankTransactions");

            migrationBuilder.DropTable(
                name: "CashTransactions");

            migrationBuilder.DropTable(
                name: "CustomerLedgers");

            migrationBuilder.DropTable(
                name: "PettyCashTransactions");

            migrationBuilder.DropTable(
                name: "SupplierLedgers");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "CashShifts");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "PettyCashFunds");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "CashDrawers");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BankAccount_ID",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_PettyCashFund_ID",
                table: "Expenses");

            migrationBuilder.RenameColumn(
                name: "Customer_ID",
                table: "Sales",
                newName: "Party_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Sales_Customer_ID",
                table: "Sales",
                newName: "IX_Sales_Party_ID");

            migrationBuilder.RenameColumn(
                name: "Supplier_ID",
                table: "PurchaseReturns",
                newName: "Party_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseReturns_Supplier_ID",
                table: "PurchaseReturns",
                newName: "IX_PurchaseReturns_Party_ID");

            migrationBuilder.RenameColumn(
                name: "Supplier_ID",
                table: "PurchaseOrders",
                newName: "Party_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseOrders_Supplier_ID",
                table: "PurchaseOrders",
                newName: "IX_PurchaseOrders_Party_ID");

            migrationBuilder.RenameColumn(
                name: "Supplier_ID",
                table: "Grns",
                newName: "Party_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Grns_Supplier_ID",
                table: "Grns",
                newName: "IX_Grns_Party_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_Parties_Party_ID",
                table: "Grns",
                column: "Party_ID",
                principalTable: "Parties",
                principalColumn: "PartyID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Parties_Party_ID",
                table: "PurchaseOrders",
                column: "Party_ID",
                principalTable: "Parties",
                principalColumn: "PartyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Parties_Party_ID",
                table: "PurchaseReturns",
                column: "Party_ID",
                principalTable: "Parties",
                principalColumn: "PartyID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Parties_Party_ID",
                table: "Sales",
                column: "Party_ID",
                principalTable: "Parties",
                principalColumn: "PartyID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grns_Parties_Party_ID",
                table: "Grns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Parties_Party_ID",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Parties_Party_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Parties_Party_ID",
                table: "Sales");

            migrationBuilder.RenameColumn(
                name: "Party_ID",
                table: "Sales",
                newName: "Customer_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Sales_Party_ID",
                table: "Sales",
                newName: "IX_Sales_Customer_ID");

            migrationBuilder.RenameColumn(
                name: "Party_ID",
                table: "PurchaseReturns",
                newName: "Supplier_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseReturns_Party_ID",
                table: "PurchaseReturns",
                newName: "IX_PurchaseReturns_Supplier_ID");

            migrationBuilder.RenameColumn(
                name: "Party_ID",
                table: "PurchaseOrders",
                newName: "Supplier_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseOrders_Party_ID",
                table: "PurchaseOrders",
                newName: "IX_PurchaseOrders_Supplier_ID");

            migrationBuilder.RenameColumn(
                name: "Party_ID",
                table: "Grns",
                newName: "Supplier_ID");

            migrationBuilder.RenameIndex(
                name: "IX_Grns_Party_ID",
                table: "Grns",
                newName: "IX_Grns_Supplier_ID");

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    BankAccountID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IBAN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.BankAccountID);
                });

            migrationBuilder.CreateTable(
                name: "CashDrawers",
                columns: table => new
                {
                    CashDrawerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawers", x => x.CashDrawerID);
                    table.ForeignKey(
                        name: "FK_CashDrawers_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RegisteredDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalReceivables = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerID);
                });

            migrationBuilder.CreateTable(
                name: "PettyCashFunds",
                columns: table => new
                {
                    PettyCashFundID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PettyCashFunds", x => x.PettyCashFundID);
                    table.ForeignKey(
                        name: "FK_PettyCashFunds_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    SupplierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalPayables = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.SupplierID);
                });

            migrationBuilder.CreateTable(
                name: "BankTransactions",
                columns: table => new
                {
                    BankTransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAccount_ID = table.Column<int>(type: "int", nullable: false),
                    TransferTo_BankAccount_ID = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.BankTransactionID);
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankAccounts_BankAccount_ID",
                        column: x => x.BankAccount_ID,
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankTransactions_BankAccounts_TransferTo_BankAccount_ID",
                        column: x => x.TransferTo_BankAccount_ID,
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CashShifts",
                columns: table => new
                {
                    CashShiftID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashDrawer_ID = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy_UserID = table.Column<int>(type: "int", nullable: true),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedBy_UserID = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashShifts", x => x.CashShiftID);
                    table.ForeignKey(
                        name: "FK_CashShifts_CashDrawers_CashDrawer_ID",
                        column: x => x.CashDrawer_ID,
                        principalTable: "CashDrawers",
                        principalColumn: "CashDrawerID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgers",
                columns: table => new
                {
                    LedgerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Customer_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedgers", x => x.LedgerID);
                    table.ForeignKey(
                        name: "FK_CustomerLedgers_Customers_Customer_ID",
                        column: x => x.Customer_ID,
                        principalTable: "Customers",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PettyCashTransactions",
                columns: table => new
                {
                    PettyCashTransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PettyCashFund_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PettyCashTransactions", x => x.PettyCashTransactionID);
                    table.ForeignKey(
                        name: "FK_PettyCashTransactions_PettyCashFunds_PettyCashFund_ID",
                        column: x => x.PettyCashFund_ID,
                        principalTable: "PettyCashFunds",
                        principalColumn: "PettyCashFundID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierLedgers",
                columns: table => new
                {
                    SupplierLedgerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Supplier_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierLedgers", x => x.SupplierLedgerID);
                    table.ForeignKey(
                        name: "FK_SupplierLedgers_Suppliers_Supplier_ID",
                        column: x => x.Supplier_ID,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashTransactions",
                columns: table => new
                {
                    CashTransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashShift_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sale_ID = table.Column<int>(type: "int", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashTransactions", x => x.CashTransactionID);
                    table.ForeignKey(
                        name: "FK_CashTransactions_CashShifts_CashShift_ID",
                        column: x => x.CashShift_ID,
                        principalTable: "CashShifts",
                        principalColumn: "CashShiftID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BankAccount_ID",
                table: "Expenses",
                column: "BankAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_PettyCashFund_ID",
                table: "Expenses",
                column: "PettyCashFund_ID");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankAccount_ID",
                table: "BankTransactions",
                column: "BankAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TransferTo_BankAccount_ID",
                table: "BankTransactions",
                column: "TransferTo_BankAccount_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawers_Store_ID",
                table: "CashDrawers",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_CashDrawer_ID",
                table: "CashShifts",
                column: "CashDrawer_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_CashShift_ID",
                table: "CashTransactions",
                column: "CashShift_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgers_Customer_ID",
                table: "CustomerLedgers",
                column: "Customer_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashFunds_Store_ID",
                table: "PettyCashFunds",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashTransactions_PettyCashFund_ID",
                table: "PettyCashTransactions",
                column: "PettyCashFund_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgers_Supplier_ID",
                table: "SupplierLedgers",
                column: "Supplier_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_BankAccounts_BankAccount_ID",
                table: "Expenses",
                column: "BankAccount_ID",
                principalTable: "BankAccounts",
                principalColumn: "BankAccountID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_PettyCashFunds_PettyCashFund_ID",
                table: "Expenses",
                column: "PettyCashFund_ID",
                principalTable: "PettyCashFunds",
                principalColumn: "PettyCashFundID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_Suppliers_Supplier_ID",
                table: "Grns",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_Supplier_ID",
                table: "PurchaseOrders",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Suppliers_Supplier_ID",
                table: "PurchaseReturns",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_Customer_ID",
                table: "Sales",
                column: "Customer_ID",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
