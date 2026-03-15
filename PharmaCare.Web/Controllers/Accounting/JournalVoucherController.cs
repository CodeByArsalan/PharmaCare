using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Web.ViewModels.Accounting;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Application.Interfaces; // For IRepository
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Accounting;

[Authorize]
public class JournalVoucherController : BaseController
{
    private readonly IJournalVoucherService _jvService;
    private readonly IAccountService _accountService;
    private readonly IRepository<VoucherType> _voucherTypeRepo;

    public JournalVoucherController(
        IJournalVoucherService jvService,
        IAccountService accountService,
        IRepository<VoucherType> voucherTypeRepo)
    {
        _jvService = jvService;
        _accountService = accountService;
        _voucherTypeRepo = voucherTypeRepo;
    }

    public async Task<IActionResult> JournalVoucherIndex()
    {
        var vouchers = await _jvService.GetAllJournalVouchersAsync();
        return View(vouchers);
    }

    /// <summary>
    /// Displays detailed view of a journal voucher.
    /// </summary>
    public async Task<IActionResult> ViewJournalVoucher(string id)
    {
        int voucherId = Utility.DecryptId(id);
        if (voucherId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Voucher ID.");
            return RedirectToAction(nameof(JournalVoucherIndex));
        }

        var voucher = await _jvService.GetByIdAsync(voucherId);
        if (voucher == null)
        {
            ShowMessage(MessageType.Error, "Voucher not found.");
            return RedirectToAction(nameof(JournalVoucherIndex));
        }

        return View(voucher);
    }

    /// <summary>
    /// Reverses (voids) a journal voucher.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReverseJournalVoucher(string id, string voidReason)
    {
        int voucherId = Utility.DecryptId(id);
        if (voucherId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Voucher ID.");
            return RedirectToAction(nameof(JournalVoucherIndex));
        }

        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Reversal reason is required.");
            return RedirectToAction(nameof(ViewJournalVoucher), new { id });
        }

        var result = await _jvService.VoidVoucherAsync(voucherId, voidReason, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Journal Voucher reversed successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to reverse voucher.");
        }

        return RedirectToAction(nameof(JournalVoucherIndex));
    }

    public IActionResult AddJournalVoucher()
    {
        // Pre-populate with one empty line
        var vm = new JournalVoucherViewModel();
        vm.VoucherDetails.Add(new JournalVoucherDetailViewModel());
        
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddJournalVoucher(JournalVoucherViewModel vm)
    {
        // Validate Totals
        if (vm.TotalDebit != vm.TotalCredit)
        {
            ModelState.AddModelError("", $"Total Debit ({vm.TotalDebit}) must equal Total Credit ({vm.TotalCredit}).");
        }

        if (vm.VoucherDetails == null || !vm.VoucherDetails.Any())
        {
             ModelState.AddModelError("", "At least one voucher detail row is required.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Map to DTO
                var dto = new Application.DTOs.Transactions.JournalVoucherDto
                {
                    VoucherType_ID = vm.VoucherType_ID,
                    VoucherDate = vm.VoucherDate,
                    Narration = vm.Narration,
                    TotalDebit = vm.TotalDebit,
                    TotalCredit = vm.TotalCredit,
                    VoucherDetails = vm.VoucherDetails.Select(d => new Application.DTOs.Transactions.JournalVoucherDetailDto 
                    {
                        Account_ID = d.Account_ID,
                        DebitAmount = d.DebitAmount,
                        CreditAmount = d.CreditAmount,
                        Description = d.Description
                    }).ToList()
                };

                await _jvService.CreateJournalVoucherAsync(dto, CurrentUserId);
                ShowMessage(MessageType.Success, "Journal Voucher created successfully!");
                return RedirectToAction(nameof(JournalVoucherIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating voucher: " + ex.Message);
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", "Details: " + ex.InnerException.Message);
                }
            }
        }

        return View(vm);
    }
    
}
