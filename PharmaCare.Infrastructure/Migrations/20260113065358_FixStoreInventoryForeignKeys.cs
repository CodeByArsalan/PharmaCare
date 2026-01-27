using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStoreInventoryForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatch_ID",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Stores_Store_ID",
                table: "StoreInventories");

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

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatch_ID",
                table: "StoreInventories",
                column: "ProductBatch_ID",
                principalTable: "ProductBatches",
                principalColumn: "ProductBatchID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoreInventories_Stores_Store_ID",
                table: "StoreInventories",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_ProductBatches_ProductBatch_ID",
                table: "StoreInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_StoreInventories_Stores_Store_ID",
                table: "StoreInventories");

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
        }
    }
}
