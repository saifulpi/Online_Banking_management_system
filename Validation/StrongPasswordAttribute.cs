using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace OnlineBankingSystem.Validation;

/// <summary>
/// Validates that a password meets minimum strength requirements:
/// at least 6 characters, one uppercase letter, one lowercase letter,
/// one digit and one special character.
/// </summary>
public sealed partial class StrongPasswordAttribute : ValidationAttribute, IClientModelValidator
{
    [GeneratedRegex(@"[A-Z]")]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex(@"[a-z]")]
    private static partial Regex LowercaseRegex();

    [GeneratedRegex(@"[0-9]")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex SpecialRegex();

    public override bool IsValid(object? value)
    {
        if (value is not string password)
            return true; // Let [Required] handle it, or allow null in optional scenarios.

        // An empty value is handled by [Required]; avoid duplicate messages here.
        if (password.Length == 0)
            return true;

        return CheckRequirements(password, out _);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-strongpassword"] = BuildErrorMessage();
    }

    private static bool CheckRequirements(string password, out List<string> missing)
    {
        missing = new List<string>();

        if (password.Length < 6)
            missing.Add("at least 6 characters");

        if (!UppercaseRegex().IsMatch(password))
            missing.Add("one uppercase letter (A-Z)");

        if (!LowercaseRegex().IsMatch(password))
            missing.Add("one lowercase letter (a-z)");

        if (!DigitRegex().IsMatch(password))
            missing.Add("one number (0-9)");

        if (!SpecialRegex().IsMatch(password))
            missing.Add("one special character (e.g. @, #, $, %, !)");

        return missing.Count == 0;
    }

    private string BuildErrorMessage()
    {
        var sb = new StringBuilder("Password must include ");
        sb.Append("at least 6 characters, one uppercase letter (A-Z), one lowercase letter (a-z), one number (0-9) and one special character (e.g. @, #, $, %, !).");
        return sb.ToString();
    }
}