using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace OnlineBankingSystem.Validation;

public sealed class MaxFileSizeMbAttribute : ValidationAttribute
{
    private readonly int _maxSizeBytes;

    public MaxFileSizeMbAttribute(int megabytes)
    {
        _maxSizeBytes = megabytes * 1024 * 1024;
        ErrorMessage = $"The file must be smaller than {megabytes} MB.";
    }

    public override bool IsValid(object? value)
    {
        if (value is not IFormFile file)
            return true;

        return file.Length <= _maxSizeBytes;
    }
}

public sealed class AllowedImageExtensionsAttribute : ValidationAttribute
{
    private readonly string[] _allowedExtensions;

    public AllowedImageExtensionsAttribute(string[] allowedExtensions)
    {
        _allowedExtensions = allowedExtensions;
        ErrorMessage = "Only JPG, JPEG, PNG or WEBP images are allowed.";
    }

    public override bool IsValid(object? value)
    {
        if (value is not IFormFile file)
            return true;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return _allowedExtensions.Contains(extension);
    }
}