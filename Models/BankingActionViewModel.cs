using System.ComponentModel.DataAnnotations;

namespace OnlineBankingSystem.Models;

public class BankingActionViewModel
{
    [Required(ErrorMessage = "Account number is required.")]
    public string AccountNumber { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    // Re-authentication password required for the money movement to be authorized.
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}
