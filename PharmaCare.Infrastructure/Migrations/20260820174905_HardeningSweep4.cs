using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardeningSweep4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PermissionsStamp",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_Pharmacy_ID_Category_ID_Name",
                table: "SubCategories",
                columns: new[] { "Pharmacy_ID", "Category_ID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Pharmacy_ID_Name",
                table: "Products",
                columns: new[] { "Pharmacy_ID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Pharmacy_ID_Name",
                table: "Categories",
                columns: new[] { "Pharmacy_ID", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubCategories_Pharmacy_ID_Category_ID_Name",
                table: "SubCategories");

            migrationBuilder.DropIndex(
                name: "IX_Products_Pharmacy_ID_Name",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Pharmacy_ID_Name",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "PermissionsStamp",
                table: "Users");
        }
    }
}
