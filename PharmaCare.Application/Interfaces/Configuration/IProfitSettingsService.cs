using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

public interface IProfitSettingsService
{
    Task<ProfitSettings> GetAsync();
    Task UpdateAsync(decimal retailProfitPercent, decimal wholesaleProfitPercent, int userId);
}
