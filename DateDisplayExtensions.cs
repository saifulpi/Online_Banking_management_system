using System.Globalization;

namespace OnlineBankingSystem;

/// <summary>
/// Formats dates consistently in English (invariant culture) with a lowercase,
/// abbreviated month, e.g. "2 sep 2026" or "2 sep 2026, 14:30".
/// The application uses the bn-BD culture as its default, so explicit English
/// formatting is required to avoid Bengali month names / numeral rendering.
/// </summary>
public static class DateDisplayExtensions
{
    public static string ToDisplayDate(this DateTime date) =>
        Format(date, "d MMM yyyy");

    public static string ToDisplayDateTime(this DateTime date) =>
        Format(date, "d MMM yyyy, HH:mm");

    public static string ToDisplayDateTimeSeconds(this DateTime date) =>
        Format(date, "d MMM yyyy, HH:mm:ss");

    public static string ToDisplayDate(this DateTime? date) =>
        date.HasValue ? Format(date.Value, "d MMM yyyy") : "-";

    public static string ToDisplayDateTime(this DateTime? date) =>
        date.HasValue ? Format(date.Value, "d MMM yyyy, HH:mm") : "-";

    private static string Format(DateTime value, string pattern)
    {
        var text = value
            .ToLocalTime()
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
