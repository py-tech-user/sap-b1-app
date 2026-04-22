using System.Security.Claims;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUser? GetCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var fullName = user.FindFirstValue("fullName");
        var codeRaw = user.FindFirstValue("sapSalesPersonCode");
        if (string.IsNullOrWhiteSpace(fullName) || !int.TryParse(codeRaw, out var sapSalesPersonCode))
        {
            return null;
        }

        return new CurrentUser(fullName, sapSalesPersonCode);
    }

    public int GetSapSalesPersonCode()
    {
        return GetCurrentUser()?.SapSalesPersonCode ?? 0;
    }

    public string? GetRole()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
    }

    public bool IsAdmin()
    {
        var role = GetRole();
        return string.Equals(role, Roles.Admin, StringComparison.OrdinalIgnoreCase);
    }
}
