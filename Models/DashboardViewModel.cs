namespace OnlineBankingSystem.Models;

public class DashboardViewModel
{
    public bool IsAdmin { get; set; }
    public string UserName { get; set; } = string.Empty;
    public AppUser? CurrentUser { get; set; }
    public Account? MyAccount { get; set; }

    // Admin summary
    public int TotalAccounts { get; set; }
    public decimal TotalBalance { get; set; }
    public List<Account> RecentAccounts { get; set; } = new();

    // User summary
    public decimal TotalDeposit { get; set; }
    public decimal TotalWithdraw { get; set; }
    public int TotalTransactions { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public List<Transaction> RecentTransactions { get; set; } = new();
}