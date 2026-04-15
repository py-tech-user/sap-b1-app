using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext    _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, IConfiguration config, ILogger<AuthService> logger)
    {
        _db     = db;
        _config = config;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("🔐 Tentative de connexion pour: {Username}", request.Username);

            // Find active user
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Username == request.Username && u.IsActive);

            if (user is null)
            {
                _logger.LogWarning("❌ Utilisateur '{Username}' introuvable ou inactif", request.Username);
                return null;
            }

            // Verify password
            if (!DbSeeder.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                _logger.LogWarning("❌ Mot de passe incorrect pour '{Username}'", request.Username);
                return null;
            }

            // Update last login
            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Generate token
            var expirationMinutes = int.Parse(
                _config["JwtSettings:ExpirationMinutes"] ?? "480");
            var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);
            var token   = GenerateJwtToken(user, expires);

            _logger.LogInformation("✅ Connexion réussie pour '{Username}' (Rôle: {Role})", user.Username, user.Role);

            return new LoginResponse
            {
                Token    = token,
                Username = user.Username,
                FullName = user.FullName,
                Role     = user.Role,
                Expires  = expires
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur lors de la connexion pour '{Username}'", request.Username);
            throw;
        }
    }

    // ── JWT token generation ─────────────────────────────────────────────────
    private string GenerateJwtToken(AppUser user, DateTime expires)
    {
        var secret = _config["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured.");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub,        user.Id.ToString()),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Email,      user.Email),
            new System.Security.Claims.Claim(ClaimTypes.Role,                    user.Role),
            new System.Security.Claims.Claim("fullName",                         user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer:             _config["JwtSettings:Issuer"],
            audience:           _config["JwtSettings:Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
