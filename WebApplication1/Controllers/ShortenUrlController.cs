using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using WebApplication1.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/urls")]
    public class ShortenUrlController : ControllerBase
    {
        private readonly IShortenUrlService _shortenUrlService;

        public ShortenUrlController(IShortenUrlService shortenUrlService)
        {
            _shortenUrlService = shortenUrlService;
        }

        // POST /api/urls/shorten
        [Authorize]   // no valid token → 401, the action never runs
        [EnableRateLimiting("shorten-per-user")]   // 11th request in a minute → 429 (policy defined in Program.cs)
        [HttpPost("shorten")]
        public async Task<IActionResult> Shorten(ShortenUrlRequest request)
        {
            // UseAuthentication filled User from the token; NameIdentifier is the Id we put in at login
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _shortenUrlService.ShortenAsync(request, userId);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        // GET /api/urls/my-urls?page=1&pageSize=10
        [Authorize]
        [HttpGet("my-urls")]
        public async Task<IActionResult> MyUrls([FromQuery] PaginationQuery query)
        {
            // [FromQuery]: read Page/PageSize from the query string; [ApiController] returns 400 if [Range] fails
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _shortenUrlService.GetUserUrlsAsync(userId, query);

            return Ok(result);
        }

        // GET /{shortCode}: public, no [Authorize]
        // Leading "/" ignores the controller's "api/urls" prefix.
        // The regex only matches Base62 codes (up to 20 chars), so paths like /favicon.ico never reach the DB.
        // ([[ ]] and {{ }} are escaped [ ] and { } inside a route template)
        [HttpGet("/{shortCode:regex(^[[0-9a-zA-Z]]{{1,20}}$)}")]
        public async Task<IActionResult> RedirectToLongUrl(string shortCode)
        {
            var longUrl = await _shortenUrlService.GetLongUrlAsync(shortCode);

            if (longUrl == null)
                return NotFound(new { message = "Short URL not found." });

            // 302 (temporary), not 301: browsers cache 301 forever, so repeat clicks would never reach us to be counted
            return Redirect(longUrl);
        }
    }
}
