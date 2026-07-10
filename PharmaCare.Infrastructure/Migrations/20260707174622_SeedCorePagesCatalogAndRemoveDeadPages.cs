using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCorePagesCatalogAndRemoveDeadPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------------------------
            // 1) Remove dead menu pages that point at actions which do not exist
            //    (CustomerPayment/CustomerLedger and CustomerPayment/AdvanceReceiptsIndex).
            //    These render as 404 links in the sidebar.
            // ---------------------------------------------------------------------------------
            migrationBuilder.Sql(@"
DELETE rp FROM RolePages rp
INNER JOIN Pages p ON p.PageID = rp.Page_ID
WHERE p.Controller = 'CustomerPayment' AND p.Action IN ('CustomerLedger','AdvanceReceiptsIndex');

DELETE FROM Pages
WHERE Controller = 'CustomerPayment' AND Action IN ('CustomerLedger','AdvanceReceiptsIndex');
");

            // ---------------------------------------------------------------------------------
            // 2) Ensure the core Sales/Purchase page catalog exists so a fresh (migrations-only)
            //    database is self-provisioning. Pages are GLOBAL (not tenant-scoped), so this is
            //    idempotent: each row is inserted only if a page with the same Controller+Action
            //    does not already exist. On an existing DB every row is skipped (no duplicates),
            //    and the admin role (Role_ID = 1) is granted full access to any newly-created page.
            // ---------------------------------------------------------------------------------
            migrationBuilder.Sql(@"
-- Parent menu groups (matched by Title).
IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Sales Management')
BEGIN
    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    VALUES ('Sales Management', 'fas fa-cash-register', NULL, 20, 1, 1, NULL, NULL, GETDATE(), 1, GETDATE(), 1);
END

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Title = 'Purchase Management')
BEGIN
    INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    VALUES ('Purchase Management', 'fas fa-truck', NULL, 30, 1, 1, NULL, NULL, GETDATE(), 1, GETDATE(), 1);
END

DECLARE @SalesParentID INT = (SELECT TOP 1 PageID FROM Pages WHERE Title = 'Sales Management');
DECLARE @PurchaseParentID INT = (SELECT TOP 1 PageID FROM Pages WHERE Title = 'Purchase Management');

-- Core index pages: (Title, Icon, Parent, DisplayOrder, Controller, Action).
DECLARE @CorePages TABLE (
    Title NVARCHAR(200), Icon NVARCHAR(100), Parent_ID INT, DisplayOrder INT,
    Controller NVARCHAR(100), [Action] NVARCHAR(100));

INSERT INTO @CorePages (Title, Icon, Parent_ID, DisplayOrder, Controller, [Action]) VALUES
    ('Sales',                 'fas fa-receipt',          @SalesParentID,    10, 'Sale',              'SalesIndex'),
    ('Sale Returns',          'fas fa-undo',             @SalesParentID,    11, 'SaleReturn',        'SaleReturnsIndex'),
    ('Customer Receipts',     'fas fa-hand-holding-usd', @SalesParentID,    12, 'CustomerPayment',   'ReceiptsIndex'),
    ('Purchase Orders',       'fas fa-file-invoice',     @PurchaseParentID, 40, 'PurchaseOrder',     'PurchaseOrdersIndex'),
    ('Purchases (GRN)',       'fas fa-boxes',            @PurchaseParentID, 41, 'Purchase',          'PurchasesIndex'),
    ('Purchase Returns',      'fas fa-undo-alt',         @PurchaseParentID, 42, 'PurchaseReturn',    'PurchaseReturnsIndex'),
    ('Supplier Payments',     'fas fa-money-bill-wave',  @PurchaseParentID, 43, 'SupplierPayment',   'PaymentsIndex'),
    ('Supplier Credit Notes', 'fas fa-credit-card',      @PurchaseParentID, 51, 'SupplierCreditNote','SupplierCreditNotesIndex'),
    ('Stock Adjustments',     'fas fa-balance-scale',    @PurchaseParentID, 50, 'StockAdjustment',   'AdjustmentIndex');

DECLARE @Title NVARCHAR(200), @Icon NVARCHAR(100), @Parent INT, @Order INT,
        @Controller NVARCHAR(100), @Action NVARCHAR(100), @NewPageID INT;

DECLARE core_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Title, Icon, Parent_ID, DisplayOrder, Controller, [Action] FROM @CorePages;
OPEN core_cursor;
FETCH NEXT FROM core_cursor INTO @Title, @Icon, @Parent, @Order, @Controller, @Action;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Pages WHERE Controller = @Controller AND Action = @Action)
    BEGIN
        INSERT INTO Pages (Title, Icon, Parent_ID, DisplayOrder, IsActive, IsVisible, Controller, Action, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (@Title, @Icon, @Parent, @Order, 1, 1, @Controller, @Action, GETDATE(), 1, GETDATE(), 1);

        SET @NewPageID = SCOPE_IDENTITY();

        IF EXISTS (SELECT 1 FROM Roles WHERE RoleID = 1)
           AND NOT EXISTS (SELECT 1 FROM RolePages WHERE Role_ID = 1 AND Page_ID = @NewPageID)
        BEGIN
            INSERT INTO RolePages (Role_ID, Page_ID, CanView, CanCreate, CanEdit, CanDelete)
            VALUES (1, @NewPageID, 1, 1, 1, 1);
        END
    END
    FETCH NEXT FROM core_cursor INTO @Title, @Icon, @Parent, @Order, @Controller, @Action;
END
CLOSE core_cursor;
DEALLOCATE core_cursor;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-reversible data seed. The dead pages are intentionally not recreated.
        }
    }
}
