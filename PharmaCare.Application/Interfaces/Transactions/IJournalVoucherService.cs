using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

public interface IJournalVoucherService
{
    Task<IEnumerable<Voucher>> GetAllJournalVouchersAsync();
    Task<Voucher?> GetByIdAsync(int id);
    Task<Voucher> CreateJournalVoucherAsync(JournalVoucherDto model, int userId);
    Task<bool> VoidVoucherAsync(int voucherId, string reason, int userId);
    Task<string> GenerateVoucherNoAsync();
}
