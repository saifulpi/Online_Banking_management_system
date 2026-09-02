using System.ComponentModel.DataAnnotations;

namespace OnlineBankingSystem.Models;

public class TransferViewModel
{
    [Required(ErrorMessage = "Sender account number is required.")]
    public string FromAccountNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Receiver account number is required.")]
    public string ToAccountNumber { get; set; } = string.Empty;

    [Range(100, double.MaxValue, ErrorMessage = "Amount must be at least 100.")]
    public decimal Amount { get; set; }

    [StringLength(200, ErrorMessage = "Reference cannot exceed 200 characters.")]
    public string? Reference { get; set; }

    // Re-authentication password required for the money movement to be authorized.
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}
