using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDate",
                table: "PaymentReconciliations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ReconciledBy_UserId",
                table: "PaymentReconciliations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconciledBy_User_ID",
                table: "PaymentReconciliations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemReference",
                table: "PaymentReconciliations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliations_ReconciledBy_UserId",
                table: "PaymentReconciliations",
                column: "ReconciledBy_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentReconciliations_AspNetUsers_ReconciledBy_UserId",
                table: "PaymentReconciliations",
                column: "ReconciledBy_UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentReconciliations_AspNetUsers_ReconciledBy_UserId",
                table: "PaymentReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentReconciliations_ReconciledBy_UserId",
                table: "PaymentReconciliations");

            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "PaymentReconciliations");

            migrationBuilder.DropColumn(
                name: "ReconciledBy_UserId",
                table: "PaymentReconciliations");

            migrationBuilder.DropColumn(
                name: "ReconciledBy_User_ID",
                table: "PaymentReconciliations");

            migrationBuilder.DropColumn(
                name: "SystemReference",
                table: "PaymentReconciliations");
        }
    }
}
