using PharmaCare.Application.DTOs.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;

namespace PharmaCare.Web.Models.Inventory
{
    public class GrnIndexViewModel
    {
        public List<Grn> Grns { get; set; } = new();
        public GrnSummaryDto Summary { get; set; } = new();
    }
}
