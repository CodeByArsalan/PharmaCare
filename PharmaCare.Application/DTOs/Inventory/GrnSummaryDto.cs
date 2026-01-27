namespace PharmaCare.Application.DTOs.Inventory
{
    public class GrnSummaryDto
    {
        public int TotalGrnsToday { get; set; }
        public int TotalGrnsThisMonth { get; set; }
        public decimal TotalValueThisMonth { get; set; }
        public int PendingPOs { get; set; }
    }
}
