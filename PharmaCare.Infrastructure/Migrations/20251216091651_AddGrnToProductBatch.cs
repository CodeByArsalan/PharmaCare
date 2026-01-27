using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGrnToProductBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Grn_ID",
                table: "ProductBatches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_Grn_ID",
                table: "ProductBatches",
                column: "Grn_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches",
                column: "Grn_ID",
                principalTable: "Grns",
                principalColumn: "GrnID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Grns_Grn_ID",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_Grn_ID",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "Grn_ID",
                table: "ProductBatches");
        }
    }
}
