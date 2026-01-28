using PharmaCare.Application.DTOs.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;

namespace PharmaCare.Web.Models.Inventory
{
    public class PurchaseIndexViewModel
    {
        public List<StockMain> Purchases { get; set; } = new();
        public PurchaseSummaryDto Summary { get; set; } = new();
    }

    public class AddPurchaseViewModel
    {
        public int StoreId { get; set; }
        public int? PartyId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string? SupplierInvoiceNo { get; set; }
        public string? Remarks { get; set; }
        public List<AddPurchaseItemViewModel> Items { get; set; } = new();
        
        // For display
        public PurchaseOrder? PurchaseOrder { get; set; }
    }

    public class AddPurchaseItemViewModel
    {
        public int ProductId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
    }
}
