using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBankingSystem.Data;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IAccountService _accountService;
    private readonly IUserService _userService;

    public HomeController(IAccountService accountService, IUserService userService)
    {
        _accountService = accountService;
        _userService = userService;
    }

    public async Task<IActionResult> Index()
    {
        var isAdmin = User.IsInRole("Admin");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = userId != null ? await _userService.GetUserByIdAsync(userId) : null;

        if (isAdmin)
        {
            return RedirectToAction("Index", "Admin");
        }

        // Regular user dashboard - all data belongs to the authenticated user only.
        var userVm = new DashboardViewModel
        {
            IsAdmin = false,
            UserName = currentUser?.FullName ?? "User",
            CurrentUser = currentUser
        };

        if (currentUser != null && !string.IsNullOrEmpty(currentUser.AccountNumber))
        {
            var account = await _accountService.GetAccountAsync(currentUser.AccountNumber);
            if (account == null)
                account = await _accountService.EnsureAccountExistsAsync(currentUser.AccountNumber);

            userVm.MyAccount = account;

            var transactions = await _accountService.GetTransactionsAsync(account.AccountNumber);
            userVm.TotalTransactions = transactions.Count;
            userVm.LastActivityDate = transactions.FirstOrDefault()?.Date;
            userVm.RecentTransactions = transactions.Take(5).ToList();

            if (transactions.Count > 0)
            {
                userVm.TotalDeposit = transactions
                    .Where(t => t.Type == TransactionType.Deposit)
                    .Sum(t => t.Amount);

                // Money leaving the account: withdrawals + transfers sent to another account.
                userVm.TotalWithdraw = transactions
                        .Where(t => t.Type == TransactionType.Withdrawal)
                        .Sum(t => t.Amount)
                    + transactions
                        .Where(t => t.Type == TransactionType.Transfer
                            && t.Note.StartsWith("Transferred", StringComparison.OrdinalIgnoreCase))
                        .Sum(t => t.Amount);
            }
        }

        return View("UserDashboard", userVm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Contact()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}