using PharmaCare.Domain.Models.SaleManagement;

namespace PharmaCare.Application.DTOs.POS;

public class ProductSearchResultDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    public string ProductCode { get; set; }
    public string Barcode { get; set; }
    public List<BatchInfoDto> AvailableBatches { get; set; } = new();
}

public class BatchInfoDto
{
    public int ProductBatchID { get; set; }
    public string BatchNumber { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal Price { get; set; }
}

public class CartItemDetailDto : BatchInfoDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    public string ProductCode { get; set; }
}

public class CheckoutDto
{
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public int? CustomerID { get; set; }
    public int? StoreID { get; set; }
    public int? PrescriptionID { get; set; }
    public List<CartItemDto> Items { get; set; }
    public List<PaymentDto> Payments { get; set; }

    // Invoice-level discount
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
}


public class CartItemDto
{
    public int ProductID { get; set; }
    public int ProductBatchID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal Subtotal { get; set; }
}

public class PaymentDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class ReceiptDto
{
    public int SaleID { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public List<ReceiptItemDto> Items { get; set; } = new();
    public List<ReceiptPaymentDto> Payments { get; set; } = new();
}

public class ReceiptItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
}

public class ReceiptPaymentDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class AddToCartDto
{
    public int ProductBatchID { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal DiscountPercent { get; set; }
}

public class SaleHistoryDto
{
    public int SaleID { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string CustomerName { get; set; } = "Walk-in";
    public string CustomerPhone { get; set; } = "N/A";
    public decimal TotalAmount { get; set; }
    public string PaymentMethods { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string Status { get; set; } = "Completed";
    public string PaymentStatus { get; set; } = "Paid";
}

public class SaleDetailDto
{
    public int SaleID { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int? CustomerID { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public string PaymentStatus { get; set; } = "Paid";
    public decimal SubTotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceAmount { get; set; }
    public List<SaleLineDto> Lines { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}

public class SaleLineDto
{
    public int SaleLineID { get; set; }
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int ProductBatchID { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
}

