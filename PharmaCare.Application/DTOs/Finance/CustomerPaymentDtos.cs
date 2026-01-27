namespace PharmaCare.Application.DTOs.Finance;

/// <summary>
/// DTO for listing outstanding sales (sales with balance due)
/// </summary>
public class OutstandingSaleDto
{
    public int SaleID { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int CustomerID { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating a customer payment
/// </summary>
public class CreateCustomerPaymentDto
{
    public int? CustomerID { get; set; }
    public int? SaleID { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for listing customer payments
/// </summary>
public class CustomerPaymentListDto
{
    public int CustomerPaymentID { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int CustomerID { get; set; }
    public string? SaleNumber { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO for customer summary with outstanding balance
/// </summary>
public class CustomerOutstandingDto
{
    public int CustomerID { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int OutstandingSalesCount { get; set; }
    public decimal TotalOutstanding { get; set; }
}
