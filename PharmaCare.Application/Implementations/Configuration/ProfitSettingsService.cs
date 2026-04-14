using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Implementations.Configuration;

public class ProfitSettingsService : IProfitSettingsService
{
    private readonly IRepository<ProfitSettings> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ProfitSettingsService(IRepository<ProfitSettings> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProfitSettings> GetAsync()
    {
        var settings = await _repository.Query().FirstOrDefaultAsync(s => s.SettingsID == 1);
        
        if (settings == null)
        {
            settings = new ProfitSettings
            {
                SettingsID = 1,
                RetailProfitPercent = 20,
                WholesaleProfitPercent = 10,
                UpdatedAt = DateTime.Now,
                UpdatedBy = 1 // System user
            };
            await _repository.AddAsync(settings);
            await _unitOfWork.SaveChangesAsync();
        }
        
        return settings;
    }

    public async Task UpdateAsync(decimal retailProfitPercent, decimal wholesaleProfitPercent, int userId)
    {
        var settings = await GetAsync();
        
        settings.RetailProfitPercent = retailProfitPercent;
        settings.WholesaleProfitPercent = wholesaleProfitPercent;
        settings.UpdatedAt = DateTime.Now;
        settings.UpdatedBy = userId;
        
        _repository.Update(settings);
        await _unitOfWork.SaveChangesAsync();
    }
}
