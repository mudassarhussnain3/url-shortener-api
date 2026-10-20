using WebApplication1.DTOs;

namespace WebApplication1.Services
{
    public interface IShortenUrlService
    {
        Task<ShortenUrlResponse> ShortenAsync(ShortenUrlRequest request, int userId);
        Task<string?> GetLongUrlAsync(string shortCode);
        Task<PagedResult<UrlSummary>> GetUserUrlsAsync(int userId, PaginationQuery query);
    }
}
