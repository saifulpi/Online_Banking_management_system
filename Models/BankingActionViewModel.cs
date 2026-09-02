using System.ComponentModel.DataAnnotations;

namespace OnlineBankingSystem.Models;

public class BankingActionViewModel
{
    [Required(ErrorMessage = "Account number is required.")]
    public string AccountNumber { get; set; } = string.Empty;

    [Range(100, double.MaxValue, ErrorMessage = "Amount must be at least 100.")]
    public decimal Amount { get; set; }

    // Re-authentication password required for the money movement to be authorized.
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}
