using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherIdToDependentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Voucher_ID",
                table: "SupplierPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Voucher_ID",
                table: "StockMovements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundVoucher_ID",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Voucher_ID",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Voucher_ID",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Voucher_ID",
                table: "CustomerPayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_Voucher_ID",
                table: "SupplierPayments",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Voucher_ID",
                table: "StockMovements",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_RefundVoucher_ID",
                table: "PurchaseReturns",
                column: "RefundVoucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Voucher_ID",
                table: "PurchaseReturns",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Voucher_ID",
                table: "Expenses",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Voucher_ID",
                table: "CustomerPayments",
                column: "Voucher_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_AccountVouchers_Voucher_ID",
                table: "CustomerPayments",
                column: "Voucher_ID",
                principalTable: "AccountVouchers",
                principalColumn: "VoucherID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_AccountVouchers_Voucher_ID",
                table: "Expenses",
                column: "Voucher_ID",
                principalTable: "AccountVouchers",
                principalColumn: "VoucherID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_AccountVouchers_RefundVoucher_ID",
                table: "PurchaseReturns",
                column: "RefundVoucher_ID",
                principalTable: "AccountVouchers",
                principalColumn: "VoucherID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_AccountVouchers_Voucher_ID",
                table: "PurchaseReturns",
                column: "Voucher_ID",
                principalTable: "AccountVouchers",
                principalColumn: "VoucherID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_AccountVouchers_Voucher_ID",
                table: "StockMovements",
                column: "Voucher_ID",
                principalTable: "AccountVouchers",
                principalColumn: "VoucherID",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_AccountVouchers_Voucher_ID",
                table: "SupplierPayments",
                column: "Voucher_ID",
                principalTable: "AccountVouchers",
                principalColumn: "VoucherID",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_AccountVouchers_Voucher_ID",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_AccountVouchers_Voucher_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_AccountVouchers_RefundVoucher_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_AccountVouchers_Voucher_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_AccountVouchers_Voucher_ID",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_AccountVouchers_Voucher_ID",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_Voucher_ID",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_Voucher_ID",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_RefundVoucher_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_Voucher_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_Voucher_ID",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_Voucher_ID",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "Voucher_ID",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "Voucher_ID",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "RefundVoucher_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "Voucher_ID",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "Voucher_ID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Voucher_ID",
                table: "CustomerPayments");
        }
    }
}
