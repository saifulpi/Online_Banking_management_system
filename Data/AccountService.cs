using MongoDB.Driver;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Data;

public interface IAccountService
{
    Task<Account> CreateAccountAsync(string accountNumber, string name, string phoneNumber, decimal initialBalance);
    Task<Account?> GetAccountAsync(string accountNumber);
    Task<List<Account>> GetAllAccountsAsync();
    Task<List<Account>> GetAccountsAsync(FilterDefinition<Account> filter);
    Task<Account> EnsureAccountExistsAsync(string accountNumber);
    Task<Account?> DepositAsync(string accountNumber, decimal amount);
    Task<Account?> WithdrawAsync(string accountNumber, decimal amount);
    Task<string> TransferAsync(string fromAccountNumber, string toAccountNumber, decimal amount, string? reference = null);
    Task<List<Transaction>> GetTransactionsAsync(string accountNumber);
    Task<decimal> GetTotalDepositAsync(string accountNumber);
    Task<decimal> GetTotalWithdrawAsync(string accountNumber);
    Task<(List<Account> Items, long Total)> GetAccountsPagedAsync(string? search, string? status, int page, int pageSize);
    Task<long> GetTotalAccountsAsync();
    Task<decimal> GetSystemBalanceAsync();
    Task<decimal> GetTotalDepositSystemAsync();
    Task<decimal> GetTotalWithdrawSystemAsync();
    Task<decimal> GetTotalTransferredAsync();
    Task<(List<Transaction> Items, long Total)> GetTransactionsPagedAsync(
        string? type, string? status, string? accountNumber, string? search,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize, string? notePrefix = null);
    Task<Transaction?> GetTransactionByIdAsync(string id);
    Task<Account?> SetAccountStatusAsync(string accountNumber, string status);
}

public class AccountService : IAccountService
{
    private readonly MongoDbContext _context;
    private readonly IMongoClient _client;
    private readonly decimal _minimumTransactionAmount;
    private readonly decimal _withdrawLimit;
    private readonly decimal _transferLimit;
    private readonly decimal _dailyWithdrawLimit;
    private readonly int _dailyWithdrawCountLimit;
    private readonly int _dailyTransferCountLimit;

    public AccountService(MongoDbContext context, IConfiguration configuration)
    {
        _context = context;
        _client = context.Client;
        _minimumTransactionAmount = configuration.GetValue<decimal>("BankingSettings:MinimumTransactionAmount");
        _withdrawLimit = configuration.GetValue<decimal>("BankingSettings:WithdrawLimit");
        _transferLimit = configuration.GetValue<decimal>("BankingSettings:TransferLimit");
        _dailyWithdrawLimit = configuration.GetValue<decimal>("BankingSettings:DailyWithdrawLimit");
        _dailyWithdrawCountLimit = configuration.GetValue<int>("BankingSettings:DailyWithdrawCountLimit");
        _dailyTransferCountLimit = configuration.GetValue<int>("BankingSettings:DailyTransferCountLimit");
    }

