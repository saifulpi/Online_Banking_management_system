using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineBankingSystem.Data;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly IAccountService _accountService;
    private readonly IUserService _userService;

    public AccountController(IAccountService accountService, IUserService userService)
    {
        _accountService = accountService;
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View(new CreateAccountViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAccountViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var existingAccount = await _accountService.GetAccountAsync(model.AccountNumber);
            if (existingAccount != null)
                throw new InvalidOperationException($"Account number {model.AccountNumber} already exists.");

            var account = await _accountService.CreateAccountAsync(
                model.AccountNumber, $"{model.FirstName} {model.LastName}".Trim(), model.MobileNumber, model.InitialBalance);

            var signup = new SignupViewModel
            {
                AccountNumber = model.AccountNumber,
                MobileNumber = model.MobileNumber,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Password = model.Password,
                ConfirmPassword = model.ConfirmPassword
            };

            var user = await _userService.RegisterAsync(signup);
            if (user == null)
                throw new InvalidOperationException("Account created but user registration failed.");

            TempData["Success"] =
                $"Account {account.AccountNumber} opened successfully for {user.FullName}. " +
                $"They can log in with mobile number {user.MobileNumber}.";
            return RedirectToAction(nameof(Details), new { accountNumber = account.AccountNumber });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(string accountNumber)
    {
        var account = await _accountService.GetAccountAsync(accountNumber);
        if (account == null)
            return NotFound();

        // Users may only view their own account.
        if (!User.IsInRole("Admin"))
        {
            var myAccountNumber = User.FindFirstValue("AccountNumber");
            if (account.AccountNumber != myAccountNumber)
                return RedirectToAction(nameof(AccessDenied));
        }

        return View(account);
    }

    private IActionResult AccessDenied()
    {
        return RedirectToAction("AccessDenied", "Auth");
    }

    private string MyAccountNumber()
    {
        return User.FindFirstValue("AccountNumber") ?? string.Empty;
    }

    /// <summary>
    /// Verifies the current user's password against the value they entered, used to
    /// re-authenticate a user before authorizing a money movement (deposit/withdraw/transfer).
    /// </summary>
    private async Task<bool> VerifyPasswordAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        var mobileNumber = User.FindFirstValue(ClaimTypes.MobilePhone);
        if (string.IsNullOrEmpty(mobileNumber))
            return false;

        var result = await _userService.ValidateLoginAsync(mobileNumber, password);
        return result.Success;
    }

    private async Task<decimal?> CurrentBalanceAsync(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber))
            return null;

        var account = await _accountService.GetAccountAsync(accountNumber);
        return account?.Balance;
    }

    [HttpGet]
    public async Task<IActionResult> Deposit()
    {
        ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
        var defaultNumber = MyAccountNumber();
        ViewBag.CurrentBalance = await CurrentBalanceAsync(defaultNumber);
        return View(new BankingActionViewModel { AccountNumber = defaultNumber });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(BankingActionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.AccountNumber);
            return View(model);
        }

        // Regular users may only deposit into their own account.
        if (!User.IsInRole("Admin") && model.AccountNumber != MyAccountNumber())
            return RedirectToAction(nameof(AccessDenied));

        // Non-admin users must re-authenticate with their password before money moves.
        if (!User.IsInRole("Admin") && !await VerifyPasswordAsync(model.Password))
        {
            ModelState.AddModelError("Password", "Enter your current password to authorize this deposit.");
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.AccountNumber);
            return View(model);
        }

        try
        {
            var account = await _accountService.DepositAsync(model.AccountNumber, model.Amount);
            if (account == null) return NotFound();
            TempData["Success"] = $"Deposited {model.Amount:C} into account {account.AccountNumber}. " +
                $"New balance: {account.Balance:C}.";
            return RedirectToAction(nameof(Details), new { accountNumber = account.AccountNumber });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.AccountNumber);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw()
    {
        ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
        var defaultNumber = MyAccountNumber();
        ViewBag.CurrentBalance = await CurrentBalanceAsync(defaultNumber);
        return View(new BankingActionViewModel { AccountNumber = defaultNumber });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(BankingActionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.AccountNumber);
            return View(model);
        }

        // Regular users may only withdraw from their own account.
        if (!User.IsInRole("Admin") && model.AccountNumber != MyAccountNumber())
            return RedirectToAction(nameof(AccessDenied));

        // Non-admin users must re-authenticate with their password before money moves.
        if (!User.IsInRole("Admin") && !await VerifyPasswordAsync(model.Password))
        {
            ModelState.AddModelError("Password", "Enter your current password to authorize this withdrawal.");
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.AccountNumber);
            return View(model);
        }

        try
        {
            var account = await _accountService.WithdrawAsync(model.AccountNumber, model.Amount);
            if (account == null) return NotFound();
            TempData["Success"] = $"Withdrew {model.Amount:C} from account {account.AccountNumber}. " +
                $"New balance: {account.Balance:C}.";
            return RedirectToAction(nameof(Details), new { accountNumber = account.AccountNumber });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.AccountNumber);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Transfer()
    {
        ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
        var defaultNumber = MyAccountNumber();
        ViewBag.CurrentBalance = await CurrentBalanceAsync(defaultNumber);
        return View(new TransferViewModel { FromAccountNumber = defaultNumber });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(TransferViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.FromAccountNumber);
            return View(model);
        }

        // Regular users may only transfer FROM their own account.
        if (!User.IsInRole("Admin") && model.FromAccountNumber != MyAccountNumber())
            return RedirectToAction(nameof(AccessDenied));

        // Non-admin users must re-authenticate with their password before money moves.
        if (!User.IsInRole("Admin") && !await VerifyPasswordAsync(model.Password))
        {
            ModelState.AddModelError("Password", "Enter your current password to authorize this transfer.");
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.FromAccountNumber);
            return View(model);
        }

        try
        {
            var toAccount = await _accountService.TransferAsync(
                model.FromAccountNumber, model.ToAccountNumber, model.Amount, model.Reference);
            TempData["Success"] =
                $"Successfully transferred {model.Amount:C} from account {model.FromAccountNumber} " +
                $"to account {toAccount}.";
            return RedirectToAction(nameof(Details), new { accountNumber = model.FromAccountNumber });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
            ViewBag.CurrentBalance = await CurrentBalanceAsync(model.FromAccountNumber);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> History(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber))
        {
            if (User.IsInRole("Admin"))
            {
                ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
                return View(new List<Transaction>());
            }
            else
            {
                // Users default to their own account history.
                var myAccountNumber = User.FindFirstValue("AccountNumber");
                if (string.IsNullOrEmpty(myAccountNumber))
                    return RedirectToAction(nameof(AccessDenied));
                return RedirectToAction(nameof(History), new { accountNumber = myAccountNumber });
            }
        }

        // Users may only view their own history.
        if (!User.IsInRole("Admin"))
        {
            var myAccountNumber = User.FindFirstValue("AccountNumber");
            if (accountNumber != myAccountNumber)
                return RedirectToAction(nameof(AccessDenied));
        }

        var transactions = await _accountService.GetTransactionsAsync(accountNumber);
        ViewBag.Accounts = await _accountService.GetAllAccountsAsync();
        ViewBag.AccountNumber = accountNumber;
        return View(transactions);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        var accounts = await _accountService.GetAllAccountsAsync();
        return View(accounts);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users()
    {
        var users = await _userService.GetAllUsersAsync();
        return View(users);
    }
}
