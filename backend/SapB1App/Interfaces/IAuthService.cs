using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}
