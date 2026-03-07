using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsWholeSaleToParty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWholeSale",
                table: "Parties",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWholeSale",
                table: "Parties");
        }
    }
}
