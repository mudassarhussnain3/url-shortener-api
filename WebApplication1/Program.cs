using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WebApplication1.Data;
using WebApplication1.Middleware;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Adds the "Authorize" button: paste the token from /api/auth/login (without "Bearer ")
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the token from /api/auth/login"
    });

    // Sends that token as "Authorization: Bearer <token>" on every request from Swagger
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IShortenUrlService, ShortenUrlService>();
// IMemoryCache (Singleton): an in-memory key/value store shared by all requests.
// SizeLimit caps it at 10,000 entries so memory can't grow forever (each entry declares Size = 1).
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);
builder.Services.AddSingleton<ClickQueue>();                   // one shared queue for the whole app
builder.Services.AddHostedService<ClickTrackingService>();      // starts with the app, runs in the background

// How to validate incoming tokens: same Issuer/Audience/Key that AuthService uses to create them
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero   // default is 5 min of grace; we want exactly 1 hour
        };
    });

// Rate limiting: a named policy that endpoints opt into with [EnableRateLimiting("shorten-per-user")]
builder.Services.AddRateLimiter(options =>
{
    // Default rejection status is 503; 429 Too Many Requests is the correct one
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("shorten-per-user", httpContext =>
        // "Partition" = a separate counter per key. Key = user Id from the JWT, so each user gets their own 10/min.
        // (Falls back to IP for anonymous callers, but [Authorize] already blocks those on shorten.)
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,                    // 10 requests...
                Window = TimeSpan.FromMinutes(1),    // ...per 1-minute window, then the counter resets
                QueueLimit = 0                       // over the limit → reject immediately, don't wait in line
            }));

    // Friendly JSON body + Retry-After header instead of an empty 429
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many requests. Limit is 10 short URLs per minute." }, cancellationToken);
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// 1st (outermost): logs every request. It sits OUTSIDE the exception handler,
// so after a crash it sees the final 500 the handler wrote, not the default 200.
app.UseMiddleware<RequestLoggingMiddleware>();

// 2nd: catches exceptions from everything registered AFTER it
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();   // who are you? reads the Bearer token and fills HttpContext.User
app.UseAuthorization();    // are you allowed? enforces [Authorize]
app.UseRateLimiter();      // must come AFTER UseAuthentication, or User is empty and every user shares one counter

app.MapControllers();

// Development only: throws on purpose to test ExceptionHandlingMiddleware
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/debug/throw", () =>
    {
        throw new InvalidOperationException("Test exception from /api/debug/throw");
    });
}

app.Run();
