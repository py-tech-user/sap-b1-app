using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUserService,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _authService = authService;
        _currentUserService = currentUserService;
        _config = config;
        _env = env;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<LoginResponse>(false, "Donnees invalides.", null));
        }

        var result = await _authService.LoginAsync(request);
        if (result is null)
        {
            return Unauthorized(new ApiResponse<LoginResponse>(false, "Nom d'utilisateur ou mot de passe incorrect.", null));
        }

        return Ok(new ApiResponse<LoginResponse>(true, "Connexion reussie.", result));
    }

    [HttpPost("dev-token")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<LoginResponse>> DevToken([FromQuery] string role = "Admin")
    {
        if (!_env.IsDevelopment())
        {
            return NotFound(new ApiResponse<LoginResponse>(false, "Endpoint not available.", null));
        }

        if (!Roles.IsValid(role))
        {
            return BadRequest(new ApiResponse<LoginResponse>(
                false,
                $"Role invalide. Utilisez: {string.Join(", ", Roles.All)}",
                null));
        }

        var secret = _config["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(24);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "999"),
            new Claim(ClaimTypes.NameIdentifier, "999"),
            new Claim(JwtRegisteredClaimNames.UniqueName, $"dev_{role.ToLower()}"),
            new Claim(JwtRegisteredClaimNames.Email, $"dev_{role.ToLower()}@test.com"),
            new Claim(ClaimTypes.Role, role),
            new Claim("fullName", $"Dev {role} User"),
            new Claim("sapSalesPersonCode", "1")
        };

        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new ApiResponse<LoginResponse>(
            true,
            $"Token de test genere pour le role {role}.",
            new LoginResponse
            {
                Token = tokenString,
                Username = $"dev_{role.ToLower()}",
                FullName = $"Dev {role} User",
                Role = role,
                SapSalesPersonCode = 1,
                CurrentUser = new CurrentUserDto
                {
                    FullName = $"Dev {role} User",
                    SapSalesPersonCode = 1
                },
                Expires = expires
            }));
    }

    [HttpGet("roles")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<string[]>> GetRoles()
    {
        return Ok(new ApiResponse<string[]>(true, null, Roles.All));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var currentUser = _currentUserService.GetCurrentUser();

        return Ok(new ApiResponse<object>(true, null, new
        {
            Id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            Username = User.Identity?.Name ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName),
            Role = User.FindFirstValue(ClaimTypes.Role),
            FullName = User.FindFirstValue("fullName"),
            SapSalesPersonCode = _currentUserService.GetSapSalesPersonCode(),
            CurrentUser = currentUser is null
                ? null
                : new
                {
                    currentUser.FullName,
                    currentUser.SapSalesPersonCode
                }
        }));
    }
}
