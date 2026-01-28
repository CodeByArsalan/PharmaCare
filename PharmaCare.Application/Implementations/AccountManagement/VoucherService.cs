using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.AccountManagement;

/// <summary>
/// Implementation of VoucherService for AccountVoucher operations
/// </summary>
public class VoucherService : IVoucherService
{
    private readonly IRepository<AccountVoucher> _voucherRepo;
    private readonly IRepository<AccountVoucherDetail> _detailRepo;
    private readonly IRepository<AccountVoucherType> _typeRepo;
    private readonly IRepository<FiscalPeriod> _fiscalPeriodRepo;

    public VoucherService(
        IRepository<AccountVoucher> voucherRepo,
        IRepository<AccountVoucherDetail> detailRepo,
        IRepository<AccountVoucherType> typeRepo,
        IRepository<FiscalPeriod> fiscalPeriodRepo)
    {
        _voucherRepo = voucherRepo;
        _detailRepo = detailRepo;
        _typeRepo = typeRepo;
        _fiscalPeriodRepo = fiscalPeriodRepo;
    }

    public async Task<AccountVoucher> CreateVoucherAsync(CreateVoucherRequest request)
    {
        var voucherCode = await GenerateVoucherCodeAsync(request.VoucherTypeId);

        var fiscalPeriod = await _fiscalPeriodRepo.FindByCondition(
            fp => fp.StartDate <= request.VoucherDate && fp.EndDate >= request.VoucherDate)
            .FirstOrDefaultAsync();

        var voucher = new AccountVoucher
        {
            VoucherType_ID = request.VoucherTypeId,
            VoucherCode = voucherCode,
            VoucherDate = request.VoucherDate,
            SourceTable = request.SourceTable,
            SourceID = request.SourceId,
            Store_ID = request.StoreId,
            FiscalPeriod_ID = fiscalPeriod?.FiscalPeriodID,
            Narration = request.Narration,
            Status = "Posted",
            CreatedBy = request.CreatedBy,
            CreatedDate = DateTime.Now,
            VoucherDetails = request.Lines.Select(l => new AccountVoucherDetail
            {
                Account_ID = l.AccountId,
                Dr = l.Dr,
                Cr = l.Cr,
                Product_ID = l.ProductId,
                Particulars = l.Particulars,
                Store_ID = l.StoreId ?? request.StoreId
            }).ToList()
        };

        voucher.TotalDebit = voucher.VoucherDetails.Sum(d => d.Dr);
        voucher.TotalCredit = voucher.VoucherDetails.Sum(d => d.Cr);

        return await _voucherRepo.InsertAndReturn(voucher);
    }

    public async Task<AccountVoucher> CreateVoucherFromStockTransactionAsync(int stockMainId)
    {
        throw new NotImplementedException("Use CreateVoucherAsync with proper lines from StockTransactionService");
    }

    public async Task<AccountVoucher?> GetVoucherAsync(int voucherId)
    {
        return await _voucherRepo.FindByCondition(v => v.VoucherID == voucherId)
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherDetails)
                .ThenInclude(d => d.Account)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AccountVoucher>> GetVouchersBySourceAsync(string sourceTable, int sourceId)
    {
        return await _voucherRepo.FindByCondition(v => v.SourceTable == sourceTable && v.SourceID == sourceId)
            .Include(v => v.VoucherType)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountVoucher>> GetVouchersByTypeAsync(int voucherTypeId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _voucherRepo.FindByCondition(v => v.VoucherType_ID == voucherTypeId)
            .Include(v => v.VoucherType)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(v => v.VoucherDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(v => v.VoucherDate <= toDate.Value);

        return await query.OrderByDescending(v => v.VoucherDate).ToListAsync();
    }

    public async Task<IEnumerable<AccountVoucher>> GetVouchersAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _voucherRepo.FindByCondition(v => true)
            .Include(v => v.VoucherType)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(v => v.VoucherDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(v => v.VoucherDate <= toDate.Value);

        return await query.OrderByDescending(v => v.VoucherDate).ToListAsync();
    }

    public async Task PostVoucherAsync(int voucherId)
    {
        var voucher = await _voucherRepo.GetByIdAsync(voucherId)
            ?? throw new InvalidOperationException($"Voucher {voucherId} not found");

        if (voucher.Status != "Draft")
            throw new InvalidOperationException($"Voucher {voucherId} is already posted");

        voucher.Status = "Posted";
        await _voucherRepo.Update(voucher);
    }

    public async Task<AccountVoucher> ReverseVoucherAsync(int voucherId, string reason, int userId)
    {
        var original = await _voucherRepo.FindByCondition(v => v.VoucherID == voucherId)
            .Include(v => v.VoucherDetails)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Voucher {voucherId} not found");

        if (original.IsReversed)
            throw new InvalidOperationException($"Voucher {voucherId} is already reversed");

        var reversalCode = await GenerateVoucherCodeAsync(original.VoucherType_ID);
        var reversal = new AccountVoucher
        {
            VoucherType_ID = original.VoucherType_ID,
            VoucherCode = reversalCode,
            VoucherDate = DateTime.Now.Date,
            SourceTable = original.SourceTable,
            SourceID = original.SourceID,
            Store_ID = original.Store_ID,
            FiscalPeriod_ID = original.FiscalPeriod_ID,
            Narration = $"Reversal of {original.VoucherCode}: {reason}",
            Status = "Posted",
            Reverses_ID = voucherId,
            CreatedBy = userId,
            CreatedDate = DateTime.Now,
            VoucherDetails = original.VoucherDetails.Select(d => new AccountVoucherDetail
            {
                Account_ID = d.Account_ID,
                Dr = d.Cr,
                Cr = d.Dr,
                Product_ID = d.Product_ID,
                Particulars = $"Reversal: {d.Particulars}",
                Store_ID = d.Store_ID
            }).ToList()
        };

        reversal.TotalDebit = reversal.VoucherDetails.Sum(d => d.Dr);
        reversal.TotalCredit = reversal.VoucherDetails.Sum(d => d.Cr);

        var savedReversal = await _voucherRepo.InsertAndReturn(reversal);

        original.IsReversed = true;
        original.ReversedBy_ID = savedReversal.VoucherID;
        await _voucherRepo.Update(original);

        return savedReversal;
    }

    public async Task<string> GenerateVoucherCodeAsync(int voucherTypeId)
    {
        var voucherType = await _typeRepo.GetByIdAsync(voucherTypeId);
        var prefix = voucherType?.Code ?? "VCH";
        return UniqueIdGenerator.Generate(prefix);
    }

    public async Task<IEnumerable<AccountVoucherType>> GetVoucherTypesAsync()
    {
        return await _typeRepo.GetAll();
    }
}
