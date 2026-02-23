using SapB1App.Models;
using System.Security.Cryptography;
using System.Text;

namespace SapB1App.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // ── Users (Admin, Manager, Commercial) ─────────────────────────────
        await SeedUserIfNotExists(context, "admin", "admin@sapb1app.com", "Administrateur", Roles.Admin, "Admin@123");
        await SeedUserIfNotExists(context, "manager", "manager@sapb1app.com", "Chef des Ventes", Roles.Manager, "Manager@123");
        await SeedUserIfNotExists(context, "commercial", "commercial@sapb1app.com", "Agent Commercial", Roles.Commercial, "Commercial@123");

        // ── Sample products ─────────────────────────────────────────────────
        if (!context.Products.Any())
        {
            context.Products.AddRange(
                new Product
                {
                    ItemCode    = "PROD001",
                    ItemName    = "Ordinateur Portable",
                    Description = "Laptop haute performance",
                    Price       = 999.99m,
                    Category    = "Informatique",
                    Stock       = 50,
                    Unit        = "Pcs",
                    IsActive    = true
                },
                new Product
                {
                    ItemCode    = "PROD002",
                    ItemName    = "Souris Sans Fil",
                    Description = "Souris ergonomique Bluetooth",
                    Price       = 29.99m,
                    Category    = "Accessoires",
                    Stock       = 200,
                    Unit        = "Pcs",
                    IsActive    = true
                },
                new Product
                {
                    ItemCode    = "PROD003",
                    ItemName    = "Clavier Mécanique",
                    Description = "Clavier rétroéclairé RGB",
                    Price       = 79.99m,
                    Category    = "Accessoires",
                    Stock       = 8,   // volontairement bas pour tester l'alerte stock
                    Unit        = "Pcs",
                    IsActive    = true
                }
            );
            await context.SaveChangesAsync();
        }

        // ── Sample customer ─────────────────────────────────────────────────
        if (!context.Customers.Any())
        {
            context.Customers.Add(new Customer
            {
                CardCode    = "CLI001",
                CardName    = "Société ACME France",
                Phone       = "+33 1 23 45 67 89",
                Email       = "contact@acme.fr",
                Address     = "1 rue de la Paix",
                City        = "Paris",
                Country     = "FR",
                Currency    = "EUR",
                CreditLimit = 50000m,
                IsActive    = true
            });
            await context.SaveChangesAsync();
        }
    }

    // ── Password hashing ────────────────────────────────────────────────────
    public static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(64);
        var salt      = Convert.ToBase64String(saltBytes);
        using var hmac = new HMACSHA512(saltBytes);
        var hash = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return (hash, salt);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        using var hmac = new HMACSHA512(saltBytes);
        var computed  = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return computed == storedHash;
    }

    // ── Seed user if not exists ──────────────────────────────────────────────
    private static async Task SeedUserIfNotExists(
        AppDbContext context,
        string username,
        string email,
        string fullName,
        string role,
        string password)
    {
        if (context.Users.Any(u => u.Username == username))
            return;

        var (hash, salt) = HashPassword(password);
        context.Users.Add(new AppUser
        {
            Username     = username,
            Email        = email,
            FullName     = fullName,
            Role         = role,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}
