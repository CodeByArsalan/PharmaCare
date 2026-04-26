using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRemainingPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @SalesParentID INT = (SELECT TOP 1 PageID FROM Pages WHERE Title = 'Sales Management');
DECLARE @PurchaseParentID INT = (SELECT TOP 1 PageID FROM Pages WHERE Title = 'Purchase Management');

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Customer Ledger')
BEGIN
    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    VALUES ('Customer Ledger', 'fas fa-book', @SalesParentID, 21, 1, 1, 'CustomerPayment', 'CustomerLedger', GETDATE(), 1, GETDATE(), 1);
    
    INSERT INTO RolePages (Role_ID, Page_ID, CanView, CanCreate, CanEdit, CanDelete)
    VALUES (1, SCOPE_IDENTITY(), 1, 1, 1, 1);
END

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Stock Adjustments')
BEGIN
    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    VALUES ('Stock Adjustments', 'fas fa-balance-scale', @PurchaseParentID, 50, 1, 1, 'StockAdjustment', 'AdjustmentIndex', GETDATE(), 1, GETDATE(), 1);
    
    INSERT INTO RolePages (Role_ID, Page_ID, CanView, CanCreate, CanEdit, CanDelete)
    VALUES (1, SCOPE_IDENTITY(), 1, 1, 1, 1);
END

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Advance Receipts')
BEGIN
    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    VALUES ('Advance Receipts', 'fas fa-money-check-alt', @SalesParentID, 22, 1, 1, 'CustomerPayment', 'AdvanceReceiptsIndex', GETDATE(), 1, GETDATE(), 1);
    
    INSERT INTO RolePages (Role_ID, Page_ID, CanView, CanCreate, CanEdit, CanDelete)
    VALUES (1, SCOPE_IDENTITY(), 1, 1, 1, 1);
END

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Supplier Credit Notes')
BEGIN
    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    VALUES ('Supplier Credit Notes', 'fas fa-credit-card', @PurchaseParentID, 51, 1, 1, 'SupplierCreditNote', 'SupplierCreditNotesIndex', GETDATE(), 1, GETDATE(), 1);
    
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
