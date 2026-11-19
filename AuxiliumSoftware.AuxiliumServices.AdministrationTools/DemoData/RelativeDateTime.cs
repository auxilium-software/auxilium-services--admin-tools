using System.Globalization;
using System.Text.RegularExpressions;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData;

public sealed partial class RelativeDateTime
{
    private readonly DateTime _nowUtc;
    private readonly TimeZoneInfo _timeZone;

    public RelativeDateTime(DateTime nowUtc, string timeZoneId)
    {
        _nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateTime NowUtc => _nowUtc;

    public static bool IsValidTimeZone(string timeZoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    public bool TryResolve(string? value, out DateTime utc)
    {
        utc = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = RelativePattern().Match(value.Trim());

        if (match.Success)
        {
            var amount = int.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
            if (match.Groups["sign"].Value == "-")
            {
                amount = -amount;
            }

            string unit = match.Groups["unit"].Value;
            Group clock = match.Groups["clock"];

            if (clock.Success)
            {
                if (unit != "d" || !TimeSpan.TryParseExact(clock.Value, @"hh\:mm", CultureInfo.InvariantCulture, out var timeOfDay))
                {
                    return false;
                }

                DateTime localToday = TimeZoneInfo.ConvertTimeFromUtc(_nowUtc, _timeZone).Date;
                DateTime localTarget = localToday.AddDays(amount) + timeOfDay;
                utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTarget, DateTimeKind.Unspecified), _timeZone);
                return true;
            }

            utc = unit switch
            {
                "d" => _nowUtc.AddDays(amount),
                "h" => _nowUtc.AddHours(amount),
                "m" => _nowUtc.AddMinutes(amount),
                _ => default,
            };
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var absolute))
        {
            utc = absolute.UtcDateTime;
            return true;
        }

        return false;
    }

    public DateTime Resolve(string value)
    {
        if (!TryResolve(value, out var utc))
        {
            throw new FormatException($"'{value}' is not a valid demo-data time.");
        }

        return utc;
    }

    public DateTime? ResolveOptional(string? value) =>
        value is null ? null : Resolve(value);

    [GeneratedRegex(@"^(?<sign>[+-]?)(?<amount>\d+)(?<unit>[dhm])(@(?<clock>\d{2}:\d{2}))?$")]
    private static partial Regex RelativePattern();
}
