using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModifyPartyAndProductEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Barcode",
                table: "Products",
                newName: "ShortCode");

            migrationBuilder.RenameIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                newName: "IX_Products_ShortCode");

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Parties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Parties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IBAN",
                table: "Parties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "IBAN",
                table: "Parties");

            migrationBuilder.RenameColumn(
                name: "ShortCode",
                table: "Products",
                newName: "Barcode");

            migrationBuilder.RenameIndex(
                name: "IX_Products_ShortCode",
                table: "Products",
                newName: "IX_Products_Barcode");
        }
    }
}
