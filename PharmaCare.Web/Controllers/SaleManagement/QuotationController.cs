using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Configuration;

namespace PharmaCare.Web.Controllers.SaleManagement;

public class QuotationController : BaseController
{
    private readonly IQuotationService _quotationService;
    private readonly IPosService _posService;
    private readonly IPartyService _partyService;

    public QuotationController(
        IQuotationService quotationService,
        IPosService posService,
        IPartyService partyService)
    {
        _quotationService = quotationService;
        _posService = posService;
        _partyService = partyService;
    }
    public async Task<IActionResult> QuotationIndex(string? status = null)
    {
        ViewBag.StatusList = new SelectList(new[] { "", "Draft", "Converted", "Expired", "Cancelled" }, status);
        ViewBag.SelectedStatus = status;

        var quotations = await _quotationService.GetQuotations(LoginUserStoreID, status);
        return View(quotations);
    }
    [HttpGet]
    public async Task<IActionResult> AddQuotation()
    {
        await PopulateDropdowns();
        var quotation = new Quotation
        {
            Store_ID = LoginUserStoreID ?? 0,
            QuotationDate = DateTime.Now,
            ValidUntil = DateTime.Now.AddDays(30)
        };
        return View(quotation);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuotation(Quotation quotation)
    {
        try
        {
            quotation.Store_ID = LoginUserStoreID ?? 0;

            int quotationId = await _quotationService.CreateQuotation(quotation, LoginUserID);
            ShowMessage(MessageBox.Success, "Quotation created successfully.");
            return RedirectToAction(nameof(QuotationDetails), new { id = Utility.EncryptURL(quotationId.ToString()) });
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, $"An error occurred: {ex.Message}");
        }

        await PopulateDropdowns(quotation.Party_ID);
        return View(quotation);
    }
    public async Task<IActionResult> QuotationDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var quotation = await _quotationService.GetQuotationById(decryptedId);

        if (quotation == null)
            return NotFound();

        return View(quotation);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertToSale(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            int saleId = await _quotationService.ConvertToSale(decryptedId, LoginUserID);
            ShowMessage(MessageBox.Success, "Quotation converted to sale successfully.");
            return RedirectToAction("Receipt", "Pos", new { id = Utility.EncryptURL(saleId.ToString()) });
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, $"An error occurred: {ex.Message}");
        }

        return RedirectToAction(nameof(QuotationIndex));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelQuotation(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            if (await _quotationService.CancelQuotation(decryptedId, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Quotation cancelled successfully.");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to cancel quotation.");
            }
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }

        return RedirectToAction(nameof(QuotationIndex));
    }
    [HttpGet]
    public async Task<IActionResult> SearchProducts(string query)
    {
        var products = await _posService.SearchProductsAsync(query);
        return Json(products);
    }
    [HttpGet]
    public async Task<IActionResult> GetBatchDetails(int batchId)
    {
        var batch = await _posService.GetBatchDetailsAsync(batchId);
        return Json(batch);
    }

    private async Task PopulateDropdowns(int? selectedPartyId = null)
    {
        var customers = await _partyService.GetCustomers();
        ViewBag.Customers = new SelectList(customers, "PartyID", "PartyName", selectedPartyId);
    }
}
