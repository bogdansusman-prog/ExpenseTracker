namespace ExpenseTracker.Api.Services;

/// <summary>
/// Converts between UTC (how dates are stored) and the user's local time zone
/// (configured with "App:TimeZone", default Europe/Bucharest).
/// </summary>
public class AppClock
{
    private readonly TimeZoneInfo _timeZone;

    public AppClock(IConfiguration configuration)
    {
        var id = configuration["App:TimeZone"] ?? "Europe/Bucharest";

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            _timeZone = TimeZoneInfo.Local;
        }
    }

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);

    /// <summary>Noon of the given local day, in UTC (same convention as the Angular client).</summary>
    public DateTime LocalDayToUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(12, 0)), _timeZone);

    public DateTime StartOfLocalMonthUtc(int monthsBack = 0)
    {
        var now = LocalNow;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        return TimeZoneInfo.ConvertTimeToUtc(start, _timeZone);
    }
}
