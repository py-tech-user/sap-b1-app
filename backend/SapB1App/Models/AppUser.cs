namespace SapB1App.Models;

// ═══════════════════════════════════════════════════════════════════════════
// Rôles disponibles
// ═══════════════════════════════════════════════════════════════════════════

public static class Roles
{
    public const string Admin      = "Admin";
    public const string Manager    = "Manager";
    public const string Commercial = "Commercial";

    public static readonly string[] All = [Admin, Manager, Commercial];

    public static bool IsValid(string role) => All.Contains(role, StringComparer.OrdinalIgnoreCase);
}

// ═══════════════════════════════════════════════════════════════════════════
// Policies d'autorisation
// ═══════════════════════════════════════════════════════════════════════════

public static class Policies
{
    public const string AdminOnly       = "AdminOnly";           // Admin uniquement
    public const string ManagerOrAdmin  = "ManagerOrAdmin";      // Manager ou Admin
    public const string AllRoles        = "AllRoles";            // Tous les rôles authentifiés
}

// ═══════════════════════════════════════════════════════════════════════════
// Utilisateur
// ═══════════════════════════════════════════════════════════════════════════

public class AppUser
{
    public int      Id           { get; set; }
    public string   Username     { get; set; } = string.Empty;
    public string   Email        { get; set; } = string.Empty;
    public string   FullName     { get; set; } = string.Empty;
    public string   Role         { get; set; } = Roles.Commercial;  // Admin | Manager | Commercial
    public string   PasswordHash { get; set; } = string.Empty;
    public string   PasswordSalt { get; set; } = string.Empty;
    public bool     IsActive     { get; set; } = true;
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin   { get; set; }
}
