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
        // A negative margin is a request to sell below cost, which the pricing formula is not
        // allowed to honour anyway (PricingService clamps the computed price up to cost). Storing
        // it would leave the settings screen showing a policy the system does not follow.
        if (retailProfitPercent < 0 || wholesaleProfitPercent < 0)
        {
            throw new InvalidOperationException(
                "Profit margins cannot be negative — that would price stock below what it cost to buy.");
        }

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
