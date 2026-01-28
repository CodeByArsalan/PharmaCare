namespace PharmaCare.Application.DTOs.Inventory
{
    public class PurchaseSummaryDto
    {
        public int TotalPurchasesToday { get; set; }
        public int TotalPurchasesThisMonth { get; set; }
        public decimal TotalValueThisMonth { get; set; }
        public int PendingPOs { get; set; }
    }
}
