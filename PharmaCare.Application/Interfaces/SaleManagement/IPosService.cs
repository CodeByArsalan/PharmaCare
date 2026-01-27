using PharmaCare.Application.DTOs.POS;

namespace PharmaCare.Application.Interfaces.SaleManagement;

public interface IPosService
{
    Task<List<ProductSearchResultDto>> SearchProductsAsync(string query);
    Task<CartItemDetailDto> GetBatchDetailsAsync(int productBatchId);
    Task<int> ProcessCheckoutAsync(CheckoutDto checkoutData, int userId);
    Task<ReceiptDto> GetReceiptAsync(int saleId);
    Task<List<SaleHistoryDto>> GetSalesHistory(DateTime? startDate, DateTime? endDate, int? storeId = null);

    /// <summary>
    /// Void a completed sale with reason
    /// </summary>
    Task<bool> VoidSaleAsync(int saleId, string reason, int userId);

    /// <summary>
    /// Get sale by ID with all details
    /// </summary>
    Task<SaleDetailDto?> GetSaleByIdAsync(int saleId);
}
