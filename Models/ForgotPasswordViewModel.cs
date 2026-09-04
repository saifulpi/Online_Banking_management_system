using System.ComponentModel.DataAnnotations;
using OnlineBankingSystem.Validation;

namespace OnlineBankingSystem.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [TrimmedEmail(ErrorMessage = "Enter a valid email address.")]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Registered phone number is required.")]
    [RegularExpression(@"^0\d{10}$", ErrorMessage = "Enter a valid 11-digit mobile number starting with 0.")]
    [DataType(DataType.PhoneNumber)]
    public string MobileNumber { get; set; } = string.Empty;
}