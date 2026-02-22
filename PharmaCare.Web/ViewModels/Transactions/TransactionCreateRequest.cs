using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Web.ViewModels.Transactions;

public class StockDetailCreateRequest
{
    [Range(1, int.MaxValue)]
    public int Product_ID { get; set; }

    [Range(typeof(decimal), "0.0001", "79228162514264337593543950335")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal CostPrice { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal DiscountPercent { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal DiscountAmount { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal LineTotal { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal LineCost { get; set; }

    [StringLength(200)]
    public string? Remarks { get; set; }
}

public class SaleCreateRequest
{
    [DataType(DataType.Date)]
    public DateTime TransactionDate { get; set; } = DateTime.Now;

    public int? Party_ID { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal DiscountPercent { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal PaidAmount { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [MinLength(1)]
    public List<StockDetailCreateRequest> StockDetails { get; set; } = new();
}

public class PurchaseCreateRequest
{
    [DataType(DataType.Date)]
    public DateTime TransactionDate { get; set; } = DateTime.Now;

    public int? Party_ID { get; set; }

    public int? ReferenceStockMain_ID { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal DiscountPercent { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal PaidAmount { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [MinLength(1)]
    public List<StockDetailCreateRequest> StockDetails { get; set; } = new();
}
