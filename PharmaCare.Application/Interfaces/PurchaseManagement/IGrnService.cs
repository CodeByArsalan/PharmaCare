using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;

namespace PharmaCare.Application.Interfaces.PurchaseManagement;

public interface IGrnService
{
    Task<List<Grn>> GetGrns();
    Task<Grn> GetGrnById(int id);
    Task<bool> CreateGrn(Grn grn, int loginUserId);
    Task<PharmaCare.Application.DTOs.Inventory.GrnSummaryDto> GetGrnSummary();
}
