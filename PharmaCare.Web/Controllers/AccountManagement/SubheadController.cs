using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.ViewModels;

namespace PharmaCare.Web.Controllers.AccountManagement;

public class SubheadController(
    ISubheadService _subheadService,
    IHeadService _headService) : BaseController
{
    public async Task<IActionResult> Subheads()
    {
        var viewModel = new SubheadsViewModel
        {
            SubheadsList = await _subheadService.GetSubheads(),
            IsEditMode = false
        };
        return View(viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubhead(SubheadsViewModel viewModel)
    {
        var subhead = viewModel.CurrentSubhead;
        subhead.IsActive = true;
        subhead.CreatedBy = LoginUserID;
        subhead.CreatedDate = DateTime.Now;

        var result = await _subheadService.CreateSubhead(subhead);
        if (result)
        {
            ShowMessage(MessageBox.Success, "Subhead created successfully");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to create Subhead");
        }
        return RedirectToAction(nameof(Subheads));
    }
    public async Task<IActionResult> EditSubhead(string id)
    {
        int decryptedId = DecryptId(id);
        var subhead = await _subheadService.GetSubheadById(decryptedId);
        if (subhead == null)
        {
            ShowMessage(MessageBox.Error, "Subhead not found");
            return RedirectToAction(nameof(Subheads));
        }

        var viewModel = new SubheadsViewModel
        {
            CurrentSubhead = subhead,
            SubheadsList = await _subheadService.GetSubheads(),
            IsEditMode = true
        };
        return View("Subheads", viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubhead(SubheadsViewModel viewModel)
    {
        var subhead = viewModel.CurrentSubhead;

        var existing = await _subheadService.GetSubheadById(subhead.SubheadID);
        if (existing != null)
        {
            subhead.IsActive = existing.IsActive;
            subhead.CreatedBy = existing.CreatedBy;
            subhead.CreatedDate = existing.CreatedDate;
        }

        subhead.UpdatedBy = LoginUserID;

        var result = await _subheadService.UpdateSubhead(subhead);
        if (result)
        {
            ShowMessage(MessageBox.Success, "Subhead updated successfully");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to update Subhead");
        }
        return RedirectToAction(nameof(Subheads));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubhead(string id)
    {
        int decryptedId = DecryptId(id);
        var subhead = await _subheadService.GetSubheadById(decryptedId);
        
        if (subhead != null)
        {
            subhead.IsActive = !subhead.IsActive;
            subhead.UpdatedBy = LoginUserID;
            subhead.UpdatedDate = DateTime.Now;

            var result = await _subheadService.UpdateSubhead(subhead);
            if (result)
            {
                string status = subhead.IsActive ? "activated" : "deactivated";
                ShowMessage(MessageBox.Success, $"Subhead {status} successfully");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to update Subhead status");
            }
        }
        else
        {
            ShowMessage(MessageBox.Error, "Subhead not found");
        }
        return RedirectToAction(nameof(Subheads));
    }
    [HttpGet]
    public async Task<IActionResult> GetSubheadsByHead(int headId)
    {
        var subheads = await _subheadService.GetSubheadsByHeadId(headId);
        return Json(subheads.Select(s => new { id = s.SubheadID, name = s.SubheadName }));
    }
}
