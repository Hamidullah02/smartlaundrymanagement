using Microsoft.Extensions.Caching.Memory;

namespace LaundryMVC.Services;

// Purpose: hold short-lived 6-digit codes keyed by email + purpose.
// Lives in memory only — restarting the server clears all pending codes (fine for dev).
public class OtpService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public OtpService(IMemoryCache cache) => _cache = cache;

    public enum Purpose { Register, ResetPassword }

    // Generate a fresh code, store it, return it (the controller emails it).
    public string Generate(string email, Purpose purpose)
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        _cache.Set(Key(email, purpose), code, Lifetime);
        return code;
    }

    // Returns true exactly once: the code is removed on a successful verify.
    public bool Verify(string email, Purpose purpose, string input)
    {
        var key = Key(email, purpose);
        if (!_cache.TryGetValue(key, out string? stored)) return false;
        if (!string.Equals(stored, input, StringComparison.Ordinal)) return false;
        _cache.Remove(key);
        return true;
    }

    private static string Key(string email, Purpose purpose) => $"otp:{purpose}:{email.ToLowerInvariant()}";
}
