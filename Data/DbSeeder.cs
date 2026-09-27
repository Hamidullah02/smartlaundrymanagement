using LaundryMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LaundryMVC.Data;

// Runs once on startup. Creates the schema and the default accounts.
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users   = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles   = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ServiceCatalog" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ServiceCatalog" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Price" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """);

        foreach (var r in new[] { "Admin", "Staff", "Customer" })
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new IdentityRole(r));

        if (!await db.ServiceCatalog.AnyAsync())
        {
            db.ServiceCatalog.AddRange(
                new ServiceCatalogItem { Name = "Shirt Wash & Iron", Description = "Wash, dry, and press shirts.", Price = 2.50m },
                new ServiceCatalogItem { Name = "Pants / Jeans Dry Clean", Description = "Professional dry cleaning for pants and jeans.", Price = 4.00m });
            await db.SaveChangesAsync();
        }

        await EnsureUser(users, "admin@laundry.local", "Administrator", "Admin", "Admin@123456");
        await EnsureUser(users, "staff@laundry.local", "Staff", "Staff", "Staff@123456");
    }

    private static async Task EnsureUser(UserManager<AppUser> users, string email, string fullName, string role, string password)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing == null)
        {
            existing = new AppUser { UserName = email, Email = email, FullName = fullName, EmailConfirmed = true };
            var result = await users.CreateAsync(existing, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        if (!await users.IsInRoleAsync(existing, role))
        {
            var roleResult = await users.AddToRoleAsync(existing, role);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }
    }
}
