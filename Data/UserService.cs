using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using OnlineBankingSystem.Models;

namespace OnlineBankingSystem.Data;

public interface IUserService
{
    Task<AppUser?> RegisterAsync(SignupViewModel model);
    Task<(bool Success, AppUser? User, string Message)> ValidateLoginAsync(string mobileNumber, string password);
    Task<AppUser?> GetUserByMobileAsync(string mobileNumber);
    Task<AppUser?> GetUserByIdAsync(string id);
    Task<AppUser?> GetUserByAccountNumberAsync(string accountNumber);
    Task<List<AppUser>> GetAllUsersAsync();
    Task<(List<AppUser> Items, long Total)> GetUsersPagedAsync(string? search, string? status, int page, int pageSize);
    Task<long> GetTotalUsersAsync();
    Task<AppUser?> UpdateProfileAsync(string userId, ProfileViewModel model, string? profilePictureUrl = null);
    Task<AppUser?> UpdateUserByAdminAsync(string userId, AdminEditUserViewModel model);
    Task<AppUser?> SetUserStatusAsync(string userId, string status);
    Task EnsureAdminSeededAsync();
}

public class UserService : IUserService
{
    private readonly MongoDbContext _context;
    private readonly PasswordHasher<AppUser> _passwordHasher;
    private readonly IAccountService _accountService;

    public UserService(MongoDbContext context, IAccountService accountService)
    {
        _context = context;
        _accountService = accountService;
        _passwordHasher = new PasswordHasher<AppUser>();
    }

