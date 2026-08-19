using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces; // For IRepository
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;
using PharmaCare.Web.ViewModels.Accounting;

namespace PharmaCare.Web.Controllers.Accounting;

[Authorize]
public class JournalVoucherController : BaseController
{
    private readonly IJournalVoucherService _jvService;
    private readonly IAccountService _accountService;
    private readonly IRepository<VoucherType> _voucherTypeRepo;
    private readonly IUserService _userService;

    public JournalVoucherController(
        IJournalVoucherService jvService,
        IAccountService accountService,
        IRepository<VoucherType> voucherTypeRepo,
        IUserService userService)
    {
        _jvService = jvService;
        _accountService = accountService;
        _voucherTypeRepo = voucherTypeRepo;
        _userService = userService;
    }

    public async Task<IActionResult> JournalVoucherIndex(string? search, string? status, int page = 1, int pageSize = 25)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);
        var vouchers = await _jvService.GetPagedJournalVouchersAsync(search, status, page, pageSize);
        var users = await _userService.GetAllUsersAsync();
        ViewBag.UserNames = users.ToDictionary(u => u.Id, u => u.FullName);
        ViewBag.Search = search;
        ViewBag.SelectedStatus = status;
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
    // Reversing a posted voucher unwinds the ledger — a destructive action, gated on "delete" like
    // every other void endpoint. Without the explicit type this defaulted to "view", so anyone who
    // could merely open the page could reverse financial vouchers.
    [LinkedToPage("JournalVoucher", "JournalVoucherIndex", PermissionType = "delete")]
    public async Task<IActionResult> ReverseJournalVoucher(string id, string voidReason)
    {
        int voucherId = Utility.DecryptId(id);
        if (voucherId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Voucher ID.");
            return RedirectToAction(nameof(JournalVoucherIndex));
        }

        if (!IsVoidReasonValid(voidReason, out var reasonError))
        {
            ShowMessage(MessageType.Error, reasonError);
            return RedirectToAction(nameof(ViewJournalVoucher), new { id });
        }

        try
        {
            var result = await _jvService.VoidVoucherAsync(voucherId, voidReason.Trim(), CurrentUserId);
            ShowMessage(result ? MessageType.Success : MessageType.Error,
                result ? "Journal Voucher reversed successfully!" : "Failed to reverse voucher.");
        }
        catch (Exception ex)
        {
            // An already-reversed voucher (double submit) and a closed period both throw here.
            // Without this the second POST surfaces as a 500 instead of a message.
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "Reversing journal voucher"));
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

        if (ModelState.IsValid && vm.VoucherDetails != null)
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
                    VoucherDetails = vm.VoucherDetails
                        .Where(d => d != null)
                        .Select(d => new Application.DTOs.Transactions.JournalVoucherDetailDto 
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
                // The inner exception is the most likely place for SQL text and server details to
                // surface, so it stays out of the response entirely — SafeErrorMessage logs the
                // whole exception chain for diagnosis.
                ModelState.AddModelError("", SafeErrorMessage(ex, "AddJournalVoucher"));
            }
        }

        return View(vm);
    }
    
}
