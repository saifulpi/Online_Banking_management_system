using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace OnlineBankingSystem.Validation;

/// <summary>
/// Validates an email address after trimming surrounding whitespace.
/// Requires a valid user@domain.tld format with a dot in the domain part.
/// Rejects values such as "user@", "user.com", "@gmail.com" and plain text.
/// </summary>
public sealed partial class TrimmedEmailAttribute : ValidationAttribute, IClientModelValidator
{
    // Also allows surrounding whitespace so the value is valid before trimming;
    // after trimming the trimmed value is what matters. The core check rejects
    // missing local/domain parts and requires a dot in the domain part.
    [GeneratedRegex(@"^[\s]*[^@\s]+@[^@\s]+\.[^@\s]+[\s]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    public override bool IsValid(object? value)
    {
        if (value is not string raw)
            return true;

        var email = raw.Trim();

        if (email.Length == 0)
            return true;

        return EmailRegex().IsMatch(email);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-regex"] = ErrorMessage ?? "Enter a valid email address.";
        // The regex mirrors the server rule, allowing surrounding whitespace.
        context.Attributes["data-val-regex-pattern"] = EmailRegex().ToString();
    }
}
