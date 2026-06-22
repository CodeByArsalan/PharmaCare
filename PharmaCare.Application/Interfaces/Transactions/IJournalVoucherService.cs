using PharmaCare.Application.DTOs;
using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

public interface IJournalVoucherService
{
    Task<IEnumerable<Voucher>> GetAllJournalVouchersAsync();

    /// <summary>
    /// Server-side paged + filtered manual journal vouchers for the index grid.
    /// </summary>
    Task<PagedResult<Voucher>> GetPagedJournalVouchersAsync(string? search, string? status, int page, int pageSize);
    Task<Voucher?> GetByIdAsync(int id);
    Task<Voucher> CreateJournalVoucherAsync(JournalVoucherDto model, int userId);
    Task<bool> VoidVoucherAsync(int voucherId, string reason, int userId);
    Task<string> GenerateVoucherNoAsync();
}
