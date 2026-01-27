using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PharmaCare.Infrastructure.Interfaces.Accounting;


namespace PharmaCare.Infrastructure.Implementations.Accounting;

/// <summary>
/// Automatically ensures fiscal year exists for the current year on application startup.
/// This service runs once during application initialization.
/// </summary>
public class FiscalPeriodInitializer
{
    private readonly PharmaCareDBContext _context;
    private readonly IFiscalPeriodService _fiscalPeriodService;
    private readonly ILogger<FiscalPeriodInitializer> _logger;

    public FiscalPeriodInitializer(
        PharmaCareDBContext context,
        IFiscalPeriodService fiscalPeriodService,
        ILogger<FiscalPeriodInitializer> logger)
    {
        _context = context;
        _fiscalPeriodService = fiscalPeriodService;
        _logger = logger;
    }

    /// <summary>
    /// Ensures fiscal year and periods exist for the current year.
    /// </summary>
    public async Task EnsureCurrentFiscalYearExistsAsync()
    {
        try
        {
            var currentYear = DateTime.Now.Year;
            var yearCode = $"FY-{currentYear}";

            // Check if fiscal year already exists
            var existingYear = await _context.FiscalYears
                .AsNoTracking()
                .FirstOrDefaultAsync(y => y.YearCode == yearCode);

            if (existingYear != null)
            {
                _logger.LogInformation("Fiscal year {YearCode} already exists.", yearCode);
                return;
            }

            // Create fiscal year for current year (starting Jan 1)
            var startDate = new DateTime(currentYear, 1, 1);
            var fiscalYear = await _fiscalPeriodService.CreateFiscalYearAsync(startDate, 1); // userId 1 = system

            _logger.LogInformation(
                "Created fiscal year {YearCode} with {PeriodCount} periods.",
                fiscalYear.YearCode,
                12);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize fiscal year. Journal entries will have null FiscalPeriod_ID.");
        }
    }
}
