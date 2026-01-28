using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateDataToUnifiedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ================================================================
            // PHASE 1: Migrate JournalEntry → AccountVoucher
            // ================================================================
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [AccountVouchers] ON;

                INSERT INTO [AccountVouchers] (
                    [VoucherID],
                    [VoucherType_ID],
                    [VoucherCode],
                    [VoucherDate],
                    [SourceTable],
                    [SourceID],
                    [Store_ID],
                    [FiscalPeriod_ID],
                    [TotalDebit],
                    [TotalCredit],
                    [Status],
                    [IsReversed],
                    [ReversedBy_ID],
                    [Reverses_ID],
                    [Narration],
                    [CreatedBy],
                    [CreatedDate]
                )
                SELECT 
                    je.[JournalEntryID],
                    1, -- VoucherType_ID = JV (Journal Voucher)
                    je.[EntryNumber],
                    je.[EntryDate],
                    je.[Source_Table],
                    je.[Source_ID],
                    je.[Store_ID],
                    je.[FiscalPeriod_ID],
                    je.[TotalDebit],
                    je.[TotalCredit],
                    je.[Status],
                    CASE WHEN je.[ReversedByEntry_ID] IS NOT NULL THEN 1 ELSE 0 END,
                    je.[ReversedByEntry_ID],
                    je.[ReversesEntry_ID],
                    je.[Description],
                    ISNULL(je.[CreatedBy], 1),
                    je.[CreatedDate]
                FROM [JournalEntries] je;

                SET IDENTITY_INSERT [AccountVouchers] OFF;
            ");

            // ================================================================
            // PHASE 2: Migrate JournalEntryLine → AccountVoucherDetail
            // ================================================================
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [AccountVoucherDetails] ON;

                INSERT INTO [AccountVoucherDetails] (
                    [VoucherDetailID],
                    [Voucher_ID],
                    [Account_ID],
                    [Dr],
                    [Cr],
                    [Product_ID],
                    [Particulars],
                    [Store_ID]
                )
                SELECT 
                    jel.[JournalEntryLineID],
                    jel.[JournalEntry_ID],
                    jel.[Account_ID],
                    jel.[DebitAmount],
                    jel.[CreditAmount],
                    NULL, -- No product on old journal lines
                    jel.[Description],
                    jel.[Store_ID]
                FROM [JournalEntryLines] jel;

                SET IDENTITY_INSERT [AccountVoucherDetails] OFF;
            ");

            // ================================================================
            // PHASE 3: Migrate Sales → StockMain (InvoiceType=1)
            // ================================================================
            migrationBuilder.Sql(@"
                INSERT INTO [StockMains] (
                    [InvoiceType_ID],
                    [InvoiceNo],
                    [InvoiceDate],
                    [Store_ID],
                    [Party_ID],
                    [SubTotal],
                    [DiscountPercent],
                    [DiscountAmount],
                    [TotalAmount],
                    [PaidAmount],
                    [BalanceAmount],
                    [Status],
                    [PaymentStatus],
                    [PaymentVoucher_ID],
                    [ReferenceStockMain_ID],
                    [Remarks],
                    [VoidReason],
                    [VoidedBy],
                    [VoidedDate],
                    [RefundAmount],
                    [CreatedBy],
                    [CreatedDate],
                    [UpdatedBy],
                    [UpdatedDate]
                )
                SELECT 
                    1, -- InvoiceType_ID = SALE
                    s.[SaleNumber],
                    s.[SaleDate],
                    s.[Store_ID],
                    s.[Party_ID],
                    ISNULL(s.[SubTotal], 0),
                    s.[DiscountPercent],
                    ISNULL(s.[DiscountAmount], 0),
                    ISNULL(s.[Total], 0),
                    ISNULL(s.[AmountPaid], 0),
                    ISNULL(s.[BalanceAmount], 0),
                    s.[Status],
                    s.[PaymentStatus],
                    s.[JournalEntry_ID], -- Links to AccountVoucher
                    NULL,
                    NULL,
                    s.[VoidReason],
                    s.[VoidedBy],
                    s.[VoidedDate],
                    0,
                    ISNULL(s.[CreatedBy], 1),
                    s.[CreatedDate],
                    s.[UpdatedBy],
                    s.[UpdatedDate]
                FROM [Sales] s;

                -- Create mapping for SaleLines migration
                CREATE TABLE #SaleMapping (SaleID INT, StockMainID INT);
                INSERT INTO #SaleMapping (SaleID, StockMainID)
                SELECT s.[SaleID], sm.[StockMainID]
                FROM [Sales] s
                INNER JOIN [StockMains] sm ON sm.[InvoiceNo] = s.[SaleNumber] AND sm.[InvoiceType_ID] = 1;

                -- Migrate SaleLines → StockDetails
                INSERT INTO [StockDetails] (
                    [StockMain_ID],
                    [Product_ID],
                    [ProductBatch_ID],
                    [Quantity],
                    [UnitPrice],
                    [PurchasePrice],
                    [DiscountPercent],
                    [DiscountAmount],
                    [LineTotal],
                    [LineCost],
                    [MovementType],
                    [TotalCost]
                )
                SELECT 
                    m.[StockMainID],
                    sl.[Product_ID],
                    sl.[ProductBatch_ID],
                    sl.[Quantity],
                    sl.[UnitPrice],
                    ISNULL(pb.[CostPrice], ISNULL(p.[OpeningPrice], 0)),
                    sl.[DiscountPercent],
                    ISNULL(sl.[DiscountAmount], 0),
                    ISNULL(sl.[NetAmount], 0),
                    sl.[Quantity] * ISNULL(pb.[CostPrice], ISNULL(p.[OpeningPrice], 0)),
                    'SALE',
                    sl.[Quantity] * ISNULL(pb.[CostPrice], ISNULL(p.[OpeningPrice], 0))
                FROM [SaleLines] sl
                INNER JOIN #SaleMapping m ON m.[SaleID] = sl.[Sale_ID]
                LEFT JOIN [Products] p ON p.[ProductID] = sl.[Product_ID]
                LEFT JOIN [ProductBatches] pb ON pb.[ProductBatchID] = sl.[ProductBatch_ID];

                DROP TABLE #SaleMapping;
            ");

            // ================================================================
            // PHASE 4: Migrate GRNs → StockMain (InvoiceType=2)
            // ================================================================
            migrationBuilder.Sql(@"
                INSERT INTO [StockMains] (
                    [InvoiceType_ID],
                    [InvoiceNo],
                    [InvoiceDate],
                    [Store_ID],
                    [Party_ID],
                    [SubTotal],
                    [DiscountPercent],
                    [DiscountAmount],
                    [TotalAmount],
                    [PaidAmount],
                    [BalanceAmount],
                    [Status],
                    [PaymentStatus],
                    [PaymentVoucher_ID],
                    [SupplierInvoiceNo],
                    [Remarks],
                    [RefundAmount],
                    [CreatedBy],
                    [CreatedDate],
                    [UpdatedBy],
                    [UpdatedDate]
                )
                SELECT 
                    2, -- InvoiceType_ID = PURCHASE
                    g.[GrnNumber],
                    g.[CreatedDate], -- GRN uses CreatedDate
                    g.[Store_ID],
                    g.[Party_ID],
                    ISNULL(g.[TotalAmount], 0),
                    NULL,
                    0,
                    ISNULL(g.[TotalAmount], 0),
                    ISNULL(g.[AmountPaid], 0),
                    ISNULL(g.[BalanceAmount], 0),
                    ISNULL(g.[PaymentStatus], 'Unpaid'), -- Status from PaymentStatus
                    g.[PaymentStatus],
                    g.[JournalEntry_ID],
                    g.[InvoiceNumber], -- InvoiceNumber is supplier invoice
                    NULL, -- No remarks field
                    ISNULL(g.[ReturnedAmount], 0),
                    ISNULL(g.[CreatedBy], 1),
                    g.[CreatedDate],
                    g.[UpdatedBy],
                    g.[UpdatedDate]
                FROM [Grns] g;

                -- Create mapping for GrnItems migration
                CREATE TABLE #GrnMapping (GrnID INT, StockMainID INT);
                INSERT INTO #GrnMapping (GrnID, StockMainID)
                SELECT g.[GrnID], sm.[StockMainID]
                FROM [Grns] g
                INNER JOIN [StockMains] sm ON sm.[InvoiceNo] = g.[GrnNumber] AND sm.[InvoiceType_ID] = 2;

                -- Migrate GrnItems → StockDetails
                INSERT INTO [StockDetails] (
                    [StockMain_ID],
                    [Product_ID],
                    [ProductBatch_ID],
                    [Quantity],
                    [UnitPrice],
                    [PurchasePrice],
                    [DiscountPercent],
                    [DiscountAmount],
                    [LineTotal],
                    [LineCost],
                    [MovementType],
                    [TotalCost]
                )
                SELECT 
                    m.[StockMainID],
                    gi.[Product_ID],
                    pb.[ProductBatchID],
                    gi.[QuantityReceived],
                    gi.[SellingPrice],
                    gi.[CostPrice],
                    NULL,
                    0,
                    gi.[QuantityReceived] * gi.[CostPrice],
                    gi.[QuantityReceived] * gi.[CostPrice],
                    'PURCHASE',
                    gi.[QuantityReceived] * gi.[CostPrice]
                FROM [GrnItems] gi
                INNER JOIN #GrnMapping m ON m.[GrnID] = gi.[Grn_ID]
                LEFT JOIN [ProductBatches] pb ON pb.[BatchNumber] = gi.[BatchNumber] AND pb.[Product_ID] = gi.[Product_ID];

                DROP TABLE #GrnMapping;
            ");

            // ================================================================
            // PHASE 5: Migrate SalesReturns → StockMain (InvoiceType=3)
            // ================================================================
            migrationBuilder.Sql(@"
                INSERT INTO [StockMains] (
                    [InvoiceType_ID],
                    [InvoiceNo],
                    [InvoiceDate],
                    [Store_ID],
                    [Party_ID],
                    [SubTotal],
                    [DiscountAmount],
                    [TotalAmount],
                    [PaidAmount],
                    [BalanceAmount],
                    [Status],
                    [PaymentVoucher_ID],
                    [ReturnReason],
                    [RefundMethod],
                    [RefundAmount],
                    [CreatedBy],
                    [CreatedDate],
                    [UpdatedBy],
                    [UpdatedDate]
                )
                SELECT 
                    3, -- InvoiceType_ID = SALE_RTN
                    sr.[ReturnNumber],
                    sr.[ReturnDate],
                    sr.[Store_ID],
                    sr.[Party_ID],
                    ISNULL(sr.[TotalAmount], 0),
                    0,
                    ISNULL(sr.[TotalAmount], 0),
                    ISNULL(sr.[TotalAmount], 0), -- PaidAmount = RefundAmount
                    0,
                    sr.[Status],
                    sr.[JournalEntry_ID],
                    sr.[ReturnReason],
                    sr.[RefundMethod],
                    ISNULL(sr.[TotalAmount], 0), -- No RefundAmount col, use TotalAmount
                    ISNULL(sr.[CreatedBy], 1),
                    sr.[CreatedDate],
                    sr.[UpdatedBy],
                    sr.[UpdatedDate]
                FROM [SalesReturns] sr;

                -- Create mapping for SalesReturnLines migration
                CREATE TABLE #SRMapping (SalesReturnID INT, StockMainID INT);
                INSERT INTO #SRMapping (SalesReturnID, StockMainID)
                SELECT sr.[SalesReturnID], sm.[StockMainID]
                FROM [SalesReturns] sr
                INNER JOIN [StockMains] sm ON sm.[InvoiceNo] = sr.[ReturnNumber] AND sm.[InvoiceType_ID] = 3;

                -- Migrate SalesReturnLines → StockDetails
                INSERT INTO [StockDetails] (
                    [StockMain_ID],
                    [Product_ID],
                    [ProductBatch_ID],
                    [Quantity],
                    [UnitPrice],
                    [PurchasePrice],
                    [DiscountAmount],
                    [LineTotal],
                    [LineCost],
                    [ReturnReason],
                    [MovementType],
                    [TotalCost]
                )
                SELECT 
                    m.[StockMainID],
                    srl.[Product_ID],
                    srl.[ProductBatch_ID],
                    srl.[Quantity],
                    srl.[UnitPrice],
                    ISNULL(pb.[CostPrice], ISNULL(p.[OpeningPrice], 0)),
                    0,
                    ISNULL(srl.[Amount], srl.[Quantity] * srl.[UnitPrice]),
                    srl.[Quantity] * ISNULL(pb.[CostPrice], ISNULL(p.[OpeningPrice], 0)),
                    NULL, -- No Reason on SalesReturnLine
                    'SALE_RETURN',
                    srl.[Quantity] * ISNULL(pb.[CostPrice], ISNULL(p.[OpeningPrice], 0))
                FROM [SalesReturnLines] srl
                INNER JOIN #SRMapping m ON m.[SalesReturnID] = srl.[SalesReturn_ID]
                LEFT JOIN [Products] p ON p.[ProductID] = srl.[Product_ID]
                LEFT JOIN [ProductBatches] pb ON pb.[ProductBatchID] = srl.[ProductBatch_ID];

                DROP TABLE #SRMapping;
            ");

            // ================================================================
            // PHASE 6: Migrate PurchaseReturns → StockMain (InvoiceType=4)
            // ================================================================
            migrationBuilder.Sql(@"
                INSERT INTO [StockMains] (
                    [InvoiceType_ID],
                    [InvoiceNo],
                    [InvoiceDate],
                    [Store_ID],
                    [Party_ID],
                    [SubTotal],
                    [DiscountAmount],
                    [TotalAmount],
                    [PaidAmount],
                    [BalanceAmount],
                    [Status],
                    [PaymentVoucher_ID],
                    [Remarks],
                    [RefundMethod],
                    [RefundAmount],
                    [RefundVoucher_ID],
                    [CreatedBy],
                    [CreatedDate],
                    [UpdatedBy],
                    [UpdatedDate]
                )
                SELECT 
                    4, -- InvoiceType_ID = PURCH_RTN
                    CONCAT('PRN-', pr.[PurchaseReturnID]), -- Generate ReturnNumber
                    pr.[ReturnDate],
                    pr.[Store_ID],
                    pr.[Party_ID],
                    ISNULL(pr.[TotalAmount], 0),
                    0,
                    ISNULL(pr.[TotalAmount], 0),
                    0,
                    0,
                    pr.[Status],
                    pr.[JournalEntry_ID],
                    pr.[Remarks],
                    pr.[RefundMethod],
                    ISNULL(pr.[RefundAmount], 0),
                    pr.[RefundJournalEntry_ID],
                    ISNULL(pr.[CreatedBy], 1),
                    pr.[CreatedDate],
                    pr.[UpdatedBy],
                    pr.[UpdatedDate]
                FROM [PurchaseReturns] pr;

                -- Create mapping for PurchaseReturnItems migration
                CREATE TABLE #PRMapping (PurchaseReturnID INT, StockMainID INT);
                INSERT INTO #PRMapping (PurchaseReturnID, StockMainID)
                SELECT pr.[PurchaseReturnID], sm.[StockMainID]
                FROM [PurchaseReturns] pr
                INNER JOIN [StockMains] sm ON sm.[InvoiceNo] = CONCAT('PRN-', pr.[PurchaseReturnID]) AND sm.[InvoiceType_ID] = 4;

                -- Migrate PurchaseReturnItems → StockDetails
                INSERT INTO [StockDetails] (
                    [StockMain_ID],
                    [Product_ID],
                    [ProductBatch_ID],
                    [Quantity],
                    [UnitPrice],
                    [PurchasePrice],
                    [DiscountAmount],
                    [LineTotal],
                    [LineCost],
                    [ReturnReason],
                    [MovementType],
                    [TotalCost]
                )
                SELECT 
                    m.[StockMainID],
                    pb.[Product_ID], -- Get from ProductBatch
                    pri.[ProductBatch_ID],
                    pri.[Quantity],
                    pri.[UnitPrice], -- Using UnitPrice (CostPrice in SQL sense)
                    pri.[UnitPrice],
                    0,
                    ISNULL(pri.[TotalLineAmount], pri.[Quantity] * pri.[UnitPrice]),
                    pri.[Quantity] * pri.[UnitPrice],
                    pri.[Reason],
                    'PURCHASE_RETURN',
                    pri.[Quantity] * pri.[UnitPrice]
                FROM [PurchaseReturnItems] pri
                INNER JOIN #PRMapping m ON m.[PurchaseReturnID] = pri.[PurchaseReturn_ID]
                LEFT JOIN [ProductBatches] pb ON pb.[ProductBatchID] = pri.[ProductBatch_ID];

                DROP TABLE #PRMapping;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete migrated data in reverse order
            migrationBuilder.Sql(@"
                -- Delete StockDetails (child records first)
                DELETE FROM [StockDetails] WHERE [MovementType] IN ('SALE', 'PURCHASE', 'SALE_RETURN', 'PURCHASE_RETURN');

                -- Delete StockMains migrated from old tables
                DELETE FROM [StockMains] WHERE [InvoiceType_ID] IN (1, 2, 3, 4);

                -- Delete AccountVoucherDetails (child records first)
                DELETE FROM [AccountVoucherDetails];

                -- Delete AccountVouchers
                DELETE FROM [AccountVouchers];
            ");
        }
    }
}
