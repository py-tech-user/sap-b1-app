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

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        // Find active user
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username == request.Username && u.IsActive);

        if (user is null)
            return null;

        // Verify password
        if (!DbSeeder.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            return null;

        // Update last login
        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Generate token
        var expirationMinutes = int.Parse(
            _config["JwtSettings:ExpirationMinutes"] ?? "480");
        var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var token   = GenerateJwtToken(user, expires);

        return new LoginResponse
        {
            Token    = token,
            Username = user.Username,
            FullName = user.FullName,
            Role     = user.Role,
            Expires  = expires
        };
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
