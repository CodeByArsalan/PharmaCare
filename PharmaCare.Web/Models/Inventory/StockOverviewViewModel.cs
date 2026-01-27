using PharmaCare.Application.DTOs.Inventory;
using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Web.Models.Inventory
{
    public class StockOverviewViewModel
    {
        public List<StoreInventory> StockItems { get; set; } = new();
        public InventorySummaryDto Summary { get; set; } = new();
        public int? SelectedStore_ID { get; set; }
    }
}
