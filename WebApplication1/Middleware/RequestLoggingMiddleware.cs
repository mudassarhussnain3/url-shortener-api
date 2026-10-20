using System.Diagnostics;

namespace WebApplication1.Middleware
{
    // Writes one log line per request: method, path, status code and how long it took.
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // ON THE WAY IN: start timing
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);   // everything else runs here (other middleware, controller, service, DB)
            }
            finally
            {
                // ON THE WAY OUT: "finally" runs whether the request succeeded or threw,
                // so failed requests get logged too
                stopwatch.Stop();

                // Placeholders ({Method}...) not $"..." so each value stays a separate, searchable field
                _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