    public async Task<AppUser?> RegisterAsync(SignupViewModel model)
    {
        model.MobileNumber = model.MobileNumber.Trim();
        model.AccountNumber = model.AccountNumber.Trim();
        model.Email = model.Email.Trim().ToLowerInvariant();

        if (await GetUserByMobileAsync(model.MobileNumber) != null)
            throw new InvalidOperationException("An account already exists with this mobile number.");

        var emailFilter = Builders<AppUser>.Filter.Eq(u => u.Email, model.Email);
        if (await _context.Users.Find(emailFilter).FirstOrDefaultAsync() != null)
            throw new InvalidOperationException("An account already exists with this email address.");

        var accountFilter = Builders<AppUser>.Filter.Eq(u => u.AccountNumber, model.AccountNumber);
        if (await _context.Users.Find(accountFilter).FirstOrDefaultAsync() != null)
            throw new InvalidOperationException("This account number is already registered.");

        await _accountService.EnsureAccountExistsAsync(model.AccountNumber);

        var user = new AppUser
        {
            AccountNumber = model.AccountNumber,
            MobileNumber = model.MobileNumber,
            Email = model.Email,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        await _context.Users.InsertOneAsync(user);
        return user;
    }

    public async Task<(bool Success, AppUser? User, string Message)> ValidateLoginAsync(string mobileNumber, string password)
    {
        var user = await GetUserByMobileAsync(mobileNumber.Trim());
        if (user == null)
            return (false, null, "Invalid mobile number or password.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return (false, null, "Invalid mobile number or password.");

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return (false, null, "Your account has been deactivated. Please contact support.");

        // Rehash if needed (e.g., upgraded hash format)
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            var filter = Builders<AppUser>.Filter.Eq(u => u.Id, user.Id);
            var update = Builders<AppUser>.Update.Set(u => u.PasswordHash, user.PasswordHash);
            await _context.Users.UpdateOneAsync(filter, update);
        }

        return (true, user, "Login successful.");
    }

    public async Task<AppUser?> GetUserByMobileAsync(string mobileNumber)
    {
        var filter = Builders<AppUser>.Filter.Eq(u => u.MobileNumber, mobileNumber.Trim());
        return await _context.Users.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<AppUser?> GetUserByIdAsync(string id)
    {
        var filter = Builders<AppUser>.Filter.Eq(u => u.Id, id);
        return await _context.Users.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        return await _context.Users.Find(_ => true)
            .SortByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<AppUser?> GetUserByAccountNumberAsync(string accountNumber)
    {
        var filter = Builders<AppUser>.Filter.Eq(u => u.AccountNumber, accountNumber.Trim());
        return await _context.Users.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<(List<AppUser> Items, long Total)> GetUsersPagedAsync(string? search, string? status, int page, int pageSize)
    {
        var builder = Builders<AppUser>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(search.Trim(), "i");
            filter &= builder.Or(
                builder.Regex(u => u.FirstName, regex),
                builder.Regex(u => u.LastName, regex),
                builder.Regex(u => u.MobileNumber, regex),
                builder.Regex(u => u.Email, regex),
                builder.Regex(u => u.AccountNumber, regex));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            filter &= builder.Eq(u => u.Status, status);
        }

        var total = await _context.Users.CountDocumentsAsync(filter);
        var items = await _context.Users.Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<long> GetTotalUsersAsync()
    {
        return await _context.Users.CountDocumentsAsync(_ => true);
    }

    public async Task<AppUser?> UpdateUserByAdminAsync(string userId, AdminEditUserViewModel model)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        var mobile = model.MobileNumber.Trim();
        var email = model.Email.Trim().ToLowerInvariant();

        var mobileFilter = Builders<AppUser>.Filter.Eq(u => u.MobileNumber, mobile)
            & Builders<AppUser>.Filter.Ne(u => u.Id, user.Id);
        if (await _context.Users.Find(mobileFilter).FirstOrDefaultAsync() != null)
            throw new InvalidOperationException("This mobile number is already used by another account.");

        var emailFilter = Builders<AppUser>.Filter.Eq(u => u.Email, email)
            & Builders<AppUser>.Filter.Ne(u => u.Id, user.Id);
        if (await _context.Users.Find(emailFilter).FirstOrDefaultAsync() != null)
            throw new InvalidOperationException("This email address is already used by another account.");

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.Email = email;
        user.MobileNumber = mobile;
        user.Address = model.Address.Trim();

        var filter = Builders<AppUser>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<AppUser>.Update
            .Set(u => u.FirstName, user.FirstName)
            .Set(u => u.LastName, user.LastName)
            .Set(u => u.Email, user.Email)
            .Set(u => u.MobileNumber, user.MobileNumber)
            .Set(u => u.Address, user.Address);
        await _context.Users.UpdateOneAsync(filter, update);

        var account = await _accountService.GetAccountAsync(user.AccountNumber);
        if (account != null)
        {
            var accountFilter = Builders<Account>.Filter.Eq(a => a.Id, account.Id);
            var accountUpdate = Builders<Account>.Update
                .Set(a => a.Name, user.FullName)
                .Set(a => a.PhoneNumber, user.MobileNumber);
            await _context.Accounts.UpdateOneAsync(accountFilter, accountUpdate);
        }

        return user;
    }

    public async Task<AppUser?> SetUserStatusAsync(string userId, string status)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An administrator account cannot be activated or deactivated.");

        user.Status = status;

        var filter = Builders<AppUser>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<AppUser>.Update.Set(u => u.Status, status);
        await _context.Users.UpdateOneAsync(filter, update);

        return user;
    }

    public async Task<AppUser?> UpdateProfileAsync(string userId, ProfileViewModel model, string? profilePictureUrl = null)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        var mobile = model.MobileNumber.Trim();

        var mobileFilter = Builders<AppUser>.Filter.Eq(u => u.MobileNumber, mobile)
            & Builders<AppUser>.Filter.Ne(u => u.Id, user.Id);
        if (await _context.Users.Find(mobileFilter).FirstOrDefaultAsync() != null)
            throw new InvalidOperationException("This mobile number is already used by another account.");

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.MobileNumber = mobile;
        user.Address = model.Address.Trim();

        // Apply the profile picture change: null keeps the current value, "" clears it,
        // otherwise a new relative path is stored.
        var pictureChanged = profilePictureUrl is not null;
        if (pictureChanged)
            user.ProfilePictureUrl = profilePictureUrl!;

        var filter = Builders<AppUser>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<AppUser>.Update
            .Set(u => u.FirstName, user.FirstName)
            .Set(u => u.LastName, user.LastName)
            .Set(u => u.MobileNumber, user.MobileNumber)
            .Set(u => u.Address, user.Address);
        if (pictureChanged)
            update = update.Set(u => u.ProfilePictureUrl, user.ProfilePictureUrl);
        await _context.Users.UpdateOneAsync(filter, update);

        // Keep the linked banking account's display name/phone in sync.
        var account = await _accountService.GetAccountAsync(user.AccountNumber);
        if (account != null)
        {
            var accountFilter = Builders<Account>.Filter.Eq(a => a.Id, account.Id);
            var accountUpdate = Builders<Account>.Update
                .Set(a => a.Name, user.FullName)
                .Set(a => a.PhoneNumber, user.MobileNumber);
            await _context.Accounts.UpdateOneAsync(accountFilter, accountUpdate);
        }

        return user;
    }

    public async Task EnsureAdminSeededAsync()
    {
        var adminFilter = Builders<AppUser>.Filter.Eq(u => u.Role, "Admin");
        var existing = await _context.Users.Find(adminFilter).FirstOrDefaultAsync();
        if (existing != null)
            return;

        var adminUser = await GetUserByMobileAsync("01700000000");
        if (adminUser != null)
            return;

        var admin = new AppUser
        {
            AccountNumber = "10000000",
            MobileNumber = "01700000000",
            Email = "admin@onlinebank.com",
            FirstName = "Admin",
            LastName = "User",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = _passwordHasher.HashPassword(admin, "Admin@123");

        await _context.Users.InsertOneAsync(admin);
    }
}