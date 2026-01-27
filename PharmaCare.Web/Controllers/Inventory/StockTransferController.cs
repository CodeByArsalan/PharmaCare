using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Inventory;

public class StockTransferController : BaseController
{
    private readonly IStockService _stockService;
    private readonly IStoreService _storeService;

    public StockTransferController(IStockService stockService, IStoreService storeService)
    {
        _stockService = stockService;
        _storeService = storeService;
    }

    public async Task<IActionResult> StockTransferIndex()
    {
        var transfers = await _stockService.GetStockTransfers();
        return View(transfers);
    }

    [HttpGet]
    public async Task<IActionResult> CreateStockTransfer()
    {
        var stores = await _storeService.GetStoresByLoginUserID(LoginUserID);
        ViewBag.Stores = new SelectList(stores, "StoreID", "Name");

        var model = new StockTransfer
        {
            TransferNumber = UniqueIdGenerator.Generate("ST"),
            TransferDate = DateTime.Now
        };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateStockTransfer(StockTransfer transfer)
    {
        if (transfer.SourceStore_ID == transfer.DestinationStore_ID)
        {
            ModelState.AddModelError("", "Source and Destination stores cannot be the same.");
        }

        if (ModelState.IsValid)
        {
            if (await _stockService.CreateStockTransfer(transfer, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Stock Transfer initiated successfully.");
                return RedirectToAction(nameof(StockTransferIndex));
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to create transfer. Please check stock levels.");
            }
        }

        var stores = await _storeService.GetStoresByLoginUserID(LoginUserID);
        ViewBag.Stores = new SelectList(stores, "StoreID", "Name");
        return View(transfer);
    }

    [HttpGet]
    public async Task<IActionResult> StockTransferDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var transfer = await _stockService.GetStockTransferById(decryptedId);
        if (transfer == null) return NotFound();
        return View(transfer);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveTransfer(int id)
    {
        if (await _stockService.ApproveStockTransfer(id, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Stock Transfer approved and inventory updated.");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to approve transfer.");
        }
        return RedirectToAction(nameof(StockTransferDetails), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> GetBatchesForStore(int storeId)
    {
        var items = await _stockService.GetTransferableItems(storeId);
        return Json(items);
    }
}
