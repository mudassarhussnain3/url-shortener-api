using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
namespace WebApplication1.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<ShortUrl> ShortUrls { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Name).HasMaxLength(100);
                entity.Property(u => u.Email).HasMaxLength(256);
                entity.Property(u => u.PasswordHash).HasMaxLength(256);
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<ShortUrl>(entity =>
            {
                entity.Property(s => s.ShortCode).HasMaxLength(20);
                entity.Property(s => s.LongUrl).HasMaxLength(2048);
                entity.HasIndex(s => s.ShortCode).IsUnique();

                entity.HasOne(s => s.User)
                      .WithMany(u => u.ShortUrls)
                      .HasForeignKey(s => s.UserId);
            });
        }
    }
}
