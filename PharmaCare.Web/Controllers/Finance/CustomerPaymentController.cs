using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Infrastructure.Interfaces;
using System.Security.Claims;

namespace PharmaCare.Web.Controllers.Finance;

[Authorize]
public class CustomerPaymentController : BaseController
{
    private readonly ICustomerPaymentService _customerPaymentService;
    private readonly IComboBoxRepository _comboBox;

    public CustomerPaymentController(
        ICustomerPaymentService customerPaymentService,
        IComboBoxRepository comboBox)
    {
        _customerPaymentService = customerPaymentService;
        _comboBox = comboBox;
    }

    // GET: CustomerPayment/CustomerPaymentIndex
    public async Task<IActionResult> CustomerPaymentIndex()
    {
        var customers = await _customerPaymentService.GetCustomersWithOutstanding();
        ViewBag.Customers = await _comboBox.GetCustomersAsync();
        return View(customers);
    }

    // GET: CustomerPayment/GetOutstandingSales
    [HttpGet]
    public async Task<IActionResult> GetOutstandingSales(int? customerId)
    {
        var sales = await _customerPaymentService.GetOutstandingSales(customerId);
        return Json(sales);
    }

    // GET: CustomerPayment/GetPaymentHistory
    [HttpGet]
    public async Task<IActionResult> GetPaymentHistory(int? customerId, DateTime? startDate, DateTime? endDate)
    {
        var payments = await _customerPaymentService.GetAllPayments(customerId, startDate, endDate);
        return Json(payments);
    }

    // GET: CustomerPayment/Create
    public async Task<IActionResult> AddCustomerPayment(int? customerId, int? saleId)
    {
        ViewBag.Customers = await _comboBox.GetCustomersAsync();
        ViewBag.SelectedCustomerId = customerId;
        ViewBag.SelectedSaleId = saleId;

        if (customerId.HasValue)
        {
            ViewBag.TotalOutstanding = await _customerPaymentService.GetTotalOutstandingForCustomer(customerId.Value);
        }

        return View(new CreateCustomerPaymentDto
        {
            CustomerID = customerId,
            SaleID = saleId
        });
    }

    // POST: CustomerPayment/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomerPayment(CreateCustomerPaymentDto dto)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Customers = await _comboBox.GetCustomersAsync();
            return View(dto);
        }

        try
        {
            var paymentId = await _customerPaymentService.CreatePayment(dto, LoginUserID);
            TempData["SuccessMessage"] = "Payment recorded successfully.";
            return RedirectToAction(nameof(CustomerPaymentIndex));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            ViewBag.Customers = await _comboBox.GetCustomersAsync();
            return View(dto);
        }
    }

    // POST: CustomerPayment/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelCustomerPayment(int paymentId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Json(new { success = false, message = "Reason is required." });
        }

        try
        {
            var result = await _customerPaymentService.CancelPayment(paymentId, reason, LoginUserID);
            if (result)
            {
                return Json(new { success = true, message = "Payment cancelled successfully." });
            }
            return Json(new { success = false, message = "Failed to cancel payment." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // AJAX: CustomerPayment/CreatePaymentAjax
    [HttpPost]
    public async Task<IActionResult> CreatePaymentAjax([FromBody] CreateCustomerPaymentDto dto)
    {
        try
        {
            var paymentId = await _customerPaymentService.CreatePayment(dto, LoginUserID);
            return Json(new { success = true, paymentId = paymentId, message = "Payment recorded successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
