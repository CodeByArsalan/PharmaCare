using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStoreLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Stores_Store_ID",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Stores_Store_ID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMains_Stores_DestinationStore_ID",
                table: "StockMains");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMains_Stores_Store_ID",
                table: "StockMains");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Stores_Store_ID",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Stores_Store_ID",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "StoreInventories");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_Store_ID",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Users_Store_ID",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_DestinationStore_ID",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_StockMains_Store_ID",
                table: "StockMains");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Store_ID",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_Store_ID",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Store_ID",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Store_ID",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DestinationStore_ID",
                table: "StockMains");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "StockMains");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "StockMains");

            migrationBuilder.DropColumn(
                name: "Store_ID",
                table: "StockMains");

            migrationBuilder.DropColumn(
                name: "Store_ID",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Store_ID",
                table: "Expenses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Store_ID",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Store_ID",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DestinationStore_ID",
                table: "StockMains",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "StockMains",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedBy",
                table: "StockMains",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Store_ID",
                table: "StockMains",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Store_ID",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Store_ID",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    StoreID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.StoreID);
                });

            migrationBuilder.CreateTable(
                name: "StoreInventories",
                columns: table => new
                {
                    StoreInventoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Product_ID = table.Column<int>(type: "int", nullable: false),
                    Store_ID = table.Column<int>(type: "int", nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInventories", x => x.StoreInventoryID);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Products_Product_ID",
                        column: x => x.Product_ID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreInventories_Stores_Store_ID",
                        column: x => x.Store_ID,
                        principalTable: "Stores",
                        principalColumn: "StoreID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Store_ID",
                table: "Vouchers",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Store_ID",
                table: "Users",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_DestinationStore_ID",
                table: "StockMains",
                column: "DestinationStore_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StockMains_Store_ID",
                table: "StockMains",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Store_ID",
                table: "Payments",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Store_ID",
                table: "Expenses",
                column: "Store_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_Product_ID",
                table: "StoreInventories",
                column: "Product_ID");

            migrationBuilder.CreateIndex(
                name: "IX_StoreInventories_Store_ID_Product_ID",
                table: "StoreInventories",
                columns: new[] { "Store_ID", "Product_ID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Stores_Store_ID",
                table: "Expenses",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Stores_Store_ID",
                table: "Payments",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMains_Stores_DestinationStore_ID",
                table: "StockMains",
                column: "DestinationStore_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMains_Stores_Store_ID",
                table: "StockMains",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Stores_Store_ID",
                table: "Users",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Stores_Store_ID",
                table: "Vouchers",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
