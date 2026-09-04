using Microsoft.Extensions.Caching.Memory;

namespace OnlineBankingSystem.Data;

/// <summary>
/// Short-lived in-memory store for one-time passwords used in the
/// forgot-password flow. OTPs expire after 5 minutes, are usable only once,
/// and allow a maximum of 5 verification attempts.
/// </summary>
public class OtpService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 5;

    private readonly IMemoryCache _cache;

    public OtpService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Generate(string email)
    {
        var normalized = Normalize(email);
        var code = Random.Shared.Next(100000, 1000000).ToString();
        var entry = new OtpEntry { Code = code, ExpiresAt = DateTime.UtcNow.Add(Lifetime) };
        var existing = _cache.Get<OtpEntry>(Key(normalized));
        _cache.Remove(Key(normalized));
        _cache.Set(Key(normalized), entry, Lifetime);
        return code;
    }

    /// <summary>
    /// Verifies the OTP. Returns false if expired, already used, mismatched, or
    /// the attempt limit is exceeded. On a failed attempt the counter increments.
    /// </summary>
    public bool Verify(string email, string code, out string error)
    {
        var normalized = Normalize(email);
        error = string.Empty;

        var entry = _cache.Get<OtpEntry>(Key(normalized));
        if (entry == null)
        {
            error = "This verification code has expired. Please request a new one.";
            return false;
        }

        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            _cache.Remove(Key(normalized));
            error = "This verification code has expired. Please request a new one.";
            return false;
        }

        if (entry.Used)
        {
            error = "This verification code has already been used. Please request a new one.";
            return false;
        }

        if (!string.Equals(entry.Code, code.Trim(), StringComparison.Ordinal))
        {
            entry.AttemptsUsed++;
            if (entry.AttemptsUsed >= MaxAttempts)
            {
                _cache.Remove(Key(normalized));
                error = "Too many incorrect attempts. Please request a new verification code.";
            }
            else
            {
                _cache.Set(Key(normalized), entry, Lifetime);
                error = "The verification code is incorrect. Please try again.";
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Marks the OTP as used so it cannot be reused. Call after successful reset.
    /// </summary>
    public void Invalidate(string email)
    {
        _cache.Remove(Key(Normalize(email)));
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string Key(string email) => $"otp:{email}";

    private sealed class OtpEntry
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }
        public int AttemptsUsed { get; set; }
    }
}