using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReconciliationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReconciliations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentReconciliations",
                columns: table => new
                {
                    PaymentReconciliationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankTransaction_ID = table.Column<int>(type: "int", nullable: true),
                    ReconciledBy_UserId = table.Column<int>(type: "int", nullable: true),
                    Sale_ID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReconciledBy_User_ID = table.Column<int>(type: "int", nullable: true),
                    ReconciledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystemReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReconciliations", x => x.PaymentReconciliationID);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliations_AspNetUsers_ReconciledBy_UserId",
                        column: x => x.ReconciledBy_UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliations_BankTransactions_BankTransaction_ID",
                        column: x => x.BankTransaction_ID,
                        principalTable: "BankTransactions",
                        principalColumn: "BankTransactionID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentReconciliations_Sales_Sale_ID",
                        column: x => x.Sale_ID,
                        principalTable: "Sales",
                        principalColumn: "SaleID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliations_BankTransaction_ID",
                table: "PaymentReconciliations",
                column: "BankTransaction_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliations_ReconciledBy_UserId",
                table: "PaymentReconciliations",
                column: "ReconciledBy_UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliations_Sale_ID",
                table: "PaymentReconciliations",
                column: "Sale_ID");
        }
    }
}
