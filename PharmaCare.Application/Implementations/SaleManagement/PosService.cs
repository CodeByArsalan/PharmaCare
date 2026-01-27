using PharmaCare.Application.DTOs.POS;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces.Inventory;
using InventorySaleLineDto = PharmaCare.Infrastructure.Interfaces.Inventory.SaleLineDto;
using PosSaleLineDto = PharmaCare.Application.DTOs.POS.SaleLineDto;

namespace PharmaCare.Application.Implementations.SaleManagement;

public class PosService(
    IPosRepository _posRepository,
    IAccountingService _accountingService,
    PharmaCare.Domain.Interfaces.IStoreContext _storeContext,
    IInventoryAccountingService _inventoryAccountingService) : IPosService
{
    public async Task<List<ProductSearchResultDto>> SearchProductsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<ProductSearchResultDto>();

        // Get Domain entities from repository
        var products = await _posRepository.SearchProductsAsync(query);

        // Map Domain entities to DTOs in Application layer
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
                    Price = b.SellingPrice // Use actual selling price from batch
                })
                .ToList()
        }).ToList();
    }

    public async Task<CartItemDetailDto> GetBatchDetailsAsync(int productBatchId)
    {
        // Get Domain entity from repository
        var batch = await _posRepository.GetBatchDetailsAsync(productBatchId);
        if (batch == null)
            throw new InvalidOperationException("Batch not found");

        // Get stock quantity
        var stockQuantity = batch.TotalQuantityOnHand;

        // Map Domain entity to DTO in Application layer
        return new CartItemDetailDto
        {
            ProductBatchID = batch.ProductBatchID,
            ProductID = batch.Product_ID ?? 0,
            BatchNumber = batch.BatchNumber,
            ExpiryDate = batch.ExpiryDate,
            Price = batch.SellingPrice,
            AvailableQuantity = stockQuantity,
            ProductName = batch.Product.ProductName,
            ProductCode = batch.Product.ProductCode
        };
    }

    public async Task<int> ProcessCheckoutAsync(CheckoutDto checkoutData, int userId)
    {
        if (checkoutData.Items == null || !checkoutData.Items.Any())
        {
            throw new InvalidOperationException("Cart is empty");
        }

        // 1. Validate Stock for all items again
        foreach (var item in checkoutData.Items)
        {
            var stock = await _posRepository.GetStockQuantityAsync(item.ProductBatchID);
            if (stock < item.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock for {item.ProductName}");
            }
        }

        // 2. Handle Party (Customer)
        int? partyId = checkoutData.CustomerID;

        if (!partyId.HasValue && !string.IsNullOrWhiteSpace(checkoutData.CustomerName))
        {
            Party? party = null;

            // Only search by phone if a phone number is provided
            if (!string.IsNullOrWhiteSpace(checkoutData.CustomerPhone))
            {
                party = await _posRepository.GetPartyByPhoneAsync(checkoutData.CustomerPhone);
            }

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
                await _posRepository.SaveChangesAsync(); // Save to get ID
            }
            partyId = party.PartyID;
        }
        // If partyId is still null, it's a walk-in (or anonymous) sale

        // 3. Create Sale
        // Generate sale number: S-[TIMESTAMP]
        var saleNumber = UniqueIdGenerator.Generate("S");

        // Determine Store ID (prioritize checkout data, then context)
        var storeId = checkoutData.StoreID ?? _storeContext.CurrentStoreId ?? 1;

        // Calculate financial amounts
        var subTotal = checkoutData.Items.Sum(i => i.Subtotal); // Sum of (Qty * Price) for each item

        // Use invoice-level discount from checkout data
        var discountPercent = checkoutData.DiscountPercent;
        var discountAmount = checkoutData.DiscountAmount;

        // If discount amount not provided but percentage is, calculate it
        if (discountAmount <= 0 && discountPercent > 0)
        {
            discountAmount = (subTotal * discountPercent) / 100;
        }

        var total = subTotal - discountAmount;
        var amountPaid = checkoutData.Payments?.Sum(p => p.Amount) ?? 0;
        var balanceAmount = total - amountPaid;

        // Determine payment status
        string paymentStatus;
        if (balanceAmount <= 0)
        {
            paymentStatus = "Paid";
            balanceAmount = 0; // Prevent negative balance
        }
        else if (amountPaid > 0)
        {
            paymentStatus = "Partial";
        }
        else
        {
            paymentStatus = "Credit";
        }

        var sale = new Sale
        {
            SaleNumber = saleNumber,
            SaleDate = DateTime.Now,
            Store_ID = storeId,
            Party_ID = partyId,
            Prescription_ID = checkoutData.PrescriptionID,
            Status = "Completed",
            SubTotal = subTotal,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            Total = total,
            AmountPaid = amountPaid,
            BalanceAmount = balanceAmount,
            PaymentStatus = paymentStatus,
            CreatedBy = userId,
            CreatedDate = DateTime.Now,
            // Distribute invoice-level discount proportionally to each line item
            SaleLines = checkoutData.Items.Select(i =>
            {
                // Calculate this item's share of the invoice discount
                var lineGrossAmount = i.Subtotal; // Qty * UnitPrice
                var lineDiscountShare = subTotal > 0
                    ? (lineGrossAmount / subTotal) * discountAmount
                    : 0;
                var lineDiscountPercent = lineGrossAmount > 0
                    ? (lineDiscountShare / lineGrossAmount) * 100
                    : 0;

                return new SaleLine
                {
                    Product_ID = i.ProductID,
                    ProductBatch_ID = i.ProductBatchID,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    DiscountPercent = lineDiscountPercent,
                    DiscountAmount = lineDiscountShare,
                    NetAmount = lineGrossAmount - lineDiscountShare
                };
            }).ToList(),
            Payments = checkoutData.Payments?.Select(p => new Payment
            {
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                ReferenceNumber = p.ReferenceNumber
            }).ToList() ?? new List<Payment>()
        };

        _posRepository.AddSale(sale);
        await _posRepository.SaveChangesAsync(); // Save sale to get SaleID

        // 4. Process Inventory + Accounting via InventoryAccountingService
        // This atomically handles: StockMovement, StoreInventory, JournalEntry
        try
        {
            // Determine payment account (Cash or Bank)
            var cashAccount = await _accountingService.GetFirstAccountByTypeId(1);  // Cash
            var bankAccount = await _accountingService.GetFirstAccountByTypeId(2);  // Bank

            // Use cash as default payment account, or bank if only bank payments
            var primaryPaymentMethod = sale.Payments.FirstOrDefault()?.PaymentMethod ?? "Cash";
            var paymentAccountId = primaryPaymentMethod == "Cash"
                ? cashAccount?.AccountID ?? 0
                : bankAccount?.AccountID ?? 0;

            // Convert saved sale lines to SaleLineDtos for InventoryAccountingService
            // Using sale.SaleLines which have computed discounts applied
            var saleLines = sale.SaleLines.Select(sl => new InventorySaleLineDto
            {
                ProductBatchId = sl.ProductBatch_ID ?? 0,
                Quantity = sl.Quantity,
                UnitPrice = sl.UnitPrice,
                NetAmount = sl.NetAmount // Pass the discounted amount for revenue
            });

            var result = await _inventoryAccountingService.ProcessSaleAsync(
                storeId: storeId,
                saleId: sale.SaleID,
                saleDate: sale.SaleDate,
                saleLines: saleLines,
                paymentAccountId: paymentAccountId,
                userId: userId);

            if (result.Success)
            {
                // Link journal entry to sale
                sale.JournalEntry_ID = result.JournalEntryId;
                sale.AccountingError = null; // Clear any previous error
                await _posRepository.SaveChangesAsync();
            }
            else
            {
                // Save error to sale record for debugging
                sale.AccountingError = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {result.ErrorMessage}";
                await _posRepository.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"Inventory/Accounting Error for Sale {sale.SaleID}: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            // Capture full error details for debugging
            var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" | Inner: {ex.InnerException.Message}";
            }

            // Save error to sale record
            sale.AccountingError = errorMessage;
            await _posRepository.SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine($"Inventory/Accounting Integration Error for Sale {sale.SaleID}: {errorMessage}");
        }

        // Note: Customer/Party ledger tracking removed - journal entries now handle all financial tracking

        return sale.SaleID;
    }

    public async Task<ReceiptDto> GetReceiptAsync(int saleId)
    {
        var sale = await _posRepository.GetSaleWithDetailsAsync(saleId);
        if (sale == null) return null;

        return new ReceiptDto
        {
            SaleID = sale.SaleID,
            SaleNumber = sale.SaleNumber,
            SaleDate = sale.SaleDate,
            CustomerName = sale.Party?.PartyName,
            CustomerPhone = sale.Party?.ContactNumber,
            SubTotal = sale.SubTotal,
            DiscountAmount = sale.DiscountAmount,
            Total = sale.Total,
            Items = sale.SaleLines.Select(sl => new ReceiptItemDto
            {
                ProductName = sl.Product.ProductName,
                BatchNumber = sl.ProductBatch.BatchNumber,
                Quantity = sl.Quantity,
                UnitPrice = sl.UnitPrice,
                DiscountAmount = sl.DiscountAmount,
                Subtotal = sl.Quantity * sl.UnitPrice
            }).ToList(),
            Payments = sale.Payments.Select(p => new ReceiptPaymentDto
            {
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount
            }).ToList()
        };
    }


    public async Task<List<SaleHistoryDto>> GetSalesHistory(DateTime? startDate, DateTime? endDate, int? storeId = null)
    {
        var sales = await _posRepository.GetSalesHistoryAsync(startDate, endDate, storeId);

        return sales.Select(s => new SaleHistoryDto
        {
            SaleID = s.SaleID,
            SaleNumber = s.SaleNumber,
            SaleDate = s.SaleDate,
            CustomerName = s.Party?.PartyName ?? "Walk-in",
            CustomerPhone = s.Party?.ContactNumber ?? "N/A",
            TotalAmount = s.Total,
            PaymentMethods = string.Join(", ", s.Payments.Select(p => p.PaymentMethod).Distinct()),
            ItemCount = s.SaleLines.Count,
            Status = s.Status,
            PaymentStatus = s.PaymentStatus
        }).ToList();
    }

    public async Task<bool> VoidSaleAsync(int saleId, string reason, int userId)
    {
        var sale = await _posRepository.GetSaleWithDetailsAsync(saleId);
        if (sale == null)
            throw new InvalidOperationException("Sale not found");

        if (sale.Status == "Voided")
            throw new InvalidOperationException("Sale is already voided");

        // Update sale status
        sale.Status = "Voided";
        sale.VoidReason = reason;
        sale.VoidedBy = userId;
        sale.VoidedDate = DateTime.Now;

        // Void the journal entry if exists
        if (sale.JournalEntry_ID.HasValue)
        {
            await _accountingService.VoidJournalEntry(sale.JournalEntry_ID.Value, userId);
        }

        // Note: Inventory restoration would be handled by InventoryAccountingService
        // For now, we just mark the sale as voided

        await _posRepository.SaveChangesAsync();
        return true;
    }

    public async Task<SaleDetailDto?> GetSaleByIdAsync(int saleId)
    {
        var sale = await _posRepository.GetSaleWithDetailsAsync(saleId);
        if (sale == null) return null;

        return new SaleDetailDto
        {
            SaleID = sale.SaleID,
            SaleNumber = sale.SaleNumber,
            SaleDate = sale.SaleDate,
            CustomerName = sale.Party?.PartyName,
            CustomerPhone = sale.Party?.ContactNumber,
            CustomerID = sale.Party_ID,
            StoreName = sale.Store?.Name ?? "",
            Status = sale.Status,
            PaymentStatus = sale.PaymentStatus,
            SubTotal = sale.SubTotal,
            DiscountPercent = sale.DiscountPercent,
            DiscountAmount = sale.DiscountAmount,
            Total = sale.Total,
            AmountPaid = sale.AmountPaid,
            BalanceAmount = sale.BalanceAmount,
            Lines = sale.SaleLines.Select(sl => new PosSaleLineDto
            {
                SaleLineID = sl.SaleLineID,
                ProductID = sl.Product_ID ?? 0,
                ProductName = sl.Product?.ProductName ?? "",
                ProductBatchID = sl.ProductBatch_ID ?? 0,
                BatchNumber = sl.ProductBatch?.BatchNumber ?? "",
                Quantity = sl.Quantity,
                UnitPrice = sl.UnitPrice,
                DiscountPercent = sl.DiscountPercent,
                DiscountAmount = sl.DiscountAmount,
                NetAmount = sl.NetAmount
            }).ToList(),
            Payments = sale.Payments.Select(p => new PaymentDto
            {
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                ReferenceNumber = p.ReferenceNumber
            }).ToList()
        };
    }
}
