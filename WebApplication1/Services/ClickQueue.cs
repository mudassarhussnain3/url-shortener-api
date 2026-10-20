using System.Threading.Channels;

namespace WebApplication1.Services
{
    // In-memory queue of short codes that were clicked.
    // Registered as a Singleton so every request and the background worker share the SAME queue.
    public class ClickQueue
    {
        private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

        // Called by the redirect: instant, no DB work
        public void Enqueue(string shortCode)
        {
            _channel.Writer.TryWrite(shortCode);
        }

        // Called by the background worker: waits until a code arrives, then yields it
        public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
