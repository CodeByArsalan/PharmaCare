using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductBatchUnusedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRecalled",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "ManufacturingDate",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "RecallDate",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "RecallReason",
                table: "ProductBatches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecalled",
                table: "ProductBatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufacturingDate",
                table: "ProductBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecallDate",
                table: "ProductBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecallReason",
                table: "ProductBatches",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
