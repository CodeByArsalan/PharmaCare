using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierToGrn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Supplier_ID",
                table: "Grns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grns_Supplier_ID",
                table: "Grns",
                column: "Supplier_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_Suppliers_Supplier_ID",
                table: "Grns",
                column: "Supplier_ID",
                principalTable: "Suppliers",
                principalColumn: "SupplierID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grns_Suppliers_Supplier_ID",
                table: "Grns");

            migrationBuilder.DropIndex(
                name: "IX_Grns_Supplier_ID",
                table: "Grns");

            migrationBuilder.DropColumn(
                name: "Supplier_ID",
                table: "Grns");
        }
    }
}
