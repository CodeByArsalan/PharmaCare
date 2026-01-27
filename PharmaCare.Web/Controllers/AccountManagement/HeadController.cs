using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.ViewModels;

namespace PharmaCare.Web.Controllers.AccountManagement;

[Authorize]
public class HeadController(IHeadService _headService) : BaseController
{
    public async Task<IActionResult> Heads()
    {
        var viewModel = new HeadsViewModel
        {
            HeadsList = await _headService.GetHeads(),
            IsEditMode = false
        };
        return View(viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHead(HeadsViewModel viewModel)
    {
        var head = viewModel.CurrentHead;
        head.IsActive = true;
        head.CreatedBy = LoginUserID;
        head.CreatedDate = DateTime.Now;

        var result = await _headService.CreateHead(head);
        if (result)
        {
            ShowMessage(MessageBox.Success, "Head created successfully");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to create Head");
        }
        return RedirectToAction(nameof(Heads));
    }
    public async Task<IActionResult> EditHead(string id)
    {
        int decryptedId = DecryptId(id);
        var head = await _headService.GetHeadById(decryptedId);
        if (head == null)
        {
            ShowMessage(MessageBox.Error, "Head not found");
            return RedirectToAction(nameof(Heads));
        }

        var viewModel = new HeadsViewModel
        {
            CurrentHead = head,
            HeadsList = await _headService.GetHeads(),
            IsEditMode = true
        };
        return View("Heads", viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditHead(HeadsViewModel viewModel)
    {
        var head = viewModel.CurrentHead;

        var existing = await _headService.GetHeadById(head.HeadID);
        if (existing != null)
        {
            head.IsActive = existing.IsActive;
            head.CreatedBy = existing.CreatedBy;
            head.CreatedDate = existing.CreatedDate;
        }

        head.UpdatedBy = LoginUserID;

        var result = await _headService.UpdateHead(head);
        if (result)
        {
            ShowMessage(MessageBox.Success, "Head updated successfully");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to update Head");
        }
        return RedirectToAction(nameof(Heads));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHead(string id)
    {
        int decryptedId = DecryptId(id);
        var head = await _headService.GetHeadById(decryptedId);
        
        if (head != null)
        {
            head.IsActive = !head.IsActive;
            head.UpdatedBy = LoginUserID;
            head.UpdatedDate = DateTime.Now;

            var result = await _headService.UpdateHead(head);
            if (result)
            {
                string status = head.IsActive ? "activated" : "deactivated";
                ShowMessage(MessageBox.Success, $"Head {status} successfully");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to update Head status");
            }
        }
        else
        {
            ShowMessage(MessageBox.Error, "Head not found");
        }
        return RedirectToAction(nameof(Heads));
    }
}
