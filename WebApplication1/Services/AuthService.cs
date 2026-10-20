using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;


namespace WebApplication1.Services
{
    public class AuthService : IAuthService
    {
        public AppDbContext  _db;
        private readonly IConfiguration _config;
        public AuthService (AppDbContext AppDbContext, IConfiguration config)
        {
            _db = AppDbContext;
            _config = config;
        }

        public async Task<string?> LoginAsync(LoginRequest request)
        {

            User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return null;

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed) return null;
            //return true;
            // 1. What the token says about the user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };

            // 2. The secret key from appsettings, as bytes
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

            // 3. Sign with that key using HMAC-SHA256
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 4. Build the token
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            // 5. Convert it to the "eyJhbGci..." string
            return new JwtSecurityTokenHandler().WriteToken(token);

        }

        async Task<bool> IAuthService.RegisterAsync(RegisterRequest request)
        {
            var email = request.Email.Trim().ToLower();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user?.Email == email)
                return false;
            

                var newUser = new User
                {
                    Name = request.Name.Trim(),
                    Email = email
                };

                var hasher = new PasswordHasher<User>();
                newUser.PasswordHash = hasher.HashPassword(newUser, request.Password);
                await _db.Users.AddAsync(newUser);
                await _db.SaveChangesAsync();
                return true;
            }
        }

    }

