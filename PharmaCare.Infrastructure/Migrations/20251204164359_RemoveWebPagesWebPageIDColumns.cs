using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWebPagesWebPageIDColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WebPages_WebPages_WebPagesWebPageID",
                table: "WebPages");

            migrationBuilder.DropForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPagesWebPageID",
                table: "WebPageUrls");

            migrationBuilder.DropIndex(
                name: "IX_WebPageUrls_WebPagesWebPageID",
                table: "WebPageUrls");

            migrationBuilder.DropIndex(
                name: "IX_WebPages_WebPagesWebPageID",
                table: "WebPages");

            migrationBuilder.DropColumn(
                name: "WebPagesWebPageID",
                table: "WebPageUrls");

            migrationBuilder.DropColumn(
                name: "WebPagesWebPageID",
                table: "WebPages");

            migrationBuilder.CreateIndex(
                name: "IX_WebPageUrls_WebPage_ID",
                table: "WebPageUrls",
                column: "WebPage_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPage_ID",
                table: "WebPageUrls",
                column: "WebPage_ID",
                principalTable: "WebPages",
                principalColumn: "WebPageID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPage_ID",
                table: "WebPageUrls");

            migrationBuilder.DropIndex(
                name: "IX_WebPageUrls_WebPage_ID",
                table: "WebPageUrls");

            migrationBuilder.AddColumn<int>(
                name: "WebPagesWebPageID",
                table: "WebPageUrls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WebPagesWebPageID",
                table: "WebPages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebPageUrls_WebPagesWebPageID",
                table: "WebPageUrls",
                column: "WebPagesWebPageID");

            migrationBuilder.CreateIndex(
                name: "IX_WebPages_WebPagesWebPageID",
                table: "WebPages",
                column: "WebPagesWebPageID");

            migrationBuilder.AddForeignKey(
                name: "FK_WebPages_WebPages_WebPagesWebPageID",
                table: "WebPages",
                column: "WebPagesWebPageID",
                principalTable: "WebPages",
                principalColumn: "WebPageID");

            migrationBuilder.AddForeignKey(
                name: "FK_WebPageUrls_WebPages_WebPagesWebPageID",
                table: "WebPageUrls",
                column: "WebPagesWebPageID",
                principalTable: "WebPages",
                principalColumn: "WebPageID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
