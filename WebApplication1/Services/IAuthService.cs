using WebApplication1.DTOs;

namespace WebApplication1.Services
{
    public interface IAuthService
    {
        Task<bool> RegisterAsync(RegisterRequest request);
        Task<string?> LoginAsync(LoginRequest request);
    }
}
