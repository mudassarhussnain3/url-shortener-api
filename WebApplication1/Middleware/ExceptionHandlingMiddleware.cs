using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Middleware
{
    // Catches any exception thrown further down the pipeline (controllers, services, EF)
    // and turns it into one consistent JSON error instead of a stack trace page.
    public class ExceptionHandlingMiddleware
    {
        // _next = "the rest of the pipeline" (the next middleware, eventually the controller)
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        // Middleware is created ONCE at startup, so only Singleton-safe things go in the constructor
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        // Runs on EVERY request
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);   // run everything after this middleware
            }
            catch (Exception ex)
            {
                // Full details go to the log only, never to the client
                _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

                // If part of the response was already sent, we can't change it anymore: let the server abort it
                if (context.Response.HasStarted)
                    throw;

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                // ProblemDetails = the standard JSON error shape (RFC 9457), same shape [ApiController] uses for 400s
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Instance = context.Request.Path,
                    // Show the real message only on your machine; in production it could leak internals
                    Detail = _env.IsDevelopment() ? ex.Message : null
                };
                // traceId lets you find this exact error in the logs when a user reports it
                problem.Extensions["traceId"] = context.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
            }
        }
    }
}
