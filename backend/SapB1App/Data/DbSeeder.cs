using SapB1App.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace SapB1App.Data;

public static class DbSeeder
{
    private sealed record UserSeed(
        string Username,
        string Email,
        string FullName,
        string Role,
        int SapSalesPersonCode,
        string Password);

    private static readonly UserSeed[] RequiredUsers =
    [
        new("admin", "admin@sapb1app.com", "Administrateur", Roles.Admin, 999, "Admin@123"),
        new("karim", "karim@sapb1app.com", "EL BADAOUI Karim", Roles.Commercial, 1, "Karim@123"),
        new("tarik", "tarik@sapb1app.com", "TARIK", Roles.Commercial, 2, "Tarik@123"),
        new("dacosta", "dacosta@sapb1app.com", "Dacosta Mohamed", Roles.Commercial, 3, "Dacosta@123"),
        new("hakim", "hakim@sapb1app.com", "EL BADAOUI Hakim", Roles.Commercial, 4, "Hakim@123")
    ];

    public static async Task SeedAsync(AppDbContext context)
    {
        await EnsureRequiredUsersIfMissingAsync(context);

        if (!context.Products.Any())
        {
            context.Products.AddRange(
                new Product
                {
                    ItemCode = "PROD001",
                    ItemName = "Ordinateur Portable",
                    Description = "Laptop haute performance",
                    Price = 999.99m,
                    Category = "Informatique",
                    Stock = 50,
                    Unit = "Pcs",
                    IsActive = true
                },
                new Product
                {
                    ItemCode = "PROD002",
                    ItemName = "Souris Sans Fil",
                    Description = "Souris ergonomique Bluetooth",
                    Price = 29.99m,
                    Category = "Accessoires",
                    Stock = 200,
                    Unit = "Pcs",
                    IsActive = true
                },
                new Product
                {
                    ItemCode = "PROD003",
                    ItemName = "Clavier Mecanique",
                    Description = "Clavier retroeclaire RGB",
                    Price = 79.99m,
                    Category = "Accessoires",
                    Stock = 8,
                    Unit = "Pcs",
                    IsActive = true
                });

            await context.SaveChangesAsync();
        }

        if (!context.Customers.Any())
        {
            context.Customers.Add(new Customer
            {
                CardCode = "CLI001",
                CardName = "Societe ACME France",
                PartnerType = PartnerType.Client,
                ForeignName = "ACME France Company",
                GroupCode = CustomerGroup.Locaux,
                Currency = CurrencyType.EUR,
                FederalTaxId = "FR12345678901",
                Phone = "+33 1 23 45 67 89",
                Email = "contact@acme.fr",
                Location = "1 rue de la Paix",
                City = "Paris",
                Country = "FR",
                CreditLimit = 50000m
            });

            await context.SaveChangesAsync();
        }
    }

    public static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(64);
        var salt = Convert.ToBase64String(saltBytes);
        using var hmac = new HMACSHA512(saltBytes);
        var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return (hash, salt);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        using var hmac = new HMACSHA512(saltBytes);
        var computed = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return computed == storedHash;
    }

    private static async Task EnsureRequiredUsersIfMissingAsync(AppDbContext context)
    {
        foreach (var seed in RequiredUsers)
        {
            var exists = await context.Users.AsNoTracking().AnyAsync(u =>
                u.Username == seed.Username ||
                u.SapSalesPersonCode == seed.SapSalesPersonCode);
            if (exists)
            {
                continue;
            }

            var (hash, salt) = HashPassword(seed.Password);
            context.Users.Add(new AppUser
            {
                Username = seed.Username,
                Email = seed.Email,
                FullName = seed.FullName,
                SapSalesPersonCode = seed.SapSalesPersonCode,
                Role = seed.Role,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}
