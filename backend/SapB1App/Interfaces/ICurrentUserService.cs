using SapB1App.Models;

namespace SapB1App.Interfaces;

public interface ICurrentUserService
{
    CurrentUser? GetCurrentUser();
    int GetSapSalesPersonCode();
    string? GetRole();
    bool IsAdmin();
}
