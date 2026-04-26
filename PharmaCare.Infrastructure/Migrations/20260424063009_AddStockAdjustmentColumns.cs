using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustmentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReason",
                table: "StockMains",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentType",
                table: "StockMains",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM TransactionTypes WHERE Code = 'SADJ+')
                    INSERT INTO TransactionTypes (Code, Name, Category, StockDirection, AffectsStock, CreatesVoucher, IsActive)
                    VALUES ('SADJ+', 'Stock Adjustment In', 'INVENTORY', 1, 1, 1, 1);

                IF NOT EXISTS (SELECT * FROM TransactionTypes WHERE Code = 'SADJ-')
                    INSERT INTO TransactionTypes (Code, Name, Category, StockDirection, AffectsStock, CreatesVoucher, IsActive)
                    VALUES ('SADJ-', 'Stock Adjustment Out', 'INVENTORY', -1, 1, 1, 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustmentReason",
                table: "StockMains");

            migrationBuilder.DropColumn(
                name: "AdjustmentType",
                table: "StockMains");
        }
    }
}
