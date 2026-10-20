using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs
{
    public class ShortenUrlRequest
    {
        [Required]
        [Url]
        [MaxLength(2048)]
        public string LongUrl { get; set; } = string.Empty;
    }

    public class ShortenUrlResponse
    {
        public string ShortCode { get; set; } = string.Empty;
        public string ShortUrl { get; set; } = string.Empty;
    }
}
