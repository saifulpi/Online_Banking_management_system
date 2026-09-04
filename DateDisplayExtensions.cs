using System.Globalization;

namespace OnlineBankingSystem;

/// <summary>
/// Formats dates consistently in English (invariant culture) with a lowercase,
/// abbreviated month and a 12-hour clock, e.g. "2 sep 2026" or "2 sep 2026, 2:30 PM".
/// Times are converted to Bangladesh Standard Time (UTC+6, no DST).
/// The application uses the bn-BD culture as its default, so explicit English
/// formatting is required to avoid Bengali month names / numeral rendering.
/// </summary>
public static class DateDisplayExtensions
{
    private static readonly TimeZoneInfo BangladeshTimeZone = ResolveBangladeshTimeZone();

    private static TimeZoneInfo ResolveBangladeshTimeZone()
    {
        // Asia/Dhaka = UTC+6, no daylight saving. Prefer the IANA zone where
        // available, otherwise fall back to an explicit fixed UTC+6 zone.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Bangladesh Standard Time",
                TimeSpan.FromHours(6),
                "Bangladesh Standard Time",
                "BST");
        }
    }

    private static DateTime ToBangladeshTime(DateTime value) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            BangladeshTimeZone);

    public static string ToDisplayDate(this DateTime date) =>
        Format(date, "d MMM yyyy");

    public static string ToDisplayDateTime(this DateTime date) =>
        Format(date, "d MMM yyyy, h:mm tt");

    public static string ToDisplayDateTimeSeconds(this DateTime date) =>
        Format(date, "d MMM yyyy, h:mm:ss tt");

    public static string ToDisplayDate(this DateTime? date) =>
        date.HasValue ? Format(date.Value, "d MMM yyyy") : "-";

    public static string ToDisplayDateTime(this DateTime? date) =>
        date.HasValue ? Format(date.Value, "d MMM yyyy, h:mm tt") : "-";

    private static string Format(DateTime value, string pattern)
    {
        var text = ToBangladeshTime(value)
            .ToString(pattern, CultureInfo.InvariantCulture);

        // Lowercase the abbreviated month abbreviation: "2 Sep 2026" -> "2 sep 2026".
        var split = text.IndexOf(' ');
        if (split > 0 && split < text.Length - 1)
        {
            var secondSpace = text.IndexOf(' ', split + 1);
            if (secondSpace > split)
            {
                var month = text.Substring(split + 1, secondSpace - split - 1);
                text = text.Remove(split + 1, month.Length).Insert(split + 1, month.ToLowerInvariant());
            }
        }

        return text;
    }
}