namespace PharmaCare.Application.DTOs.Inventory
{
    public class InventorySummaryDto
    {
        public int TotalProducts { get; set; }
        public int LowStockItems { get; set; }
        public int ExpiredItems { get; set; }
        public int ExpiringSoonItems { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public int PendingPurchaseOrders { get; set; }
    }
}
