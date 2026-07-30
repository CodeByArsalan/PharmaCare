using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowCompletedStockMainStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMains_Status_Valid",
                table: "StockMains");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMains_Status_Valid",
                table: "StockMains",
                sql: "[Status] IN ('Draft','Approved','Completed','Void')");

            // ---------------------------------------------------------------------------------
            // DATA BACKFILL (review before applying).
            //
            // Until now the Approved -> Completed transition was decided inside a READ path
            // (PurchaseOrderService.RecalculateOutstandingAsync) which never saved on its own, so
            // it only reached the database when an unrelated SaveChanges happened to flush the
            // tracked entity in the same request. Existing fully-received POs are therefore left
            // at 'Approved' inconsistently, and those keep appearing in the GRN picker forever.
            //
            // This moves exactly those rows: PO-type, currently 'Approved', has at least one line,
            // and no product line has outstanding quantity across its non-void GRNs. Quantity —
            // not value — matches the runtime rule in PurchaseService, so a zero-priced bonus line
            // that has not arrived correctly keeps the PO open. Nothing else is touched.
            // ---------------------------------------------------------------------------------
            migrationBuilder.Sql(@"
UPDATE po
SET Status = 'Completed'
FROM StockMains po
INNER JOIN TransactionTypes tt
        ON tt.TransactionTypeID = po.TransactionType_ID
       AND tt.Code = 'PO'
WHERE po.Status = 'Approved'
  AND EXISTS (SELECT 1 FROM StockDetails d WHERE d.StockMain_ID = po.StockMainID)
  AND NOT EXISTS (
        SELECT 1
        FROM (
            SELECT d.Product_ID, SUM(d.Quantity) AS OrderedQty
            FROM StockDetails d
            WHERE d.StockMain_ID = po.StockMainID
            GROUP BY d.Product_ID
        ) ordered
        WHERE ordered.OrderedQty > ISNULL((
            SELECT SUM(gd.Quantity)
            FROM StockMains grn
            INNER JOIN TransactionTypes gtt
                    ON gtt.TransactionTypeID = grn.TransactionType_ID
                   AND gtt.Code = 'GRN'
            INNER JOIN StockDetails gd
                    ON gd.StockMain_ID = grn.StockMainID
                   AND gd.Product_ID = ordered.Product_ID
            WHERE grn.ReferenceStockMain_ID = po.StockMainID
              AND grn.Status <> 'Void'
        ), 0)
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMains_Status_Valid",
                table: "StockMains");

            // The narrowed constraint below cannot be re-added while 'Completed' rows exist, so
            // fold them back into 'Approved' first. This is lossy: a re-applied Up() recomputes
            // the status from GRN quantities, so nothing is permanently lost.
            migrationBuilder.Sql("UPDATE StockMains SET Status = 'Approved' WHERE Status = 'Completed';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMains_Status_Valid",
                table: "StockMains",
                sql: "[Status] IN ('Draft','Approved','Void')");
        }
    }
}
