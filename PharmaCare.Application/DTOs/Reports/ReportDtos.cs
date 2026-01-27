namespace PharmaCare.Application.DTOs.Reports;

public class SalesReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public List<DailySalesDto> DailySales { get; set; } = new();
    public List<PaymentMethodBreakdownDto> PaymentMethodBreakdown { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
}

public class DailySalesDto
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}

public class PaymentMethodBreakdownDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}

public class TopProductDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class InventoryReportDto
{
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public decimal TotalStockValue { get; set; }
    public List<StockLevelDto> StockLevels { get; set; } = new();
    public List<LowStockItemDto> LowStockItems { get; set; } = new();
    public List<ExpiringItemDto> ExpiringItems { get; set; } = new();
}

public class StockLevelDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
}

public class LowStockItemDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
}

public class ExpiringItemDto
{
    public int ProductBatchID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal QuantityOnHand { get; set; }
    public int DaysUntilExpiry { get; set; }
}

public class SalesDetailDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AveragePrice { get; set; }
    public int TransactionCount { get; set; }
}

public class StockMovementDto
{
    public DateTime MovementDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Reference { get; set; } = string.Empty;
}

// ===== NEW REPORT DTOs =====

public class SlowMovingItemDto
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal QuantitySoldLast30Days { get; set; }
    public decimal QuantitySoldLast90Days { get; set; }
    public decimal DaysOfStock { get; set; }
    public decimal StockValue { get; set; }
}

public class PurchaseReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalPurchases { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalItemsReceived { get; set; }
    public List<SupplierPurchaseDto> BySupplier { get; set; } = new();
    public List<DailyPurchaseDto> DailyPurchases { get; set; } = new();
}

public class SupplierPurchaseDto
{
    public int SupplierID { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int OrderCount { get; set; }
}

public class DailyPurchaseDto
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public int OrderCount { get; set; }
}

public class ProfitLossDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitMargin { get; set; }
    public List<CategoryProfitDto> ByCategory { get; set; } = new();
    public List<DailyProfitDto> DailyProfit { get; set; } = new();
}

public class CategoryProfitDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal Margin { get; set; }
}

public class DailyProfitDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
}

public class CustomerAnalyticsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalCustomers { get; set; }
    public int NewCustomersThisPeriod { get; set; }
    public int ActiveCustomers { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal TotalCustomerRevenue { get; set; }
    public List<TopCustomerDto> TopCustomers { get; set; } = new();
    public List<CustomerSegmentDto> Segments { get; set; } = new();
}

public class TopCustomerDto
{
    public int CustomerID { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int OrderCount { get; set; }
}

public class CustomerSegmentDto
{
    public string Segment { get; set; } = string.Empty; // High, Medium, Low
    public int CustomerCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class ExpiryWastageReportDto
{
    public decimal TotalExpiredValue { get; set; }
    public int ExpiredItemCount { get; set; }
    public decimal NearExpiryValue { get; set; }
    public int NearExpiryCount { get; set; }
    public List<ExpiredItemDetailDto> ExpiredItems { get; set; } = new();
    public List<NearExpiryItemDto> NearExpiryItems30Days { get; set; } = new();
    public List<NearExpiryItemDto> NearExpiryItems60Days { get; set; } = new();
    public List<NearExpiryItemDto> NearExpiryItems90Days { get; set; } = new();
}

public class ExpiredItemDetailDto
{
    public int ProductBatchID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public int DaysExpired { get; set; }
}

public class NearExpiryItemDto
{
    public int ProductBatchID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public int DaysUntilExpiry { get; set; }
}
