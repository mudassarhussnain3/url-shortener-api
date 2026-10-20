using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs
{
    // Query string input: /api/urls/my-urls?page=2&pageSize=10
    public class PaginationQuery
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 50)]   // cap the page size so nobody can request 1,000,000 rows at once
        public int PageSize { get; set; } = 10;
    }

    // One row in the list (never return the entity itself)
    public class UrlSummary
    {
        public string ShortCode { get; set; } = string.Empty;
        public string ShortUrl { get; set; } = string.Empty;
        public string LongUrl { get; set; } = string.Empty;
        public long ClickCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Generic page wrapper: reusable for any list endpoint (PagedResult<UrlSummary>, PagedResult<User>, ...)
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
