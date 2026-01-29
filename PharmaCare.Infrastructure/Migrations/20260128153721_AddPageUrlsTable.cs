using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPageUrlsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageUrls",
                columns: table => new
                {
                    PageUrlID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Page_ID = table.Column<int>(type: "int", nullable: false),
                    Controller = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageUrls", x => x.PageUrlID);
                    table.ForeignKey(
                        name: "FK_PageUrls_Pages_Page_ID",
                        column: x => x.Page_ID,
                        principalTable: "Pages",
                        principalColumn: "PageID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageUrls_Controller_Action",
                table: "PageUrls",
                columns: new[] { "Controller", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_PageUrls_Page_ID",
                table: "PageUrls",
                column: "Page_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageUrls");
        }
    }
}
