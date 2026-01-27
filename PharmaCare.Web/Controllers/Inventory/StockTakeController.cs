using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Web.Models.Inventory;
using PharmaCare.Web.Utilities;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Web.Controllers.Inventory;

public class StockTakeController : BaseController
{
    private readonly IStockService _stockService;
    private readonly IStoreService _storeService;
    private readonly IComboBoxRepository _comboBox;

    public StockTakeController(IStockService stockService, IStoreService storeService, IComboBoxRepository comboBox)
    {
        _stockService = stockService;
        _storeService = storeService;
        _comboBox = comboBox;
    }

    public async Task<IActionResult> StockTakeIndex()
    {
        var stockTakes = await _stockService.GetStockTakes();
        return View(stockTakes);
    }

    [HttpGet]
    public IActionResult AddStockTake()
    {
        return View(new InitiateStockTakeViewModel());
    }


    [HttpPost]
    public async Task<IActionResult> AddStockTake(InitiateStockTakeViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Initiate
            var result = await _stockService.InitiateStockTake(model.Store_ID, LoginUserID, model.Remarks ?? "", model.CategoryID);
            if (result != null)
            {
                ShowMessage(MessageBox.Success, "Stock Take initiated successfully.");
                return RedirectToAction(nameof(StockTakeDetails), new { id = Utility.EncryptURL(result.StockTakeID.ToString2()) });
            }
            ShowMessage(MessageBox.Error, "Failed to initiate Stock Take.");
        }
        return View(model);
    }


    [HttpGet]
    public async Task<IActionResult> StockTakeDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var stockTake = await _stockService.GetStockTake(decryptedId);
        if (stockTake == null) return NotFound();
        return View(stockTake);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateItem([FromBody] StockTakeItemUpdateViewModel model)
    {
        if (await _stockService.UpdateStockTakeItem(model.ItemID, model.PhysicalQty))
        {
            return Json(new { success = true });
        }
        return Json(new { success = false });
    }

    [HttpPost]
    public async Task<IActionResult> Complete(string id)
    {
        int decryptedId = DecryptId(id);
        if (await _stockService.CompleteStockTake(decryptedId, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Stock Take completed and inventory adjusted.");
            return RedirectToAction(nameof(StockTakeDetails), new { id });
        }
        ShowMessage(MessageBox.Error, "Failed to complete Stock Take.");
        return RedirectToAction(nameof(StockTakeDetails), new { id });
    }
}

public class StockTakeItemUpdateViewModel
{
    public int ItemID { get; set; }
    public int PhysicalQty { get; set; }
}
