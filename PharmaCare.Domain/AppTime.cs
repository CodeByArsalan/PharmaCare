namespace PharmaCare.Domain;

/// <summary>
/// The application clock. All business timestamps (audit fields, transaction dates,
/// document-number date parts, report "today") come from here, pinned to the pharmacy
/// timezone — Pakistan Standard Time by default — so behaviour does not change with the
/// hosting server's OS clock. Deploying to a cloud region in another timezone requires
/// no code change and shifts nothing.
///
/// Never use DateTime.Now / DateTime.Today in application code; use AppTime.Now / AppTime.Today.
/// </summary>
public static class AppTime
{
    private static TimeZoneInfo _timeZone = ResolveTimeZone("Asia/Karachi");

    /// <summary>The active business timezone.</summary>
    public static TimeZoneInfo TimeZone => _timeZone;

    /// <summary>Current date-time in the business timezone (Kind = Unspecified, as stored).</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

    /// <summary>Current date in the business timezone.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>
    /// Optionally overrides the timezone from configuration (e.g. "AppTime:TimeZone").
    /// Accepts IANA ("Asia/Karachi") or Windows ("Pakistan Standard Time") ids.
    /// Called once at startup; a null/blank id keeps the default.
    /// </summary>
    public static void Initialize(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            _timeZone = ResolveTimeZone(timeZoneId.Trim());
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        // .NET 6+ usually converts IANA<->Windows ids automatically (ICU); fall back to the
        // Windows id for Pakistan, then to UTC+5 fixed offset (PKT has no DST), never to the
        // machine's local zone — an implicit local fallback would defeat the whole point.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            }
            catch (Exception inner) when (inner is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("PKT+5", TimeSpan.FromHours(5), "Pakistan Time (fixed)", "Pakistan Time (fixed)");
            }
        }
    }
}
