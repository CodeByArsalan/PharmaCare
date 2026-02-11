using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Web.ViewModels.Accounting;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Application.Interfaces; // For IRepository

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

    public IActionResult AddJournalVoucher()
    {
        // Pre-populate with one empty line?
        var vm = new JournalVoucherViewModel();
        vm.VoucherDetails.Add(new JournalVoucherDetailViewModel());
        
        // await LoadDropdowns(); // REMOVED
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddJournalVoucher(JournalVoucherViewModel vm)
    {
        // 1. Clean up empty lines? Or rely on validation?
        // Let's filter out lines with no account selected or zero amounts if desired.
        // For now, strict validation.

        // 2. Validate Totals
        if (vm.TotalDebit != vm.TotalCredit)
        {
            ModelState.AddModelError("", $"Total Debit ({vm.TotalDebit}) must equal Total Credit ({vm.TotalCredit}).");
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

                return RedirectToAction(nameof(JournalVoucherIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating voucher: " + ex.Message);
            }
        }

        // await LoadDropdowns(); // REMOVED
        return View(vm);
    }
    
    // ... [Void method unchanged] ...

    // Helper method LoadDropdowns removed
}
