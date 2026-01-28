using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Web.Controllers;

namespace PharmaCare.Web.Controllers.Accounting
{
    [Authorize]
    public class AccountingController : BaseController
    {
        private readonly IVoucherService _voucherService;
        private readonly IAccountingService _accountingService;
        private readonly ISystemUserService _userService;

        public AccountingController(
            IVoucherService voucherService,
            IAccountingService accountingService,
            ISystemUserService userService)
        {
            _voucherService = voucherService;
            _accountingService = accountingService;
            _userService = userService;
        }

        // GET: Accounting/VoucherIndex
        public async Task<IActionResult> VoucherIndex(DateTime? fromDate, DateTime? toDate)
        {
            fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            toDate ??= DateTime.Now;

            var vouchers = await _voucherService.GetVouchersAsync(fromDate, toDate);
            
            ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

            return View(vouchers); 
        }

        // GET: Accounting/CreateVoucher
        public async Task<IActionResult> CreateVoucher()
        {
            ViewBag.VoucherTypes = await _voucherService.GetVoucherTypesAsync();
            var accounts = await _accountingService.GetChartOfAccounts(true); // Active accounts
            ViewBag.Accounts = accounts;
            return View();
        }

        // POST: Accounting/CreateVoucher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVoucher([FromBody] CreateVoucherRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = GetCurrentUserId(); // Helper from BaseController
                if (userId == 0) return Unauthorized();

                request.CreatedBy = userId;
                
                var voucher = await _voucherService.CreateVoucherAsync(request);
                await _voucherService.PostVoucherAsync(voucher.VoucherID);

                // return RedirectToAction(nameof(VoucherIndex));
                return Json(new { success = true, voucherId = voucher.VoucherID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Accounting/VoucherDetails/5
        public async Task<IActionResult> VoucherDetails(int id)
        {
            var voucher = await _voucherService.GetVoucherAsync(id);
            if (voucher == null)
            {
                return NotFound();
            }

            return View(voucher);
        }

        private int GetCurrentUserId()
        {
            // Assuming BaseController or Claims handling
             var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
             if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
             {
                 return userId;
             }
             return 0;
        }
    }
}
