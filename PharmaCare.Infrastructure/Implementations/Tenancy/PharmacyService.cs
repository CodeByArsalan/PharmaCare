using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Domain.Entities.Tenancy;

namespace PharmaCare.Infrastructure.Implementations.Tenancy;

public class PharmacyService : IPharmacyService
{
    private readonly IRepository<Pharmacy> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PharmacyService(IRepository<Pharmacy> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Pharmacy>> GetAllAsync()
        => await _repository.Query().OrderBy(p => p.Name).ToListAsync();

    public async Task<Pharmacy?> GetByIdAsync(int id)
        => await _repository.GetByIdAsync(id);

    public async Task<bool> IsOperationalAsync(int pharmacyId)
    {
        var pharmacy = await _repository.GetByIdAsync(pharmacyId);
        return pharmacy != null
               && pharmacy.IsActive
               && string.Equals(pharmacy.Status, "Active", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludeId = null)
    {
        var normalized = (code ?? string.Empty).Trim();
        return await _repository.Query()
            .AnyAsync(p => p.Code == normalized && (!excludeId.HasValue || p.PharmacyID != excludeId.Value));
    }

    public async Task<bool> SetStatusAsync(int pharmacyId, string status, int userId)
    {
        var pharmacy = await _repository.GetByIdAsync(pharmacyId);
        if (pharmacy == null)
        {
            return false;
        }

        pharmacy.Status = status;
        pharmacy.IsActive = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
        pharmacy.UpdatedAt = DateTime.Now;
        pharmacy.UpdatedBy = userId;
        _repository.Update(pharmacy);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
