using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;
using System.Text.Json;
using System.Globalization;

namespace SapB1App.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ISapB1Service _sapB1Service;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        ISapB1Service sapB1Service,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _db = db;
        _sapB1Service = sapB1Service;
        _config = config;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Tentative de connexion pour: {Username}", request.Username);

            var sapUser = await AuthenticateAgainstSapAsync(request);
            if (sapUser is not null)
            {
                if (!Roles.IsValid(sapUser.Value.Role))
                {
                    _logger.LogWarning(
                        "Acces refuse pour '{Username}': role SAP '{Role}' non autorise",
                        request.Username,
                        sapUser.Value.Role);
                    return null;
                }

                var commercialUser = await EnsureLocalUserAsync(
                    username: sapUser.Value.Username,
                    fullName: sapUser.Value.FullName,
                    role: sapUser.Value.Role,
                    sapSalesPersonCode: sapUser.Value.SalesPersonCode);
                commercialUser.LastLogin = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return BuildLoginResponse(commercialUser);
            }
            
            _logger.LogWarning("Utilisateur SAP invalide pour '{Username}'", request.Username);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la connexion pour '{Username}'", request.Username);
            throw;
        }
    }

    private LoginResponse BuildLoginResponse(AppUser user)
    {
        var expirationMinutes = int.Parse(_config["JwtSettings:ExpirationMinutes"] ?? "480");
        var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var token = GenerateJwtToken(user, expires);

        _logger.LogInformation("Connexion reussie pour '{Username}' (Role: {Role})", user.Username, user.Role);

        return new LoginResponse
        {
            Token = token,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            SapSalesPersonCode = user.SapSalesPersonCode,
            CurrentUser = new CurrentUserDto
            {
                FullName = user.FullName,
                SapSalesPersonCode = user.SapSalesPersonCode
            },
            Expires = expires
        };
    }

    private async Task<(int SalesPersonCode, string Username, string FullName, string Role)? > AuthenticateAgainstSapAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var escapedUsername = EscapeOdataString(request.Username.Trim());
        var escapedPassword = EscapeOdataString(request.Password);

        var relativeUrl =
            $"SalesPersons?$select=SalesEmployeeCode,SalesEmployeeName,U_NomUtilisateur,U_MotPasseWeb,U_AppRole&$filter=U_NomUtilisateur eq '{escapedUsername}' and U_MotPasseWeb eq '{escapedPassword}'";

        var response = await _sapB1Service.ServiceLayerGetAsync(relativeUrl);
        if (!response.Success || response.Response is null)
        {
            _logger.LogWarning("Echec verification SAP pour '{Username}': {Error}", request.Username, response.ErrorMessage);
            return null;
        }

        if (response.Response.Value.ValueKind != JsonValueKind.Object ||
            !response.Response.Value.TryGetProperty("value", out var valueNode) ||
            valueNode.ValueKind != JsonValueKind.Array ||
            valueNode.GetArrayLength() == 0)
        {
            return null;
        }

        foreach (var item in valueNode.EnumerateArray())
        {
            if (!item.TryGetProperty("SalesEmployeeCode", out var salesCodeNode) ||
                !salesCodeNode.TryGetInt32(out var salesPersonCode) ||
                salesPersonCode <= 0)
            {
                continue;
            }

            var username = item.TryGetProperty("U_NomUtilisateur", out var usernameNode)
                ? usernameNode.GetString() ?? request.Username
                : request.Username;

            var fullName = GetStringProperty(item, "SalesEmployeeName") ?? username;
            var role = NormalizeAppRole(GetStringProperty(item, "U_AppRole"));
            return (salesPersonCode, username, fullName, role);
        }

        return null;
    }

    private static string EscapeOdataString(string input) => input.Replace("'", "''");

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private async Task<AppUser> EnsureLocalUserAsync(string username, string fullName, string role, int sapSalesPersonCode)
    {
        var normalizedUsername = username.Trim();
        var existing = await _db.Users.AsTracking().FirstOrDefaultAsync(u =>
            u.Username == normalizedUsername || u.SapSalesPersonCode == sapSalesPersonCode);

        if (existing is not null)
        {
            existing.Username = normalizedUsername;
            existing.FullName = fullName;
            existing.Role = role;
            existing.IsActive = true;
            if (sapSalesPersonCode > 0 || existing.SapSalesPersonCode <= 0)
            {
                existing.SapSalesPersonCode = sapSalesPersonCode;
            }

            if (string.IsNullOrWhiteSpace(existing.Email))
            {
                existing.Email = BuildFallbackEmail(normalizedUsername);
            }

            return existing;
        }

        var (hash, salt) = DbSeeder.HashPassword(Guid.NewGuid().ToString("N"));
        var user = new AppUser
        {
            Username = normalizedUsername,
            Email = BuildFallbackEmail(normalizedUsername),
            FullName = fullName,
            Role = role,
            SapSalesPersonCode = sapSalesPersonCode,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        return user;
    }

    private static string BuildFallbackEmail(string username)
    {
        var compact = username.Trim().ToLowerInvariant().Replace(" ", ".");
        return $"{compact}@sap.local";
    }

    private static string NormalizeAppRole(string? rawRole)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            return "Unauthorized";
        }

        if (rawRole.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)) return Roles.Admin;
        if (rawRole.Equals(Roles.Manager, StringComparison.OrdinalIgnoreCase)) return Roles.Manager;
        if (rawRole.Equals(Roles.Commercial, StringComparison.OrdinalIgnoreCase)) return Roles.Commercial;
        return "Unauthorized";
    }

    private string GenerateJwtToken(AppUser user, DateTime expires)
    {
        var secret = _config["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName),
            new Claim("sapSalesPersonCode", user.SapSalesPersonCode.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
