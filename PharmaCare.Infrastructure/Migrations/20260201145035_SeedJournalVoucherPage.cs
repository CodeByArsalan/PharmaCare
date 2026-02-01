using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedJournalVoucherPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Journal Vouchers')
                BEGIN
                    DECLARE @ParentID INT
                    SELECT @ParentID = PageID FROM Pages WHERE Title = 'Accounting' OR Title = 'Chart of Accounts' -- Fallback
                    
                    -- If no parent found, maybe separate section or root? Set to NULL or 0 via logic, skipping for now if logic complex. 
                    -- Or just insert as root.
                    -- Better: Find 'Transactions' or 'Accounting'.
                    
                    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                    VALUES ('Journal Vouchers', 'fas fa-book', @ParentID, 20, 1, 1, 'JournalVoucher', 'Index', GETDATE(), 1, GETDATE(), 1)

                    DECLARE @PageID INT
                    SELECT @PageID = SCOPE_IDENTITY()

                    -- Grant Admin (Role 1) Access
                    -- Check if Role 1 exists
                    IF EXISTS (SELECT 1 FROM Roles WHERE RoleID = 1)
                    BEGIN
                        INSERT INTO RolePages (Role_ID, Page_ID, CanView, CanCreate, CanEdit, CanDelete)
                        VALUES (1, @PageID, 1, 1, 1, 1)
                    END
                END
            ");
            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Vouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Vouchers");
        }
    }
}
