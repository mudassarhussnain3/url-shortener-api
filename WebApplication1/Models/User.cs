namespace WebApplication1.Models
{
    public class User
    {
     public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public ICollection<ShortUrl> ShortUrls { get; set; } = new List<ShortUrl>();
    }
}
