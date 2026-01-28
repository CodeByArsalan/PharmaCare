using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.SaleManagement;

public class QuotationService : IQuotationService
{
    private readonly IRepository<Quotation> _quotationRepo;
    private readonly IStockTransactionService _stockTransactionService;

    public QuotationService(
        IRepository<Quotation> quotationRepo,
        IStockTransactionService stockTransactionService)
    {
        _quotationRepo = quotationRepo;
        _stockTransactionService = stockTransactionService;
    }

    public async Task<int> CreateQuotation(Quotation quotation, int userId)
    {
        quotation.QuotationNumber = await GenerateQuotationNumber();
        quotation.QuotationDate = DateTime.Now;
        quotation.ValidUntil = DateTime.Now.AddDays(30); // Valid for 30 days
        quotation.Status = "Draft";
        quotation.CreatedBy = userId;
        quotation.CreatedDate = DateTime.Now;

        // Calculate totals
        quotation.SubTotal = quotation.QuotationLines.Sum(l => l.Quantity * l.UnitPrice);
        quotation.DiscountAmount = quotation.QuotationLines.Sum(l => l.DiscountAmount);
        quotation.GrandTotal = quotation.SubTotal - quotation.DiscountAmount;

        // Calculate line net amounts
        foreach (var line in quotation.QuotationLines)
        {
            line.NetAmount = (line.Quantity * line.UnitPrice) - line.DiscountAmount;
        }

        await _quotationRepo.Insert(quotation);
        return quotation.QuotationID;
    }

    public async Task<Quotation?> GetQuotationById(int id)
    {
        return await _quotationRepo.FindByCondition(q => q.QuotationID == id)
            .Include(q => q.Party)
            .Include(q => q.Store)
            .Include(q => q.QuotationLines)
                .ThenInclude(l => l.Product)
            .Include(q => q.QuotationLines)
                .ThenInclude(l => l.ProductBatch)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Quotation>> GetQuotations(int? storeId = null, string? status = null)
    {
        var query = _quotationRepo.FindByCondition(q => q.Status != "Cancelled");

        if (storeId.HasValue)
            query = query.Where(q => q.Store_ID == storeId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(q => q.Status == status);

        return await query
            .Include(q => q.Party)
            .OrderByDescending(q => q.QuotationDate)
            .ToListAsync();
    }

    public async Task<int> ConvertToSale(int quotationId, int userId)
    {
        var quotation = await GetQuotationById(quotationId);
        if (quotation == null)
            throw new InvalidOperationException("Quotation not found");

        if (quotation.Status == "Converted")
            throw new InvalidOperationException("Quotation is already converted");

        if (quotation.Status == "Expired" || quotation.ValidUntil < DateTime.Now)
            throw new InvalidOperationException("Quotation has expired");

        // Create sale via IStockTransactionService (InvoiceType=1 for SALE)
        var lines = quotation.QuotationLines.Select(ql => new CreateTransactionLineRequest
        {
            ProductId = ql.Product_ID ?? 0,
            ProductBatchId = ql.ProductBatch_ID,
            Quantity = ql.Quantity,
            UnitPrice = ql.UnitPrice,
            DiscountPercent = ql.DiscountPercent,
            DiscountAmount = ql.DiscountAmount
        }).ToList();

        var request = new CreateTransactionRequest
        {
            InvoiceTypeId = 1, // SALE
            InvoiceDate = DateTime.Now,
            StoreId = quotation.Store_ID,
            PartyId = quotation.Party_ID,
            Remarks = $"Converted from Quotation {quotation.QuotationNumber}",
            CreatedBy = userId,
            Lines = lines,
            DiscountAmount = quotation.DiscountAmount
        };

        var sale = await _stockTransactionService.CreateTransactionAsync(request);
        if (sale == null)
            throw new InvalidOperationException("Failed to create sale from quotation");

        // Complete the sale transaction to update inventory
        await _stockTransactionService.CompleteTransactionAsync(sale.StockMainID, userId);

        // Update quotation status
        quotation.Status = "Converted";
        quotation.ConvertedStockMain_ID = sale.StockMainID;
        quotation.UpdatedBy = userId;
        quotation.UpdatedDate = DateTime.Now;
        await _quotationRepo.Update(quotation);

        return sale.StockMainID;
    }

    public async Task<bool> UpdateQuotation(Quotation quotation, int userId)
    {
        var existing = await _quotationRepo.FindByCondition(q => q.QuotationID == quotation.QuotationID)
            .FirstOrDefaultAsync();

        if (existing == null)
            return false;

        if (existing.Status == "Converted")
            throw new InvalidOperationException("Cannot update a converted quotation");

        existing.CustomerName = quotation.CustomerName;
        existing.CustomerPhone = quotation.CustomerPhone;
        existing.Party_ID = quotation.Party_ID;
        existing.ValidUntil = quotation.ValidUntil;
        existing.Notes = quotation.Notes;
        existing.SubTotal = quotation.SubTotal;
        existing.DiscountPercent = quotation.DiscountPercent;
        existing.DiscountAmount = quotation.DiscountAmount;
        existing.GrandTotal = quotation.GrandTotal;
        existing.UpdatedBy = userId;
        existing.UpdatedDate = DateTime.Now;

        return await _quotationRepo.Update(existing);
    }

    public async Task<bool> CancelQuotation(int quotationId, int userId)
    {
        var quotation = await _quotationRepo.FindByCondition(q => q.QuotationID == quotationId)
            .FirstOrDefaultAsync();

        if (quotation == null)
            return false;

        if (quotation.Status == "Converted")
            throw new InvalidOperationException("Cannot cancel a converted quotation");

        quotation.Status = "Cancelled";
        quotation.UpdatedBy = userId;
        quotation.UpdatedDate = DateTime.Now;

        return await _quotationRepo.Update(quotation);
    }

    public Task<string> GenerateQuotationNumber()
    {
        return Task.FromResult(UniqueIdGenerator.Generate("Q"));
    }

    public async Task<int> MarkExpiredQuotations()
    {
        var expiredQuotes = await _quotationRepo
            .FindByCondition(q => q.Status == "Draft" && q.ValidUntil < DateTime.Now)
            .ToListAsync();

        int count = 0;
        foreach (var quote in expiredQuotes)
        {
            quote.Status = "Expired";
            await _quotationRepo.Update(quote);
            count++;
        }

        return count;
    }
}
