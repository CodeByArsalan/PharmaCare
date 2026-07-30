using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedFinancialPeriodPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @AccountingParentID INT = (SELECT TOP 1 PageID FROM Pages WHERE Title = 'Accounting');

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Financial Periods')
BEGIN
    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    VALUES ('Financial Periods', 'fas fa-calendar-alt', @AccountingParentID, 50, 1, 1, 'FinancialPeriod', 'Index', GETDATE(), 1, GETDATE(), 1);
    
    -- Guarded: on a database built purely from migrations no Roles exist yet, and an
    -- unguarded insert here fails the FK and aborts the whole migration run.
    IF EXISTS (SELECT 1 FROM Roles WHERE RoleID = 1)
        INSERT INTO RolePages (Role_ID, Page_ID, CanView, CanCreate, CanEdit, CanDelete)
        VALUES (1, SCOPE_IDENTITY(), 1, 1, 1, 1);
END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
