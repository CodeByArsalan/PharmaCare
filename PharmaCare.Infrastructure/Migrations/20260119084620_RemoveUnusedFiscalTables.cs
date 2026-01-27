using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedFiscalTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntrySequences");

            migrationBuilder.DropTable(
                name: "PeriodOverrides");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntrySequences",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntrySequences", x => x.Year);
                });

            migrationBuilder.CreateTable(
                name: "PeriodOverrides",
                columns: table => new
                {
                    PeriodOverrideID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalPeriod_ID = table.Column<int>(type: "int", nullable: false),
                    JournalEntry_ID = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy = table.Column<int>(type: "int", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Store_ID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodOverrides", x => x.PeriodOverrideID);
                    table.ForeignKey(
                        name: "FK_PeriodOverrides_FiscalPeriods_FiscalPeriod_ID",
                        column: x => x.FiscalPeriod_ID,
                        principalTable: "FiscalPeriods",
                        principalColumn: "FiscalPeriodID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodOverrides_JournalEntries_JournalEntry_ID",
                        column: x => x.JournalEntry_ID,
                        principalTable: "JournalEntries",
                        principalColumn: "JournalEntryID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodOverrides_FiscalPeriod_ID",
                table: "PeriodOverrides",
                column: "FiscalPeriod_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodOverrides_JournalEntry_ID",
                table: "PeriodOverrides",
                column: "JournalEntry_ID");
        }
    }
}
