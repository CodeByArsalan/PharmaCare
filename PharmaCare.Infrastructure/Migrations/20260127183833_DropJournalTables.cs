using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropJournalTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_JournalEntries_JournalEntry_ID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_JournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_RefundJournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_JournalEntries_JournalEntry_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_JournalEntries_JournalEntry_ID",
                table: "SupplierPayments");

            migrationBuilder.DropTable(
                name: "JournalEntryLines");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_JournalEntry_ID",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_JournalEntry_ID",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_JournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_RefundJournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_JournalEntry_ID",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "JournalEntry_ID",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "JournalEntry_ID",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "JournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "RefundJournalEntry_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "JournalEntry_ID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "JournalEntry_ID",
                table: "CustomerPayments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JournalEntry_ID",
                table: "SupplierPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JournalEntry_ID",
                table: "StockMovements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JournalEntry_ID",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundJournalEntry_ID",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JournalEntry_ID",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JournalEntry_ID",
                table: "CustomerPayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    JournalEntryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalPeriod_ID = table.Column<int>(type: "int", nullable: true),
                    ReversedByEntry_ID = table.Column<int>(type: "int", nullable: true),
                    ReversesEntry_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntryNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSystemEntry = table.Column<bool>(type: "bit", nullable: false),
                    PostedBy = table.Column<int>(type: "int", nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source_ID = table.Column<int>(type: "int", nullable: true),
                    Source_Table = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Store_ID = table.Column<int>(type: "int", nullable: true),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.JournalEntryID);
                    table.ForeignKey(
                        name: "FK_JournalEntries_FiscalPeriods_FiscalPeriod_ID",
                        column: x => x.FiscalPeriod_ID,
                        principalTable: "FiscalPeriods",
                        principalColumn: "FiscalPeriodID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_ReversedByEntry_ID",
                        column: x => x.ReversedByEntry_ID,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryID");
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_ReversesEntry_ID",
                        column: x => x.ReversesEntry_ID,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryID");
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLines",
                columns: table => new
                {
                    JournalEntryLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Account_ID = table.Column<int>(type: "int", nullable: false),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: false),
                    Store_ID = table.Column<int>(type: "int", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLines", x => x.JournalEntryLineID);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_ChartOfAccounts_Account_ID",
                        column: x => x.Account_ID,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_JournalEntries_JournalEntry_ID",
                        column: x => x.JournalEntry_ID,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_JournalEntry_ID",
                table: "SupplierPayments",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_JournalEntry_ID",
                table: "StockMovements",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_JournalEntry_ID",
                table: "PurchaseReturns",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_RefundJournalEntry_ID",
                table: "PurchaseReturns",
                column: "RefundJournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_JournalEntry_ID",
                table: "CustomerPayments",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_EntryNumber",
                table: "JournalEntries",
                column: "EntryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_FiscalPeriod_ID",
                table: "JournalEntries",
                column: "FiscalPeriod_ID");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversedByEntry_ID",
                table: "JournalEntries",
                column: "ReversedByEntry_ID",
                unique: true,
                filter: "[ReversedByEntry_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversesEntry_ID",
                table: "JournalEntries",
                column: "ReversesEntry_ID",
                unique: true,
                filter: "[ReversesEntry_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_Account_ID",
                table: "JournalEntryLines",
                column: "Account_ID");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_JournalEntry_ID",
                table: "JournalEntryLines",
                column: "JournalEntry_ID");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_Store_ID",
                table: "JournalEntryLines",
                column: "Store_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_JournalEntries_JournalEntry_ID",
                table: "CustomerPayments",
                column: "JournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_JournalEntry_ID",
                table: "PurchaseReturns",
                column: "JournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_RefundJournalEntry_ID",
                table: "PurchaseReturns",
                column: "RefundJournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_JournalEntries_JournalEntry_ID",
                table: "StockMovements",
                column: "JournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_JournalEntries_JournalEntry_ID",
                table: "SupplierPayments",
                column: "JournalEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
