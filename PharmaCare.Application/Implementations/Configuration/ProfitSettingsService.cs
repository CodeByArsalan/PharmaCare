using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Configuration;

public class ProfitSettingsService : IProfitSettingsService
{
    private readonly IRepository<ProfitSettings> _repository;
    private readonly IActivityLogService _activityLogService;
    private readonly ISessionService _sessionService;
    private readonly IUnitOfWork _unitOfWork;

    public ProfitSettingsService(
        IRepository<ProfitSettings> repository, 
        IActivityLogService activityLogService,
        ISessionService sessionService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _activityLogService = activityLogService;
        _sessionService = sessionService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProfitSettings> GetAsync()
    {
        var settings = await _repository.Query().FirstOrDefaultAsync();
        
        if (settings == null)
        {
            settings = new ProfitSettings
            {
                RetailProfitPercent = 20,
                WholesaleProfitPercent = 10,
                PriceRoundingStep = 1.00m,
                UpdatedAt = AppTime.Now,
                UpdatedBy = 1 // System user
            };
            await _repository.AddAsync(settings);
            await _unitOfWork.SaveChangesAsync();
        }
        
        return settings;
    }

    public async Task UpdateAsync(decimal retailProfitPercent, decimal wholesaleProfitPercent, decimal priceRoundingStep, int userId)
    {
        var settings = await GetAsync();

        settings.RetailProfitPercent = retailProfitPercent;
        settings.WholesaleProfitPercent = wholesaleProfitPercent;
        settings.PriceRoundingStep = priceRoundingStep < 0 ? 0 : priceRoundingStep;
        settings.UpdatedAt = AppTime.Now;
        settings.UpdatedBy = userId;

        _repository.Update(settings);
        await _unitOfWork.SaveChangesAsync();

        var userName = _sessionService.GetCurrentUser()?.FullName ?? "Unknown User";
        await _activityLogService.LogActivityAsync(
            userId,
            userName,
            ActivityType.Update,
            "ProfitSettings",
            settings.SettingsID.ToString(),
            null,
            null,
            $"Updated profit settings: Retail {retailProfitPercent}%, Wholesale {wholesaleProfitPercent}%, Rounding step {settings.PriceRoundingStep}");
    }
}
