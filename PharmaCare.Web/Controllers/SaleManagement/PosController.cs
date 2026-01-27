using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.DTOs.POS;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Web.Extensions;
using PharmaCare.Web.Utilities;
using PharmaCare.Web.Models.POS;
using System.Security.Claims;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Web.Controllers.SaleManagement;

[Authorize]
public class PosController(IPosService _posService, IComboBoxRepository _comboBox) : BaseController
{
    private const string CartSessionKey = "POSCart";

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult NewSale()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SearchProducts(string query)
    {
        var products = await _posService.SearchProductsAsync(query);
        return Json(products);
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(string query)
    {
        // Returns customers (Party with PartyType = Customer or Both) for search
        var customers = await _comboBox.GetCustomersAsync();
        var results = customers.Cast<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>()
            .Where(c => string.IsNullOrWhiteSpace(query) || c.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(c => new { id = c.Value, text = c.Text });
        return Json(results);
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
    {
        try
        {
            // Validate stock and get details
            var batchDetails = await _posService.GetBatchDetailsAsync(request.ProductBatchID);

            // Check stock again (redundant but safe) or rely on GetStock call if not cached
            // The service doesn't implicitly valid stock in GetBatchDetailsAsync, so we might want to check
            // However, the original logic checked stock before adding.
            // Let's stick to simple retrieval and session logic here.

            if (batchDetails.AvailableQuantity < request.Quantity)
                return Json(new { success = false, message = "Insufficient stock" });

            // Get or create cart
            var cart = GetCartFromSession();

            // Check if item already exists
            var existingItem = cart.FirstOrDefault(i =>
                i.ProductID == batchDetails.ProductID && i.ProductBatchID == batchDetails.ProductBatchID);

            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
            }
            else
            {
                cart.Add(new CartItemViewModel
                {
                    ProductID = batchDetails.ProductID,
                    ProductBatchID = batchDetails.ProductBatchID,

                    ProductName = batchDetails.ProductName,
                    BatchNumber = batchDetails.BatchNumber,
                    ExpiryDate = batchDetails.ExpiryDate,
                    Quantity = request.Quantity,
                    UnitPrice = request.Price // Use batch price
                });
            }

            SaveCart(cart);

            return Json(new
            {
                success = true,
                cartCount = cart.Count,
                cartTotal = cart.Sum(i => i.Subtotal)
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetCartData()
    {
        var cart = GetCartFromSession();
        return Json(new
        {
            items = cart,
            total = cart.Sum(i => i.Subtotal)
        });
    }

    [HttpPost]
    public IActionResult UpdateCartItem([FromBody] UpdateCartRequest request)
    {
        var cart = GetCartFromSession();
        var item = cart.FirstOrDefault(i => i.ProductBatchID == request.ProductBatchID);

        if (item != null)
        {
            item.Quantity = request.Quantity;
            if (item.Quantity <= 0)
                cart.Remove(item);

            SaveCart(cart);
        }

        return Json(new
        {
            success = true,
            cartTotal = cart.Sum(i => i.Subtotal)
        });
    }

    [HttpPost]
    public IActionResult RemoveFromCart(int productBatchID)
    {
        var cart = GetCartFromSession();
        var item = cart.FirstOrDefault(i => i.ProductBatchID == productBatchID);

        if (item != null)
        {
            cart.Remove(item);
            SaveCart(cart);
        }

        return Json(new
        {
            success = true,
            cartCount = cart.Count,
            cartTotal = cart.Sum(i => i.Subtotal)
        });
    }

    [HttpPost]
    public IActionResult ClearCart()
    {
        HttpContext.Session.Remove(CartSessionKey);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] CheckoutViewModel model)
    {
        var cart = GetCartFromSession();

        if (!cart.Any())
            return Json(new { success = false, message = "Cart is empty" });

        // Map ViewModel to DTO
        var checkoutDto = new CheckoutDto
        {
            CustomerName = model.CustomerName,
            CustomerPhone = model.CustomerPhone,
            CustomerID = model.CustomerID,
            StoreID = model.StoreID,
            PrescriptionID = model.PrescriptionID,
            DiscountPercent = model.DiscountPercent,
            DiscountAmount = model.DiscountAmount,
            Items = cart.Select(i => new CartItemDto
            {
                ProductID = i.ProductID,
                ProductBatchID = i.ProductBatchID,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal // Quantity * UnitPrice
            }).ToList(),
            Payments = model.Payments?.Select(p => new PaymentDto
            {
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount,
                ReferenceNumber = !string.IsNullOrWhiteSpace(p.ReferenceNumber) 
                    ? p.ReferenceNumber 
                    : (p.PaymentMethod == "Cash" ? "CASH" : null)
            }).ToList() ?? new List<PaymentDto>()
        };


        try
        {
            // Get current user ID
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "1");

            var saleId = await _posService.ProcessCheckoutAsync(checkoutDto, userId);

            // Clear cart
            HttpContext.Session.Remove(CartSessionKey);

            return Json(new { success = true, saleId = Utility.EncryptURL(saleId.ToString()) });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> Receipt(string id)
    {
        int decryptedId = DecryptId(id);
        var receipt = await _posService.GetReceiptAsync(decryptedId);
        if (receipt == null)
            return NotFound();

        // Map DTO to ViewModel
        var model = new ReceiptViewModel
        {
            SaleID = receipt.SaleID,
            SaleNumber = receipt.SaleNumber,
            SaleDate = receipt.SaleDate,
            CustomerName = receipt.CustomerName,
            CustomerPhone = receipt.CustomerPhone,
            SubTotal = receipt.SubTotal,
            DiscountAmount = receipt.DiscountAmount,
            DiscountPercent = receipt.SubTotal > 0 ? receipt.DiscountAmount / receipt.SubTotal * 100 : 0,
            Total = receipt.Total,
            Items = receipt.Items.Select(i => new ReceiptItemViewModel
            {
                ProductName = i.ProductName,
                BatchNumber = i.BatchNumber,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                Subtotal = i.Subtotal
            }).ToList(),
            Payments = receipt.Payments.Select(p => new ReceiptPaymentViewModel
            {
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount
            }).ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> SalesHistory(DateTime? startDate, DateTime? endDate)
    {
        // Default to last 30 days if no dates provided
        if (!startDate.HasValue && !endDate.HasValue)
        {
            endDate = DateTime.Now;
            startDate = DateTime.Now.AddDays(-30);
        }

        var sales = await _posService.GetSalesHistory(startDate, endDate);

        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

        return View(sales);
    }

    // POST: Pos/VoidSale
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoidSale(int saleId, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Json(new { success = false, message = "Void reason is required." });

            var result = await _posService.VoidSaleAsync(saleId, reason, LoginUserID);
            if (result)
            {
                return Json(new { success = true, message = "Sale voided successfully." });
            }
            return Json(new { success = false, message = "Failed to void sale." });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
        }
    }

    #region Helper Methods
    private List<CartItemViewModel> GetCartFromSession()
    {
        var cart = HttpContext.Session.GetObjectFromJson<List<CartItemViewModel>>(CartSessionKey);
        return cart ?? new List<CartItemViewModel>();
    }

    private void SaveCart(List<CartItemViewModel> cart)
    {
        HttpContext.Session.SetObjectAsJson(CartSessionKey, cart);
    }

    #endregion
}

// Request models - ideally move to a dedicated file if reused
public class AddToCartRequest
{
    public int ProductBatchID { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

public class UpdateCartRequest
{
    public int ProductBatchID { get; set; }
    public decimal Quantity { get; set; }
}
