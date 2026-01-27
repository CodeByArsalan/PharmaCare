using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryReversalTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReversedByEntry_ID",
                table: "JournalEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversesEntry_ID",
                table: "JournalEntries",
                type: "int",
                nullable: true);

            // Note: AccountType seed data changes removed - database already has correct values

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversedByEntry_ID",
                table: "JournalEntries",
                column: "ReversedByEntry_ID",
                unique: true,
                filter: "[ReversedByEntry_ID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversesEntry_ID",
                table: "JournalEntries",
                column: "ReversesEntry_ID",
                unique: true,
                filter: "[ReversesEntry_ID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversedByEntry_ID",
                table: "JournalEntries",
                column: "ReversedByEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversesEntry_ID",
                table: "JournalEntries",
                column: "ReversesEntry_ID",
                principalTable: "JournalEntries",
                principalColumn: "JournalEntryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversedByEntry_ID",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversesEntry_ID",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_ReversedByEntry_ID",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_ReversesEntry_ID",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReversedByEntry_ID",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReversesEntry_ID",
                table: "JournalEntries");
        }
    }
}
