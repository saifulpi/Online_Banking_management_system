using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using OnlineBankingSystem.Data;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private const int PageSize = 10;

    private readonly IAccountService _accountService;
    private readonly IUserService _userService;

    public AdminController(IAccountService accountService, IUserService userService)
    {
        _accountService = accountService;
        _userService = userService;
    }

    // ---------- Dashboard ----------

    public async Task<IActionResult> Index(string? type, string? status, DateTime? from, DateTime? to, int page = 1)
    {
        page = Math.Max(1, page);

        var transactions = await _accountService.GetTransactionsPagedAsync(
            type, status, null, null, from, to, page, PageSize);

        var viewModel = new AdminDashboardViewModel
        {
            TotalUsers = await _userService.GetTotalUsersAsync(),
            TotalAccounts = await _accountService.GetTotalAccountsAsync(),
            TotalDeposits = await _accountService.GetTotalDepositSystemAsync(),
            TotalWithdrawals = await _accountService.GetTotalWithdrawSystemAsync(),
            TotalTransferredFunds = await _accountService.GetTotalTransferredAsync(),
            TotalSystemBalance = await _accountService.GetSystemBalanceAsync(),
            RecentTransactions = transactions.Items,
            TypeFilter = type,
            StatusFilter = status,
            FromDate = from,
            ToDate = to
        };

        ViewBag.Page = page;
        ViewBag.TotalItems = transactions.Total;
        ViewBag.TotalPages = (int)Math.Ceiling(transactions.Total / (double)PageSize);
        ViewBag.NameMap = await BuildNameMapAsync();

        return View(viewModel);
    }

    private async Task<Dictionary<string, string>> BuildNameMapAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var users = await _userService.GetAllUsersAsync();
        foreach (var user in users)
        {
            if (!string.IsNullOrEmpty(user.AccountNumber))
                map[user.AccountNumber] = user.FullName;
        }
        var accounts = await _accountService.GetAllAccountsAsync();
        foreach (var account in accounts)
        {
            if (!map.ContainsKey(account.AccountNumber))
                map[account.AccountNumber] = account.Name;
        }
        return map;
    }

    // ---------- User Management ----------

    public async Task<IActionResult> Users(string? search, string? status, int page = 1)
    {
        page = Math.Max(1, page);

        var (items, total) = await _userService.GetUsersPagedAsync(search, status, page, PageSize);

        ViewBag.Page = page;
        ViewBag.TotalItems = total;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.HasNext = page < ViewBag.TotalPages;

        var accountNumbers = items.Select(u => u.AccountNumber)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var statusMap = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
        if (accountNumbers.Count > 0)
        {
            var builder = Builders<Account>.Filter;
            var filter = builder.In(a => a.AccountNumber, accountNumbers);
            var matched = await _accountService.GetAccountsAsync(filter);
            foreach (var account in matched)
                statusMap[account.AccountNumber] = account;
        }
        ViewBag.AccountStatusMap = statusMap;

        return View(items);
    }

    public async Task<IActionResult> UserDetails(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();

        var account = await _accountService.GetAccountAsync(user.AccountNumber);
        var transactions = await _accountService.GetTransactionsAsync(user.AccountNumber);

        ViewBag.Account = account;
        ViewBag.Transactions = transactions.Take(PageSize).ToList();
        ViewBag.TotalTransactions = transactions.Count;
        ViewBag.IsSelf = id == User.FindFirstValue(ClaimTypes.NameIdentifier);

        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();

        var model = new AdminEditUserViewModel
        {
            Id = user.Id,
            AccountNumber = user.AccountNumber,
            Role = user.Role,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            MobileNumber = user.MobileNumber,
            Address = user.Address
        };

        ViewBag.Unauthorized = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            && id == User.FindFirstValue(ClaimTypes.NameIdentifier);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(AdminEditUserViewModel model)
    {
        var user = await _userService.GetUserByIdAsync(model.Id);
        if (user == null)
            return NotFound();

        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            && model.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            ModelState.AddModelError(string.Empty, "You cannot edit your own administrator account.");
            return View(model);
        }

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await _userService.UpdateUserByAdminAsync(model.Id, model);
            TempData["Success"] = $"User {user.FullName} updated successfully.";
            return RedirectToAction(nameof(UserDetails), new { id = model.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateUser(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();

        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Administrator accounts cannot be deactivated.";
            return RedirectToAction(nameof(Users));
        }

        await _userService.SetUserStatusAsync(id, "Inactive");

        if (!string.IsNullOrEmpty(user.AccountNumber))
        {
            var account = await _accountService.GetAccountAsync(user.AccountNumber);
            if (account != null)
                await _accountService.SetAccountStatusAsync(user.AccountNumber, "Inactive");
        }

        TempData["Success"] = $"User {user.FullName} has been deactivated.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateUser(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();

        await _userService.SetUserStatusAsync(id, "Active");

        if (!string.IsNullOrEmpty(user.AccountNumber))
        {
            var account = await _accountService.GetAccountAsync(user.AccountNumber);
            if (account != null)
                await _accountService.SetAccountStatusAsync(user.AccountNumber, "Active");
        }

        TempData["Success"] = $"User {user.FullName} has been activated.";
        return RedirectToAction(nameof(Users));
    }

    // ---------- Account Management ----------

    public async Task<IActionResult> Accounts(string? search, string? status, int page = 1)
    {
        page = Math.Max(1, page);

        var (items, total) = await _accountService.GetAccountsPagedAsync(search, status, page, PageSize);

        var accountNumbers = items.Select(a => a.AccountNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nameMap = await BuildNameMapAsync();
        var holderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var users = await _userService.GetAllUsersAsync();
        foreach (var user in users)
        {
            if (accountNumbers.Contains(user.AccountNumber))
                holderMap[user.AccountNumber] = user.FullName;
        }

        ViewBag.Page = page;
        ViewBag.TotalItems = total;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.HolderMap = holderMap;
        ViewBag.NameMap = nameMap;

        return View(items);
    }

    public async Task<IActionResult> AccountDetails(string accountNumber)
    {
        var account = await _accountService.GetAccountAsync(accountNumber);
        if (account == null)
            return NotFound();

        var holder = await _userService.GetUserByAccountNumberAsync(accountNumber);
        var transactions = await _accountService.GetTransactionsAsync(accountNumber);

        ViewBag.Holder = holder;
        ViewBag.Transactions = transactions.Take(PageSize).ToList();
        ViewBag.TotalTransactions = transactions.Count;
        ViewBag.NameMap = await BuildNameMapAsync();

        return View(account);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FreezeAccount(string accountNumber)
    {
        var account = await _accountService.SetAccountStatusAsync(accountNumber, "Frozen");
        if (account == null)
            return NotFound();

        TempData["Success"] = $"Account {accountNumber} has been frozen.";
        return RedirectToAction(nameof(Accounts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateAccount(string accountNumber)
    {
        var account = await _accountService.SetAccountStatusAsync(accountNumber, "Active");
        if (account == null)
            return NotFound();

        TempData["Success"] = $"Account {accountNumber} has been activated.";
        return RedirectToAction(nameof(Accounts));
    }

    // ---------- Deposits ----------

    public async Task<IActionResult> Deposits(string? status, string? accountNumber, string? search,
        DateTime? from, DateTime? to, int page = 1)
    {
        page = Math.Max(1, page);

        var transactions = await _accountService.GetTransactionsPagedAsync(
            "Deposit", status, accountNumber, search, from, to, page, PageSize);

        ViewBag.Page = page;
        ViewBag.TotalItems = transactions.Total;
        ViewBag.TotalPages = (int)Math.Ceiling(transactions.Total / (double)PageSize);
        ViewBag.Status = status;
        ViewBag.AccountNumber = accountNumber;
        ViewBag.Search = search;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.NameMap = await BuildNameMapAsync();
        ViewBag.SectionTitle = "Deposits";

        return View(transactions.Items);
    }

    // ---------- Withdrawals ----------

    public async Task<IActionResult> Withdrawals(string? status, string? accountNumber, string? search,
        DateTime? from, DateTime? to, int page = 1)
    {
        page = Math.Max(1, page);

        var transactions = await _accountService.GetTransactionsPagedAsync(
            "Withdrawal", status, accountNumber, search, from, to, page, PageSize);

        ViewBag.Page = page;
        ViewBag.TotalItems = transactions.Total;
        ViewBag.TotalPages = (int)Math.Ceiling(transactions.Total / (double)PageSize);
        ViewBag.Status = status;
        ViewBag.AccountNumber = accountNumber;
        ViewBag.Search = search;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.NameMap = await BuildNameMapAsync();
        ViewBag.SectionTitle = "Withdrawals";

        return View(transactions.Items);
    }

    // ---------- Fund Transfers ----------

    public async Task<IActionResult> Transfers(string? status, string? accountNumber, string? search,
        DateTime? from, DateTime? to, int page = 1)
    {
        page = Math.Max(1, page);

        var transactions = await _accountService.GetTransactionsPagedAsync(
            "Transfer", status, accountNumber, search, from, to, page, PageSize, notePrefix: "Transferred");

        ViewBag.Page = page;
        ViewBag.TotalItems = transactions.Total;
        ViewBag.TotalPages = (int)Math.Ceiling(transactions.Total / (double)PageSize);
        ViewBag.Status = status;
        ViewBag.AccountNumber = accountNumber;
        ViewBag.Search = search;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.NameMap = await BuildNameMapAsync();
        ViewBag.SectionTitle = "Fund Transfers";

        return View("Transfers", transactions.Items);
    }

    // ---------- Transaction Management ----------

    public async Task<IActionResult> Transactions(string? type, string? status, string? accountNumber,
        string? search, DateTime? from, DateTime? to, int page = 1)
    {
        page = Math.Max(1, page);

        var transactions = await _accountService.GetTransactionsPagedAsync(
            type, status, accountNumber, search, from, to, page, PageSize);

        ViewBag.Page = page;
        ViewBag.TotalItems = transactions.Total;
        ViewBag.TotalPages = (int)Math.Ceiling(transactions.Total / (double)PageSize);
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.AccountNumber = accountNumber;
        ViewBag.Search = search;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.NameMap = await BuildNameMapAsync();
        ViewBag.SectionTitle = "Transaction Management";

        return View(transactions.Items);
    }

    public async Task<IActionResult> TransactionDetails(string id)
    {
        var transaction = await _accountService.GetTransactionByIdAsync(id);
        if (transaction == null)
            return NotFound();

        ViewBag.NameMap = await BuildNameMapAsync();

        return View(transaction);
    }
}