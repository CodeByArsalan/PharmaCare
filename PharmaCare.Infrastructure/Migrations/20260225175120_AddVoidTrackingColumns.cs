using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoidTrackingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoidedBy",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                columns: table => new
                {
                    CreditNoteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditNoteNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Party_ID = table.Column<int>(type: "int", nullable: false),
                    SourceStockMain_ID = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedBy = table.Column<int>(type: "int", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Voucher_ID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNotes", x => x.CreditNoteID);
                    table.CheckConstraint("CK_CreditNotes_Status_Valid", "[Status] IN ('Open','Applied','Void')");
                    table.ForeignKey(
                        name: "FK_CreditNotes_Parties_Party_ID",
                        column: x => x.Party_ID,
                        principalTable: "Parties",
                        principalColumn: "PartyID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_StockMains_SourceStockMain_ID",
                        column: x => x.SourceStockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Vouchers_Voucher_ID",
                        column: x => x.Voucher_ID,
                        principalTable: "Vouchers",
                        principalColumn: "VoucherID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    PaymentAllocationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Payment_ID = table.Column<int>(type: "int", nullable: true),
                    CreditNote_ID = table.Column<int>(type: "int", nullable: true),
                    StockMain_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.PaymentAllocationID);
                    table.CheckConstraint("CK_PaymentAllocations_Source_NotNull", "[Payment_ID] IS NOT NULL OR [CreditNote_ID] IS NOT NULL");
                    table.CheckConstraint("CK_PaymentAllocations_Source_Valid", "[SourceType] IN ('Receipt','CreditNote')");
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_CreditNotes_CreditNote_ID",
                        column: x => x.CreditNote_ID,
                        principalTable: "CreditNotes",
                        principalColumn: "CreditNoteID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_Payment_ID",
                        column: x => x.Payment_ID,
                        principalTable: "Payments",
                        principalColumn: "PaymentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_StockMains_StockMain_ID",
                        column: x => x.StockMain_ID,
                        principalTable: "StockMains",
                        principalColumn: "StockMainID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMains_PaymentStatus_Valid",
                table: "StockMains",
                sql: "[PaymentStatus] IN ('Unpaid','Partial','Paid')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMains_Status_Valid",
                table: "StockMains",
                sql: "[Status] IN ('Draft','Approved','Void')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_PaymentType_Valid",
                table: "Payments",
                sql: "[PaymentType] IN ('RECEIPT','PAYMENT','REFUND','ADJUSTMENT')");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CreditNoteNo",
                table: "CreditNotes",
                column: "CreditNoteNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Party_ID",
                table: "CreditNotes",
                column: "Party_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_SourceStockMain_ID",
                table: "CreditNotes",
                column: "SourceStockMain_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Voucher_ID",
                table: "CreditNotes",
                column: "Voucher_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CreditNote_ID",
                table: "PaymentAllocations",
                column: "CreditNote_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_Payment_ID",
                table: "PaymentAllocations",
                column: "Payment_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_StockMain_ID",
                table: "PaymentAllocations",
                column: "StockMain_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "CreditNotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMains_PaymentStatus_Valid",
                table: "StockMains");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMains_Status_Valid",
                table: "StockMains");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_PaymentType_Valid",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "Payments");
        }
    }
}
