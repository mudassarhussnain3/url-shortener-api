using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class ShortenUrlService : IShortenUrlService
    {
        private const string Base62Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ClickQueue _clickQueue;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ShortenUrlService> _logger;

        // Scoped service depending on Singletons (ClickQueue, IMemoryCache) is fine (the reverse is the problem)
        public ShortenUrlService(AppDbContext db, IConfiguration config, ClickQueue clickQueue,
            IMemoryCache cache, ILogger<ShortenUrlService> logger)
        {
            _db = db;
            _config = config;
            _clickQueue = clickQueue;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ShortenUrlResponse> ShortenAsync(ShortenUrlRequest request, int userId)
        {
            var shortUrl = new ShortUrl
            {
                LongUrl = request.LongUrl,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                // Temporary unique value: ShortCode has a unique index, so "" would clash between concurrent inserts
                ShortCode = Guid.NewGuid().ToString("N")[..20]
            };

            _db.ShortUrls.Add(shortUrl);
            await _db.SaveChangesAsync();          // INSERT: SQL Server generates shortUrl.Id

            shortUrl.ShortCode = ToBase62(shortUrl.Id);
            await _db.SaveChangesAsync();          // UPDATE: replace the placeholder with the real code

            return new ShortenUrlResponse
            {
                ShortCode = shortUrl.ShortCode,
                ShortUrl = $"{_config["App:BaseUrl"]}/{shortUrl.ShortCode}"
            };
        }

        public async Task<string?> GetLongUrlAsync(string shortCode)
        {
            var cacheKey = $"url:{shortCode}";   // prefix keeps keys unique if we cache other things later

            // 1. Look in memory first
            if (_cache.TryGetValue(cacheKey, out string? longUrl))
            {
                _logger.LogInformation("Cache HIT for {ShortCode}", shortCode);
            }
            else
            {
                // 2. Not in memory: read the long URL from the DB (no tracking, only the LongUrl column)
                _logger.LogInformation("Cache MISS for {ShortCode}, reading from DB", shortCode);

                longUrl = await _db.ShortUrls
                    .AsNoTracking()
                    .Where(s => s.ShortCode == shortCode)
                    .Select(s => s.LongUrl)
                    .FirstOrDefaultAsync();

                if (longUrl is null)
                {
                    return null;   // unknown code: nothing to cache
                }

                // 3. Remember it for next time. Safe because a short URL never changes.
                _cache.Set(cacheKey, longUrl, new MemoryCacheEntryOptions
                {
                    Size = 1,                                              // required because SizeLimit is set
                    SlidingExpiration = TimeSpan.FromHours(1),             // dropped after 1 hour without clicks
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) // and at most 1 day, even if popular
                });
            }

            // Don't update the DB here: just queue the click (instant).
            // ClickTrackingService increments ClickCount in the background, after the redirect is sent.
            _clickQueue.Enqueue(shortCode);

            return longUrl;
        }

        public async Task<PagedResult<UrlSummary>> GetUserUrlsAsync(int userId, PaginationQuery query)
        {
            // Only this user's rows; read-only
            var userUrls = _db.ShortUrls
                .AsNoTracking()
                .Where(s => s.UserId == userId);

            // Query 1: total rows, so the client knows how many pages exist
            var totalCount = await userUrls.CountAsync();

            var baseUrl = _config["App:BaseUrl"];

            // Query 2: just one page. OrderBy BEFORE Skip/Take, or page contents are random.
            // ThenBy Id breaks ties when two rows have the same CreatedAt.
            var items = await userUrls
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Skip((query.Page - 1) * query.PageSize)   // page 3, size 10 → skip 20
                .Take(query.PageSize)                       // → take 10
                .Select(s => new UrlSummary
                {
                    ShortCode = s.ShortCode,
                    ShortUrl = baseUrl + "/" + s.ShortCode,
                    LongUrl = s.LongUrl,
                    ClickCount = s.ClickCount,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<UrlSummary>
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };
        }

        // Base62: e.g. 1 -> "1", 62 -> "10", 125 -> "21"
        private static string ToBase62(long number)
        {
            if (number == 0) return Base62Chars[0].ToString();

            var result = string.Empty;
            while (number > 0)
            {
                result = Base62Chars[(int)(number % 62)] + result;   // remainder becomes the next digit, from the right
                number /= 62;
            }
            return result;
        }
    }
}
