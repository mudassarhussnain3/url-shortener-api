# URL Shortener API

An ASP.NET Core (.NET 10) Web API that turns long URLs into short codes and redirects visitors back to the original. Built as interview practice.

## Features

- **JWT authentication** – register/login, bearer tokens (1 hour), passwords hashed with ASP.NET Identity's `PasswordHasher`
- **Base62 short codes** – compact `[0-9a-zA-Z]` codes derived from the database ID
- **Caching** – `IMemoryCache` (size-limited) on the redirect path to avoid a DB hit per click
- **Rate limiting** – per-user fixed window (10 short URLs/min), returns `429` with `Retry-After`
- **Pagination** – `GET /api/urls/my-urls?page=1&pageSize=10`
- **Exception-handling middleware** – consistent JSON errors, plus request-logging middleware
- **Background click tracking** – clicks go through an in-memory queue and are written by a hosted service
- EF Core + SQL Server, Swagger UI in Development

## Endpoints

| Method | Route | Auth |
|---|---|---|
| POST | `/api/auth/register` | – |
| POST | `/api/auth/login` | – |
| POST | `/api/urls/shorten` | Bearer |
| GET | `/api/urls/my-urls` | Bearer |
| GET | `/{shortCode}` | – (redirects) |

## How to run

Prerequisites: .NET 10 SDK, SQL Server (LocalDB works).

```bash
cd WebApplication1

# Secrets are NOT in the repo. Set them with user secrets:
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=UrlShortenerDb;Trusted_Connection=True;"
dotnet user-secrets set "Jwt:Key" "<a-random-string-of-at-least-32-characters>"

dotnet ef database update   # apply migrations
dotnet run --launch-profile https
```

Then open Swagger at `https://localhost:7000/swagger`. Register, log in, click **Authorize** and paste the token.

(Alternatively, create a git-ignored `appsettings.Development.json` with the same keys, or use environment variables such as `Jwt__Key`.)

## How I'd scale this

- **Load balancer** – run multiple stateless API instances behind a load balancer (JWT is stateless, so no sticky sessions).
- **Redis** – replace `IMemoryCache` with a distributed cache so all instances share hot redirects; move rate-limit counters there too, and swap the in-process click queue for a durable one (Redis Streams / a message broker).
- **Read replicas** – redirects are read-heavy: send them to read replicas and keep writes on the primary. Shard by short code if one primary is still too much.
- **ID generation** – at multi-node scale, use a range-allocating ID service or snowflake-style IDs rather than a single DB identity column.
- **Batch click writes** – aggregate click counts before writing to reduce write load.
