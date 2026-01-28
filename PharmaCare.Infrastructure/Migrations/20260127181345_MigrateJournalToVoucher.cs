using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateJournalToVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Migrate Journal Entries to AccountVouchers
            migrationBuilder.Sql(@"
                INSERT INTO AccountVouchers (
                    VoucherType_ID, VoucherCode, VoucherDate, 
                    SourceTable, SourceID, Store_ID, FiscalPeriod_ID, 
                    TotalDebit, TotalCredit, Status, IsReversed, 
                    Narration, CreatedBy, CreatedDate
                )
                SELECT 
                    1, -- Default to Journal Voucher (1)
                    EntryNumber, EntryDate, 
                    Source_Table, Source_ID, 
                    COALESCE((SELECT TOP 1 Store_ID FROM JournalEntryLines WHERE JournalEntry_ID = JournalEntries.JournalEntryID), 1), -- Default Store 1 if specific store not found
                    NULL, -- FiscalPeriod
                    TotalDebit, TotalCredit, Status, 
                    CASE WHEN Status = 'Void' THEN 1 ELSE 0 END, 
                    Description, CreatedBy, CreatedDate
                FROM JournalEntries
                WHERE NOT EXISTS (SELECT 1 FROM AccountVouchers WHERE VoucherCode = JournalEntries.EntryNumber);
            ");

            // 2. Migrate Journal Entry Lines to AccountVoucherDetails
            migrationBuilder.Sql(@"
                INSERT INTO AccountVoucherDetails (
                    Voucher_ID, Account_ID, Dr, Cr, Particulars, Store_ID
                )
                SELECT 
                    AV.VoucherID, 
                    JEL.Account_ID, JEL.DebitAmount, JEL.CreditAmount, JEL.Description, JEL.Store_ID
                FROM JournalEntryLines JEL
                JOIN JournalEntries JE ON JEL.JournalEntry_ID = JE.JournalEntryID
                JOIN AccountVouchers AV ON JE.EntryNumber = AV.VoucherCode
                WHERE NOT EXISTS (SELECT 1 FROM AccountVoucherDetails WHERE Voucher_ID = AV.VoucherID AND Account_ID = JEL.Account_ID AND Dr = JEL.DebitAmount AND Cr = JEL.CreditAmount);
            ");

            // 3. Update Foreign Keys in Dependent Tables

            // SupplierPayments
            migrationBuilder.Sql(@"
                UPDATE SupplierPayments
                SET Voucher_ID = AV.VoucherID
                FROM SupplierPayments SP
                JOIN JournalEntries JE ON SP.JournalEntry_ID = JE.JournalEntryID
                JOIN AccountVouchers AV ON JE.EntryNumber = AV.VoucherCode
                WHERE SP.Voucher_ID IS NULL;
            ");

            // CustomerPayments
            migrationBuilder.Sql(@"
                UPDATE CustomerPayments
                SET Voucher_ID = AV.VoucherID
                FROM CustomerPayments CP
                JOIN JournalEntries JE ON CP.JournalEntry_ID = JE.JournalEntryID
                JOIN AccountVouchers AV ON JE.EntryNumber = AV.VoucherCode
                WHERE CP.Voucher_ID IS NULL;
            ");

            // Expenses
            migrationBuilder.Sql(@"
                UPDATE Expenses
                SET Voucher_ID = AV.VoucherID
                FROM Expenses E
                JOIN JournalEntries JE ON E.JournalEntry_ID = JE.JournalEntryID
                JOIN AccountVouchers AV ON JE.EntryNumber = AV.VoucherCode
                WHERE E.Voucher_ID IS NULL;
            ");

            // PurchaseReturns
            migrationBuilder.Sql(@"
                UPDATE PurchaseReturns
                SET RefundVoucher_ID = AV.VoucherID
                FROM PurchaseReturns PR
                JOIN JournalEntries JE ON PR.RefundJournalEntry_ID = JE.JournalEntryID
                JOIN AccountVouchers AV ON JE.EntryNumber = AV.VoucherCode
                WHERE PR.RefundVoucher_ID IS NULL;
            ");

            // StockMovements
            migrationBuilder.Sql(@"
                UPDATE StockMovements
                SET Voucher_ID = AV.VoucherID
                FROM StockMovements SM
                JOIN JournalEntries JE ON SM.JournalEntry_ID = JE.JournalEntryID
                JOIN AccountVouchers AV ON JE.EntryNumber = AV.VoucherCode
                WHERE SM.Voucher_ID IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
