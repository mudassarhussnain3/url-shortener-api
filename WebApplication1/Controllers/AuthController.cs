using Microsoft.AspNetCore.Mvc;
using WebApplication1.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        public readonly IAuthService _authService;
        public AuthController(IAuthService IAuthService)
        {
            _authService = IAuthService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var  created = await _authService.RegisterAsync(request);
            if (!created) return Conflict(new { message = "Email already exist" });

            return StatusCode(201, new { message = "User registered successfully" });

        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var token = await _authService.LoginAsync(request);
            if (token == null) return Unauthorized(new { message = "Invalid email or password" });

            return Ok(new { message = "Login successful", token });
        }


    }
}
