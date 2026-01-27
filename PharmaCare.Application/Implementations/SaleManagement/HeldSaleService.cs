using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.POS;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.SaleManagement;

public class HeldSaleService : IHeldSaleService
{
    private readonly IRepository<HeldSale> _heldSaleRepo;
    private readonly IPosRepository _posRepository;

    public HeldSaleService(
        IRepository<HeldSale> heldSaleRepo,
        IPosRepository posRepository)
    {
        _heldSaleRepo = heldSaleRepo;
        _posRepository = posRepository;
    }

    public async Task<int> HoldSale(HeldSale heldSale, int userId)
    {
        heldSale.HoldNumber = await GenerateHoldNumber();
        heldSale.HoldDate = DateTime.Now;
        heldSale.ExpiryDate = DateTime.Now.AddDays(7); // Expire after 7 days
        heldSale.CreatedBy = userId;
        heldSale.CreatedDate = DateTime.Now;
        heldSale.IsActive = true;

        await _heldSaleRepo.Insert(heldSale);
        return heldSale.HeldSaleID;
    }

    public async Task<List<HeldSale>> GetHeldSales(int storeId)
    {
        return await _heldSaleRepo.FindByCondition(h => h.Store_ID == storeId && h.IsActive)
            .Include(h => h.Party)
            .Include(h => h.HeldLines)
                .ThenInclude(l => l.Product)
            .OrderByDescending(h => h.HoldDate)
            .ToListAsync();
    }

    public async Task<HeldSale?> GetHeldSaleById(int id)
    {
        return await _heldSaleRepo.FindByCondition(h => h.HeldSaleID == id)
            .Include(h => h.Party)
            .Include(h => h.HeldLines)
                .ThenInclude(l => l.Product)
            .Include(h => h.HeldLines)
                .ThenInclude(l => l.ProductBatch)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CartItemDto>> ResumeHeldSale(int heldSaleId)
    {
        var heldSale = await GetHeldSaleById(heldSaleId);
        if (heldSale == null)
            throw new InvalidOperationException("Held sale not found");

        var cartItems = new List<CartItemDto>();

        foreach (var line in heldSale.HeldLines)
        {
            // Verify stock is still available
            if (line.ProductBatch_ID.HasValue)
            {
                var batch = await _posRepository.GetBatchDetailsAsync(line.ProductBatch_ID.Value);
                if (batch != null && batch.TotalQuantityOnHand >= line.Quantity)
                {
                    cartItems.Add(new CartItemDto
                    {
                        ProductID = line.Product_ID ?? 0,
                        ProductBatchID = line.ProductBatch_ID ?? 0,
                        ProductName = line.Product?.ProductName ?? "",
                        BatchNumber = batch.BatchNumber,
                        ExpiryDate = batch.ExpiryDate,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        DiscountPercent = line.DiscountPercent,
                        DiscountAmount = line.DiscountAmount,
                        Subtotal = (line.Quantity * line.UnitPrice) - line.DiscountAmount,
                        AvailableQuantity = batch.TotalQuantityOnHand
                    });
                }
            }
        }

        // Mark as retrieved (soft delete)
        heldSale.IsActive = false;
        await _heldSaleRepo.Update(heldSale);

        return cartItems;
    }

    public async Task<bool> DeleteHeldSale(int heldSaleId, int userId)
    {
        var heldSale = await _heldSaleRepo.FindByCondition(h => h.HeldSaleID == heldSaleId)
            .FirstOrDefaultAsync();

        if (heldSale == null)
            return false;

        heldSale.IsActive = false;
        heldSale.UpdatedBy = userId;
        heldSale.UpdatedDate = DateTime.Now;

        return await _heldSaleRepo.Update(heldSale);
    }

    public Task<string> GenerateHoldNumber()
    {
        return Task.FromResult(UniqueIdGenerator.Generate("HS"));
    }

    public async Task<int> CleanupExpiredHolds()
    {
        var expiredHolds = await _heldSaleRepo
            .FindByCondition(h => h.IsActive && h.ExpiryDate < DateTime.Now)
            .ToListAsync();

        int count = 0;
        foreach (var hold in expiredHolds)
        {
            hold.IsActive = false;
            await _heldSaleRepo.Update(hold);
            count++;
        }

        return count;
    }
}
