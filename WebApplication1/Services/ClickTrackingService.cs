using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Services
{
    // Runs for the whole life of the app, incrementing ClickCount for each code in the queue.
    public class ClickTrackingService : BackgroundService
    {
        private readonly ClickQueue _clickQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ClickTrackingService> _logger;

        // No AppDbContext here: this service lives forever, AppDbContext is Scoped (one per request).
        // We ask the scope factory for a fresh one per click instead.
        public ClickTrackingService(ClickQueue clickQueue, IServiceScopeFactory scopeFactory, ILogger<ClickTrackingService> logger)
        {
            _clickQueue = clickQueue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Waits (without blocking a thread) until a code arrives; ends when the app shuts down
            await foreach (var shortCode in _clickQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Your atomic increment: UPDATE ShortUrls SET ClickCount = ClickCount + 1 WHERE ShortCode = @code
                    await db.ShortUrls
                        .Where(s => s.ShortCode == shortCode)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.ClickCount, p => p.ClickCount + 1), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One failed update must not kill the loop, or no more clicks would ever be counted
                    _logger.LogError(ex, "Failed to record click for {ShortCode}", shortCode);
                }
            }
        }
    }
}
