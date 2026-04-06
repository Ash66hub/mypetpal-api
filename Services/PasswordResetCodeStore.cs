using Microsoft.Extensions.Caching.Memory;
using mypetpal.Services.Contracts;

namespace mypetpal.Services
{
    public class PasswordResetCodeStore : IPasswordResetCodeStore
    {
        private sealed class Entry
        {
            public required string Code { get; set; }
            public required DateTime ExpiresAtUtc { get; set; }
        }

        private readonly IMemoryCache _cache;

        public PasswordResetCodeStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task StoreCodeAsync(string email, string code, TimeSpan ttl)
        {
            var normalizedEmail = NormalizeEmail(email);
            var expiresAtUtc = DateTime.UtcNow.Add(ttl);

            _cache.Set(
                GetKey(normalizedEmail),
                new Entry { Code = code, ExpiresAtUtc = expiresAtUtc },
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                });

            return Task.CompletedTask;
        }

        public bool VerifyCode(string email, string code)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (!_cache.TryGetValue<Entry>(GetKey(normalizedEmail), out var entry) || entry == null)
            {
                return false;
            }

            if (entry.ExpiresAtUtc < DateTime.UtcNow)
            {
                _cache.Remove(GetKey(normalizedEmail));
                return false;
            }

            return string.Equals(entry.Code, code, StringComparison.Ordinal);
        }

        public void RemoveCode(string email)
        {
            var normalizedEmail = NormalizeEmail(email);
            _cache.Remove(GetKey(normalizedEmail));
        }

        private static string GetKey(string normalizedEmail) => $"password-reset:{normalizedEmail}";

        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
