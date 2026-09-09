using LaundryMVC.Models;
using Microsoft.AspNetCore.Identity;

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

        foreach (var r in new[] { "Staff", "Customer" })
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new IdentityRole(r));

        await EnsureUser(users, "staff@laundry.local", "Staff", "Staff", "Staff@123456");
    }

    private static async Task EnsureUser(UserManager<AppUser> users, string email, string fullName, string role, string password)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing != null) return;

        var u = new AppUser { UserName = email, Email = email, FullName = fullName, EmailConfirmed = true };
        var result = await users.CreateAsync(u, password);
        if (result.Succeeded)
            await users.AddToRoleAsync(u, role);
    }
}
