using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.ViewModels;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Configuration;

public class StoreController(IStoreService _storeService) : BaseController
{
    public async Task<IActionResult> Stores()
    {
        var viewModel = new StoresViewModel
        {
            StoreList = await _storeService.GetStoresByLoginUserID(LoginUserID),
            IsEditMode = false
        };
        return View(viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStore(StoresViewModel viewModel)
    {
        var storeModel = viewModel.CurrentStore;

        if (await _storeService.CreateStore(storeModel, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Store created successfully!");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to create Store.");
        }
        return RedirectToAction(nameof(Stores));
    }
    public async Task<IActionResult> EditStore(string id)
    {
        int decryptedId = DecryptId(id);
        var store = await _storeService.GetStoreById(decryptedId);
        if (store == null)
        {
            ShowMessage(MessageBox.Error, "Store not found.");
            return RedirectToAction(nameof(Stores));
        }

        var viewModel = new StoresViewModel
        {
            CurrentStore = store,
            StoreList = await _storeService.GetStoresByLoginUserID(LoginUserID),
            IsEditMode = true
        };
        return View("Stores", viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStore(StoresViewModel viewModel)
    {
        var storeModel = viewModel.CurrentStore;

        if (await _storeService.UpdateStore(storeModel, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Store updated successfully!");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to update Store.");
        }
        return RedirectToAction(nameof(Stores));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStore(string id)
    {
        int decryptedId = DecryptId(id);
        if (await _storeService.DeleteStore(decryptedId, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Store deleted successfully.");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to delete store.");
        }
        return RedirectToAction(nameof(Stores));
    }
}
