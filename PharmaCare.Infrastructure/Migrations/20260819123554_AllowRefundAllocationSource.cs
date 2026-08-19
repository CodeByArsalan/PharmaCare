using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowRefundAllocationSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations",
                sql: "[SourceType] IN ('Receipt','CreditNote','Refund')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentAllocations_Source_Valid",
                table: "PaymentAllocations",
                sql: "[SourceType] IN ('Receipt','CreditNote')");
        }
    }
}
