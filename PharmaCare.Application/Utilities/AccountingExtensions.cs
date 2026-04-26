using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Utilities;

public static class AccountingExtensions
{
    /// <summary>
    /// Generates a unique voucher number based on a prefix.
    /// Format: [PREFIX]-[YYYYMMDD]-[SEQ]
    /// </summary>
    public static async Task<string> GenerateVoucherNoAsync(this IRepository<Voucher> voucherRepository, string prefix)
    {
        var datePrefix = $"{prefix}-{DateTime.Now:yyyyMMdd}-";

        var lastVoucher = await voucherRepository.Query()
            .Where(v => v.VoucherNo != null && v.VoucherNo.StartsWith(datePrefix))
            .OrderByDescending(v => v.VoucherNo)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastVoucher != null && lastVoucher.VoucherNo != null)
        {
            var parts = lastVoucher.VoucherNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    /// <summary>
    /// Creates a reversal voucher for an existing voucher.
    /// Reverses the original voucher by swapping Debits and Credits.
    /// </summary>
    public static async Task<Voucher?> CreateReversalVoucherAsync(
        this IRepository<Voucher> voucherRepository, 
        int originalVoucherId, 
        int userId, 
        string reason, 
        string? sourceTableOverride = null, 
        int? sourceIdOverride = null)
    {
        var originalVoucher = await voucherRepository.Query()
            .Include(v => v.VoucherDetails)
            .FirstOrDefaultAsync(v => v.VoucherID == originalVoucherId);

        if (originalVoucher == null || originalVoucher.IsReversed)
        {
            return null;
        }

        var reversalVoucher = new Voucher
        {
            VoucherType_ID = originalVoucher.VoucherType_ID,
            VoucherNo = $"REV-{originalVoucher.VoucherNo}",
            VoucherDate = DateTime.Now,
            TotalDebit = originalVoucher.TotalCredit,
            TotalCredit = originalVoucher.TotalDebit,
            Status = "Posted",
            SourceTable = sourceTableOverride ?? originalVoucher.SourceTable,
            SourceID = sourceIdOverride ?? originalVoucher.SourceID,
            Narration = $"Reversal of {originalVoucher.VoucherNo} - Void: {reason}",
            ReversesVoucher_ID = originalVoucher.VoucherID,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        foreach (var detail in originalVoucher.VoucherDetails)
        {
            reversalVoucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = detail.Account_ID,
                DebitAmount = detail.CreditAmount,
                CreditAmount = detail.DebitAmount,
                Description = $"Reversal - {detail.Description}",
                Party_ID = detail.Party_ID,
                Product_ID = detail.Product_ID
            });
        }

        await voucherRepository.AddAsync(reversalVoucher);

        originalVoucher.IsReversed = true;
        originalVoucher.ReversedByVoucher = reversalVoucher;
        voucherRepository.Update(originalVoucher);

        return reversalVoucher;
    }
}
