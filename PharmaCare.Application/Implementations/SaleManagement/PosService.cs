using PharmaCare.Application.DTOs.POS;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Application.Implementations.SaleManagement;

/// <summary>
/// POS Service - Refactored to use unified StockMain/StockDetail tables
/// </summary>
public class PosService : IPosService
{
    private readonly IPosRepository _posRepository;
    private readonly IStockTransactionService _stockTransactionService;
    private readonly PharmaCare.Domain.Interfaces.IStoreContext _storeContext;
    private readonly IRepository<StockMain> _stockMainRepo;
    private readonly IRepository<Party> _partyRepo;

    public PosService(
        IPosRepository posRepository,
        IStockTransactionService stockTransactionService,
        PharmaCare.Domain.Interfaces.IStoreContext storeContext,
        IRepository<StockMain> stockMainRepo,
        IRepository<Party> partyRepo)
    {
        _posRepository = posRepository;
        _stockTransactionService = stockTransactionService;
        _storeContext = storeContext;
        _stockMainRepo = stockMainRepo;
        _partyRepo = partyRepo;
    }

    public async Task<List<ProductSearchResultDto>> SearchProductsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<ProductSearchResultDto>();

        var products = await _posRepository.SearchProductsAsync(query);

        return products.Select(p => new ProductSearchResultDto
        {
            ProductID = p.ProductID,
            ProductName = p.ProductName,
            ProductCode = p.ProductCode ?? p.Sku,
            Barcode = p.Barcode,
            AvailableBatches = p.ProductBatches
                .Where(b => b.ExpiryDate > DateTime.Now)
                .Select(b => new BatchInfoDto
                {
                    ProductBatchID = b.ProductBatchID,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    AvailableQuantity = b.TotalQuantityOnHand,
                    Price = b.SellingPrice
                })
                .ToList()
        }).ToList();
    }

    public async Task<CartItemDetailDto> GetBatchDetailsAsync(int productBatchId)
    {
        var batch = await _posRepository.GetBatchDetailsAsync(productBatchId);
        if (batch == null)
            throw new InvalidOperationException("Batch not found");

        return new CartItemDetailDto
        {
            ProductBatchID = batch.ProductBatchID,
            ProductID = batch.Product_ID ?? 0,
            BatchNumber = batch.BatchNumber,
            ExpiryDate = batch.ExpiryDate,
            Price = batch.SellingPrice,
            AvailableQuantity = batch.TotalQuantityOnHand,
            ProductName = batch.Product.ProductName,
            ProductCode = batch.Product.ProductCode
        };
    }

    public async Task<int> ProcessCheckoutAsync(CheckoutDto checkoutData, int userId)
    {
        if (checkoutData.Items == null || !checkoutData.Items.Any())
            throw new InvalidOperationException("Cart is empty");

        // 1. Validate Stock
        foreach (var item in checkoutData.Items)
        {
            var stock = await _posRepository.GetStockQuantityAsync(item.ProductBatchID);
            if (stock < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for {item.ProductName}");
        }

        // 2. Handle Customer/Party
        int? partyId = checkoutData.CustomerID;
        if (!partyId.HasValue && !string.IsNullOrWhiteSpace(checkoutData.CustomerName))
        {
            Party? party = null;
            if (!string.IsNullOrWhiteSpace(checkoutData.CustomerPhone))
                party = await _posRepository.GetPartyByPhoneAsync(checkoutData.CustomerPhone);

            if (party == null)
            {
                party = new Party
                {
                    PartyName = checkoutData.CustomerName,
                    ContactNumber = checkoutData.CustomerPhone ?? "",
                    PartyType = "Customer",
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                _posRepository.AddParty(party);
                await _posRepository.SaveChangesAsync();
            }
            partyId = party.PartyID;
        }

        // 3. Calculate financials
        var storeId = checkoutData.StoreID ?? _storeContext.CurrentStoreId ?? 1;
        var subTotal = checkoutData.Items.Sum(i => i.Subtotal);
        var discountPercent = checkoutData.DiscountPercent;
        var discountAmount = checkoutData.DiscountAmount;

        if (discountAmount <= 0 && discountPercent > 0)
            discountAmount = (subTotal * discountPercent) / 100;

        var total = subTotal - discountAmount;
        var amountPaid = checkoutData.Payments?.Sum(p => p.Amount) ?? 0;

        // 4. Create transaction via unified service (InvoiceType=1 for SALE)
        var request = new CreateTransactionRequest
        {
            InvoiceTypeId = 1, // SALE
            StoreId = storeId,
            PartyId = partyId,
            InvoiceDate = DateTime.Now,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PaidAmount = amountPaid,
            CreatedBy = userId,
            Lines = checkoutData.Items.Select(i =>
            {
                var lineGross = i.Subtotal;
                var lineDiscountShare = subTotal > 0 ? (lineGross / subTotal) * discountAmount : 0;

                return new CreateTransactionLineRequest
                {
                    ProductId = i.ProductID,
                    ProductBatchId = i.ProductBatchID,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    DiscountAmount = lineDiscountShare
                };
            }).ToList()
        };

        var transaction = await _stockTransactionService.CreateTransactionAsync(request);

        // 5. Update status to Completed and generate accounting entries
        await _stockTransactionService.UpdateStatusAsync(transaction.StockMainID, "Completed");
        await _stockTransactionService.GenerateAccountingEntriesAsync(transaction.StockMainID);

        return transaction.StockMainID;
    }

    public async Task<ReceiptDto> GetReceiptAsync(int saleId)
    {
        var transaction = await _stockTransactionService.GetTransactionAsync(saleId);
        if (transaction == null) return null!;

        return new ReceiptDto
        {
            SaleID = transaction.StockMainID,
            SaleNumber = transaction.InvoiceNo,
            SaleDate = transaction.InvoiceDate,
            CustomerName = transaction.Party?.PartyName,
            CustomerPhone = transaction.Party?.ContactNumber,
            SubTotal = transaction.SubTotal,
            DiscountAmount = transaction.DiscountAmount,
            Total = transaction.TotalAmount,
            Items = transaction.StockDetails.Select(d => new ReceiptItemDto
            {
                ProductName = d.Product?.ProductName ?? "",
                BatchNumber = d.ProductBatch?.BatchNumber ?? "",
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                DiscountAmount = d.DiscountAmount,
                Subtotal = d.Quantity * d.UnitPrice
            }).ToList(),
            Payments = new List<ReceiptPaymentDto>() // Payments handled differently now
        };
    }

    public async Task<List<SaleHistoryDto>> GetSalesHistory(DateTime? startDate, DateTime? endDate, int? storeId = null)
    {
        var transactions = await _stockTransactionService.GetTransactionsByTypeAsync(1, startDate, endDate);

        if (storeId.HasValue)
            transactions = transactions.Where(t => t.Store_ID == storeId.Value);

        return transactions.Select(t => new SaleHistoryDto
        {
            SaleID = t.StockMainID,
            SaleNumber = t.InvoiceNo,
            SaleDate = t.InvoiceDate,
            CustomerName = t.Party?.PartyName ?? "Walk-in",
            CustomerPhone = t.Party?.ContactNumber ?? "N/A",
            TotalAmount = t.TotalAmount,
            PaymentMethods = t.PaymentStatus ?? "",
            ItemCount = t.StockDetails?.Count ?? 0,
            Status = t.Status ?? "Completed",
            PaymentStatus = t.PaymentStatus ?? "Unpaid"
        }).ToList();
    }

    public async Task<bool> VoidSaleAsync(int saleId, string reason, int userId)
    {
        await _stockTransactionService.VoidTransactionAsync(saleId, reason, userId);
        return true;
    }

    public async Task<SaleDetailDto?> GetSaleByIdAsync(int saleId)
    {
        var transaction = await _stockTransactionService.GetTransactionAsync(saleId);
        if (transaction == null) return null;

        return new SaleDetailDto
        {
            SaleID = transaction.StockMainID,
            SaleNumber = transaction.InvoiceNo,
            SaleDate = transaction.InvoiceDate,
            CustomerName = transaction.Party?.PartyName,
            CustomerPhone = transaction.Party?.ContactNumber,
            CustomerID = transaction.Party_ID,
            StoreName = transaction.Store?.Name ?? "",
            Status = transaction.Status ?? "Completed",
            PaymentStatus = transaction.PaymentStatus ?? "Unpaid",
            SubTotal = transaction.SubTotal,
            DiscountPercent = transaction.DiscountPercent ?? 0,
            DiscountAmount = transaction.DiscountAmount,
            Total = transaction.TotalAmount,
            AmountPaid = transaction.PaidAmount,
            BalanceAmount = transaction.BalanceAmount,
            Lines = transaction.StockDetails.Select(d => new DTOs.POS.SaleLineDto
            {
                SaleLineID = d.StockDetailID,
                ProductID = d.Product_ID,
                ProductName = d.Product?.ProductName ?? "",
                ProductBatchID = d.ProductBatch_ID ?? 0,
                BatchNumber = d.ProductBatch?.BatchNumber ?? "",
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                DiscountPercent = d.DiscountPercent ?? 0,
                DiscountAmount = d.DiscountAmount,
                NetAmount = d.LineTotal
            }).ToList(),
            Payments = new List<PaymentDto>() // Payments now handled via vouchers
        };
    }
}
