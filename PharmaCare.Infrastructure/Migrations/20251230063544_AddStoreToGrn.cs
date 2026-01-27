using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreToGrn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Store_ID",
                table: "Grns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grns_Store_ID",
                table: "Grns",
                column: "Store_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_Stores_Store_ID",
                table: "Grns",
                column: "Store_ID",
                principalTable: "Stores",
                principalColumn: "StoreID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grns_Stores_Store_ID",
                table: "Grns");

            migrationBuilder.DropIndex(
                name: "IX_Grns_Store_ID",
                table: "Grns");

            migrationBuilder.DropColumn(
                name: "Store_ID",
                table: "Grns");
        }
    }
}
