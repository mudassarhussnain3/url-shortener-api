namespace WebApplication1.Models
{
    public class ShortUrl
    {
      public long Id { get; set; }
      public string ShortCode { get; set; } = String.Empty;
      public int UserId { get; set; }
      public DateTime CreatedAt { get; set; }
      public long ClickCount { get; set; }
      public string LongUrl { get; set; } = string.Empty;
      public User User { get; set; } = null!;
    }
}
