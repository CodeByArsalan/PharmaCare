using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class EndOfDayController : BaseController
{
    private readonly IRepository<StockMain> _stockRepository;
    private readonly IRepository<Voucher> _voucherRepository;

    public EndOfDayController(IRepository<StockMain> stockRepository, IRepository<Voucher> voucherRepository)
    {
        _stockRepository = stockRepository;
        _voucherRepository = voucherRepository;
    }

    public async Task<IActionResult> Index(DateTime? date)
    {
        var reportDate = date ?? DateTime.Today;

        // 1. Get Sales
        var sales = await _stockRepository.Query()
            .Where(s => s.TransactionDate.Date == reportDate.Date 
                        && s.TransactionType.Code == "SALE" 
                        && s.Status != "Void")
            .ToListAsync();

        // 2. Get Returns
        var returns = await _stockRepository.Query()
            .Where(s => s.TransactionDate.Date == reportDate.Date 
                        && s.TransactionType.Code == "SRTN" 
                        && s.Status != "Void")
            .ToListAsync();

        // 3. Get Vouchers for Cash Flow
        // CR = Cash Receipt (Customer Payments + Cash Sales posted as vouchers)
        // CP = Cash Payment (Supplier Payments + Expenses)
        var vouchers = await _voucherRepository.Query()
            .Include(v => v.VoucherType)
            .Where(v => v.VoucherDate.Date == reportDate.Date && v.Status == "Posted")
            .ToListAsync();

        var cashReceipts = vouchers.Where(v => v.VoucherType.Code == "CR" || v.VoucherType.Code == "RV").Sum(v => v.TotalDebit); // Cash Debit
        var cashPayments = vouchers.Where(v => v.VoucherType.Code == "CP" || v.VoucherType.Code == "PV").Sum(v => v.TotalCredit); // Cash Credit

        var model = new DayClosingViewModel
        {
            Date = reportDate,
            SalesCount = sales.Count,
            TotalSales = sales.Sum(s => s.TotalAmount),
            CashSales = sales.Sum(s => s.PaidAmount),
            CreditSales = sales.Sum(s => s.TotalAmount - s.PaidAmount),
            
            ReturnsCount = returns.Count,
            TotalReturns = returns.Sum(s => s.TotalAmount),

            // Note: If Sales are posted to Vouchers immediately, "CashReceipts" from vouchers 
            // might duplicate or cover the "CashSales". 
            // Assuming "CashSales" logic in SaleService creates a "CR"/"RV" voucher.
            // So we can use Vouchers for accurate Cash In Hand.
            // But for clarity, let's show calculated values from StockMain for Sales stats.
            
            TotalCashReceived = cashReceipts, // From Vouchers (Definitive Source for Cash)
            TotalExpenses = cashPayments      // From Vouchers (Definitive Source for Cash Out)
        };

        return View(model);
    }
}
