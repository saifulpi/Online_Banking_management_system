namespace OnlineBankingSystem.Models;

public class AdminDashboardViewModel
{
    public long TotalUsers { get; set; }
    public long TotalAccounts { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal TotalTransferredFunds { get; set; }
    public decimal TotalSystemBalance { get; set; }

    public List<Transaction> RecentTransactions { get; set; } = new();

    public string? TypeFilter { get; set; }
    public string? StatusFilter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class AdminEditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "User";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^0\d{10}$", ErrorMessage = "Phone number must be 11 digits starting with 0.")]
    public string MobileNumber { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string Address { get; set; } = string.Empty;
}