    public async Task<Account> CreateAccountAsync(string accountNumber, string name, string phoneNumber, decimal initialBalance)
    {
        var account = new Account
        {
            AccountNumber = accountNumber,
            Name = name,
            PhoneNumber = phoneNumber,
            Balance = initialBalance,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Accounts.InsertOneAsync(account);

        if (initialBalance > 0)
        {
            await AddTransactionAsync(accountNumber, relatedAccountNumber: null,
                TransactionType.Deposit, initialBalance, account.Balance,
                "Initial balance deposited at account creation.");
        }

        return account;
    }

    public async Task<Account?> GetAccountAsync(string accountNumber)
    {
        var filter = Builders<Account>.Filter.Eq(a => a.AccountNumber, accountNumber);
        return await _context.Accounts.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<Account>> GetAllAccountsAsync()
    {
        return await _context.Accounts.Find(_ => true)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Account>> GetAccountsAsync(FilterDefinition<Account> filter)
    {
        return await _context.Accounts.Find(filter).ToListAsync();
    }

    public async Task<Account> EnsureAccountExistsAsync(string accountNumber)
    {
        var existing = await GetAccountAsync(accountNumber);
        if (existing != null)
            return existing;

        var account = new Account
        {
            AccountNumber = accountNumber,
            Name = "Registered User",
            PhoneNumber = string.Empty,
            Balance = 0,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Accounts.InsertOneAsync(account);
        return account;
    }

    public async Task<Account?> DepositAsync(string accountNumber, decimal amount)
    {
        var account = await GetAccountAsync(accountNumber);
        if (account == null)
            throw new InvalidOperationException("Account not found.");

        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Deposit failed. The account is frozen or inactive.");

        EnsureMeetsMinimumAmount(amount, "Deposit");

        account.Balance += amount;

        var filter = Builders<Account>.Filter.Eq(a => a.Id, account.Id);
        var update = Builders<Account>.Update.Set(a => a.Balance, account.Balance);
        var result = await _context.Accounts.UpdateOneAsync(filter, update);
        if (result.ModifiedCount == 0)
            throw new InvalidOperationException("Failed to update account balance.");

        await AddTransactionAsync(accountNumber, relatedAccountNumber: null,
            TransactionType.Deposit, amount, account.Balance, "Money deposited into account.");

        return account;
    }

    public async Task<Account?> WithdrawAsync(string accountNumber, decimal amount)
    {
        var account = await GetAccountAsync(accountNumber);
        if (account == null)
            throw new InvalidOperationException("Account not found.");

        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Withdrawal failed. The account is frozen or inactive.");

        if (amount > _withdrawLimit)
            throw new InvalidOperationException(
                $"Withdrawal failed. Amount exceeds the per-transaction limit of {_withdrawLimit:C}.");

        EnsureMeetsMinimumAmount(amount, "Withdrawal");

        await EnsureWithinDailyWithdrawalLimitAsync(accountNumber, amount);
        await EnsureWithinDailyWithdrawalCountLimitAsync(accountNumber);

        if (amount > account.Balance)
            throw new InvalidOperationException("Withdrawal failed due to insufficient balance.");

        account.Balance -= amount;

        var filter = Builders<Account>.Filter.Eq(a => a.Id, account.Id);
        var update = Builders<Account>.Update.Set(a => a.Balance, account.Balance);
        var result = await _context.Accounts.UpdateOneAsync(filter, update);
        if (result.ModifiedCount == 0)
            throw new InvalidOperationException("Failed to update account balance.");

        await AddTransactionAsync(accountNumber, relatedAccountNumber: null,
            TransactionType.Withdrawal, amount, account.Balance, "Money withdrawn from account.");

        return account;
    }

    public async Task<string> TransferAsync(string fromAccountNumber, string toAccountNumber, decimal amount, string? reference = null)
    {
        if (fromAccountNumber == toAccountNumber)
            throw new InvalidOperationException("Sender and receiver accounts cannot be the same.");

        if (amount > _transferLimit)
            throw new InvalidOperationException(
                $"Transfer failed. Amount exceeds the per-transaction limit of {_transferLimit:C}.");

        EnsureMeetsMinimumAmount(amount, "Transfer");

        var sender = await GetAccountAsync(fromAccountNumber);
        if (sender == null)
            throw new InvalidOperationException("Sender account not found.");

        var receiver = await GetAccountAsync(toAccountNumber);
        if (receiver == null)
            throw new InvalidOperationException("Receiver account not found.");

        if (!string.Equals(sender.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Transfer failed. The sender account is frozen or inactive.");

        if (!string.Equals(receiver.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Transfer failed. The receiver account is frozen or inactive.");

        await EnsureWithinDailyTransferCountLimitAsync(fromAccountNumber);

        if (amount > sender.Balance)
            throw new InvalidOperationException("Transfer failed due to insufficient balance in the sender account.");

        try
        {
            using var session = await _client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                await ApplyTransferAsync(session, sender, receiver, fromAccountNumber, toAccountNumber, amount, reference);
                await session.CommitTransactionAsync();
            }
            catch
            {
                await session.AbortTransactionAsync();
                throw;
            }
        }
        catch (Exception ex) when (IsTransactionUnsupported(ex))
        {
            // Cluster does not support multi-document transactions (e.g. free/shared tiers).
            await ApplyTransferAsync(null, sender, receiver, fromAccountNumber, toAccountNumber, amount, reference);
        }

        return toAccountNumber;
    }

    private static bool IsTransactionUnsupported(Exception ex)
    {
        if (ex is not MongoCommandException commandException)
            return false;

        var message = commandException.ErrorMessage ?? string.Empty;
        return message.Contains("Transaction numbers are only allowed",
                StringComparison.OrdinalIgnoreCase)
            || message.Contains("replica set member or mongos", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplyTransferAsync(
        IClientSessionHandle? session,
        Account sender, Account receiver,
        string fromAccountNumber, string toAccountNumber, decimal amount, string? reference = null)
    {
        sender.Balance -= amount;
        receiver.Balance += amount;

        var senderNote = $"Transferred to account {toAccountNumber}.";
        if (!string.IsNullOrWhiteSpace(reference))
            senderNote = $"{senderNote.Trim()} Reference: {reference.Trim()}.";
        var receiverNote = $"Received from account {fromAccountNumber}.";

        var senderFilter = Builders<Account>.Filter.Eq(a => a.Id, sender.Id);
        var receiverFilter = Builders<Account>.Filter.Eq(a => a.Id, receiver.Id);
        var senderUpdate = Builders<Account>.Update.Set(a => a.Balance, sender.Balance);
        var receiverUpdate = Builders<Account>.Update.Set(a => a.Balance, receiver.Balance);

        if (session != null)
        {
            await _context.Accounts.UpdateOneAsync(session, senderFilter, senderUpdate);
            await _context.Accounts.UpdateOneAsync(session, receiverFilter, receiverUpdate);

            await AddTransactionAsync(session, fromAccountNumber, toAccountNumber,
                TransactionType.Transfer, amount, sender.Balance, senderNote);
            await AddTransactionAsync(session, toAccountNumber, fromAccountNumber,
                TransactionType.Transfer, amount, receiver.Balance, receiverNote);
        }
        else
        {
            await _context.Accounts.UpdateOneAsync(senderFilter, senderUpdate);
            await _context.Accounts.UpdateOneAsync(receiverFilter, receiverUpdate);

            await AddTransactionAsync(fromAccountNumber, toAccountNumber,
                TransactionType.Transfer, amount, sender.Balance, senderNote);
            await AddTransactionAsync(toAccountNumber, fromAccountNumber,
                TransactionType.Transfer, amount, receiver.Balance, receiverNote);
        }
    }

    public async Task<List<Transaction>> GetTransactionsAsync(string accountNumber)
    {
        var filter = Builders<Transaction>.Filter.Eq(t => t.AccountNumber, accountNumber);
        return await _context.Transactions.Find(filter)
            .SortByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalDepositAsync(string accountNumber)
    {
        var filter = Builders<Transaction>.Filter.Eq(t => t.AccountNumber, accountNumber)
            & Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Deposit);
        var deposits = await _context.Transactions.Find(filter).ToListAsync();
        return deposits.Sum(t => t.Amount);
    }

    public async Task<decimal> GetTotalWithdrawAsync(string accountNumber)
    {
        var filter = Builders<Transaction>.Filter.Eq(t => t.AccountNumber, accountNumber)
            & Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Withdrawal);
        var withdrawals = await _context.Transactions.Find(filter).ToListAsync();
        return withdrawals.Sum(t => t.Amount);
    }

    public async Task<(List<Account> Items, long Total)> GetAccountsPagedAsync(string? search, string? status, int page, int pageSize)
    {
        var builder = Builders<Account>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(search.Trim(), "i");
            filter &= builder.Or(
                builder.Regex(a => a.AccountNumber, regex),
                builder.Regex(a => a.Name, regex),
                builder.Regex(a => a.PhoneNumber, regex));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            filter &= builder.Eq(a => a.Status, status);
        }

        var total = await _context.Accounts.CountDocumentsAsync(filter);
        var items = await _context.Accounts.Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<long> GetTotalAccountsAsync()
    {
        return await _context.Accounts.CountDocumentsAsync(_ => true);
    }

    public async Task<decimal> GetSystemBalanceAsync()
    {
        var accounts = await _context.Accounts.Find(_ => true).ToListAsync();
        return accounts.Sum(a => a.Balance);
    }

    public async Task<decimal> GetTotalDepositSystemAsync()
    {
        var filter = Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Deposit);
        var deposits = await _context.Transactions.Find(filter).ToListAsync();
        return deposits.Sum(t => t.Amount);
    }

    public async Task<decimal> GetTotalWithdrawSystemAsync()
    {
        var filter = Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Withdrawal);
        var withdrawals = await _context.Transactions.Find(filter).ToListAsync();
        return withdrawals.Sum(t => t.Amount);
    }

    public async Task<decimal> GetTotalTransferredAsync()
    {
        var filter = Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Transfer)
            & Builders<Transaction>.Filter.Regex(t => t.Note, new MongoDB.Bson.BsonRegularExpression("^Transferred", "i"));
        var transfers = await _context.Transactions.Find(filter).ToListAsync();
        return transfers.Sum(t => t.Amount);
    }

    public async Task<(List<Transaction> Items, long Total)> GetTransactionsPagedAsync(
        string? type, string? status, string? accountNumber, string? search,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize, string? notePrefix = null)
    {
        var builder = Builders<Transaction>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(type) && type != "All" && Enum.TryParse<TransactionType>(type, true, out var typeEnum))
        {
            filter &= builder.Eq(t => t.Type, typeEnum);
        }

        if (!string.IsNullOrWhiteSpace(notePrefix))
        {
            var prefixRegex = new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(notePrefix)}", "i");
            filter &= builder.Regex(t => t.Note, prefixRegex);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            filter &= builder.Eq(t => t.Status, status);
        }

        if (!string.IsNullOrWhiteSpace(accountNumber))
        {
            filter &= builder.Or(
                builder.Eq(t => t.AccountNumber, accountNumber.Trim()),
                builder.Eq(t => t.RelatedAccountNumber, accountNumber.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(search.Trim(), "i");
            filter &= builder.Or(
                builder.Regex(t => t.AccountNumber, regex),
                builder.Regex(t => t.RelatedAccountNumber, regex),
                builder.Regex(t => t.Note, regex));
        }

        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            filter &= builder.Gte(t => t.Date, fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            filter &= builder.Lt(t => t.Date, toUtc);
        }

        var total = await _context.Transactions.CountDocumentsAsync(filter);
        var items = await _context.Transactions.Find(filter)
            .SortByDescending(t => t.Date)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(string id)
    {
        var filter = Builders<Transaction>.Filter.Eq(t => t.Id, id);
        return await _context.Transactions.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<Account?> SetAccountStatusAsync(string accountNumber, string status)
    {
        var account = await GetAccountAsync(accountNumber);
        if (account == null)
            throw new InvalidOperationException("Account not found.");

        account.Status = status;

        var filter = Builders<Account>.Filter.Eq(a => a.Id, account.Id);
        var update = Builders<Account>.Update.Set(a => a.Status, status);
        await _context.Accounts.UpdateOneAsync(filter, update);

        return account;
    }

    private void EnsureMeetsMinimumAmount(decimal amount, string action)
    {
        if (_minimumTransactionAmount > 0 && amount < _minimumTransactionAmount)
            throw new InvalidOperationException(
                $"{action} failed. The minimum amount is {_minimumTransactionAmount:C}.");
    }

    private async Task EnsureWithinDailyWithdrawalLimitAsync(string accountNumber, decimal amount)
    {
        if (_dailyWithdrawLimit <= 0) return;

        var startOfDay = DateTime.UtcNow.Date;
        var endOfDay = startOfDay.AddDays(1);

        var filter = Builders<Transaction>.Filter.Eq(t => t.AccountNumber, accountNumber)
            & Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Withdrawal)
            & Builders<Transaction>.Filter.Gte(t => t.Date, startOfDay)
            & Builders<Transaction>.Filter.Lt(t => t.Date, endOfDay);

        var todaysWithdrawals = await _context.Transactions.Find(filter).ToListAsync();
        var usedToday = todaysWithdrawals.Sum(t => t.Amount);

        if (usedToday + amount > _dailyWithdrawLimit)
            throw new InvalidOperationException(
                $"Withdrawal failed. Daily withdrawal limit is {_dailyWithdrawLimit:C}. " +
                $"You have already withdrawn {usedToday:C} today; adding {amount:C} would exceed the limit.");
    }

    private async Task EnsureWithinDailyWithdrawalCountLimitAsync(string accountNumber)
    {
        if (_dailyWithdrawCountLimit <= 0) return;

        var startOfDay = DateTime.UtcNow.Date;
        var endOfDay = startOfDay.AddDays(1);

        var filter = Builders<Transaction>.Filter.Eq(t => t.AccountNumber, accountNumber)
            & Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Withdrawal)
            & Builders<Transaction>.Filter.Gte(t => t.Date, startOfDay)
            & Builders<Transaction>.Filter.Lt(t => t.Date, endOfDay);

        var countToday = await _context.Transactions.CountDocumentsAsync(filter);

        if (countToday >= _dailyWithdrawCountLimit)
            throw new InvalidOperationException(
                $"Withdrawal failed. Daily withdrawal limit of {_dailyWithdrawCountLimit} has been reached. " +
                "Please try again tomorrow.");
    }

    private async Task EnsureWithinDailyTransferCountLimitAsync(string accountNumber)
    {
        if (_dailyTransferCountLimit <= 0) return;

        var startOfDay = DateTime.UtcNow.Date;
        var endOfDay = startOfDay.AddDays(1);

        var filter = Builders<Transaction>.Filter.Eq(t => t.AccountNumber, accountNumber)
            & Builders<Transaction>.Filter.Eq(t => t.Type, TransactionType.Transfer)
            & Builders<Transaction>.Filter.Regex(t => t.Note, "^Transferred")
            & Builders<Transaction>.Filter.Gte(t => t.Date, startOfDay)
            & Builders<Transaction>.Filter.Lt(t => t.Date, endOfDay);

        var countToday = await _context.Transactions.CountDocumentsAsync(filter);

        if (countToday >= _dailyTransferCountLimit)
            throw new InvalidOperationException(
                $"Transfer failed. Daily transfer limit of {_dailyTransferCountLimit} has been reached. " +
                "Please try again tomorrow.");
    }

    private async Task AddTransactionAsync(string accountNumber, string? relatedAccountNumber,
        TransactionType type, decimal amount, decimal balanceAfter, string note)
    {
        await AddTransactionAsync(null, accountNumber, relatedAccountNumber,
            type, amount, balanceAfter, note);
    }

    private async Task AddTransactionAsync(IClientSessionHandle? session, string accountNumber,
        string? relatedAccountNumber, TransactionType type, decimal amount, decimal balanceAfter, string note)
    {
        var transaction = new Transaction
        {
            AccountNumber = accountNumber,
            RelatedAccountNumber = relatedAccountNumber,
            Type = type,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Date = DateTime.UtcNow,
            Note = note,
            Status = "Completed"
        };

        if (session != null)
            await _context.Transactions.InsertOneAsync(session, transaction);
        else
            await _context.Transactions.InsertOneAsync(transaction);
    }
}
