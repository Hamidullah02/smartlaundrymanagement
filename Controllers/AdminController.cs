using System.Security.Claims;
using System.Text;
using LaundryMVC.Data;
using LaundryMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaundryMVC.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public AdminController(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        var orders = await _db.Orders.AsNoTracking().ToListAsync();
        var revenueOrders = orders.Where(order => order.Status != OrderStages.Cancelled.ToString()).ToList();

        var model = new AdminDashboardViewModel
        {
            TotalOrders = orders.Count,
            ActiveOrders = orders.Count(order => order.Status != OrderStages.Delivered.ToString() && order.Status != OrderStages.Cancelled.ToString()),
            CustomerCount = (await _users.GetUsersInRoleAsync("Customer")).Count,
            StaffCount = (await _users.GetUsersInRoleAsync("Staff")).Count,
            ServiceCount = await _db.ServiceCatalog.CountAsync(service => service.IsActive),
            Revenue = revenueOrders.Sum(order => order.Total),
            OrdersByStatus = orders
                .GroupBy(order => order.Status)
                .Select(group => new OrderStatusCountViewModel { Status = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ToList()
        };

        model.MonthlyRevenue = Enumerable.Range(0, 6)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month =>
            {
                var monthOrders = revenueOrders
                    .Where(order => order.CreatedAt.Year == month.Year && order.CreatedAt.Month == month.Month)
                    .ToList();
                return new RevenueMonthViewModel
                {
                    Month = month.ToString("MMM yyyy"),
                    Revenue = monthOrders.Sum(order => order.Total),
                    Orders = monthOrders.Count
                };
            })
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Users()
        => View(await BuildUsersViewModelAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff(CreateStaffForm form)
    {
        if (!ModelState.IsValid)
            return View("Users", await BuildUsersViewModelAsync(form));

        var user = new AppUser
        {
            UserName = form.Email.Trim(),
            Email = form.Email.Trim(),
            FullName = form.FullName.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _users.CreateAsync(user, form.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View("Users", await BuildUsersViewModelAsync(form));
        }

        var roleResult = await _users.AddToRoleAsync(user, "Staff");
        if (!roleResult.Succeeded)
        {
            await _users.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            return View("Users", await BuildUsersViewModelAsync(form));
        }

        TempData["Message"] = $"Staff account created for {user.Email}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserRole(string userId, string role)
    {
        if (role is not ("Customer" or "Staff"))
            return BadRequest();

        var user = await _users.FindByIdAsync(userId);
        if (user == null) return NotFound();
        if (await _users.IsInRoleAsync(user, "Admin"))
            return Forbid();

        var currentRoles = await _users.GetRolesAsync(user);
        foreach (var currentRole in currentRoles)
        {
            var removeResult = await _users.RemoveFromRoleAsync(user, currentRole);
            if (!removeResult.Succeeded)
            {
                AddIdentityErrors(removeResult);
                TempData["Message"] = "Could not update the account role.";
                return RedirectToAction(nameof(Users));
            }
        }

        var addResult = await _users.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
            foreach (var currentRole in currentRoles)
                await _users.AddToRoleAsync(user, currentRole);
            AddIdentityErrors(addResult);
            TempData["Message"] = "Could not update the account role.";
            return RedirectToAction(nameof(Users));
        }

        TempData["Message"] = $"{user.Email} is now assigned to {role}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null) return NotFound();
        if (await _users.IsInRoleAsync(user, "Admin"))
            return Forbid();

        var isActive = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow;
        var enableLockout = await _users.SetLockoutEnabledAsync(user, true);
        if (!enableLockout.Succeeded)
        {
            AddIdentityErrors(enableLockout);
            TempData["Message"] = "Could not update the account status.";
            return RedirectToAction(nameof(Users));
        }

        var lockoutEnd = await _users.SetLockoutEndDateAsync(user, isActive ? DateTimeOffset.UtcNow.AddYears(100) : null);
        if (!lockoutEnd.Succeeded)
        {
            AddIdentityErrors(lockoutEnd);
            TempData["Message"] = "Could not update the account status.";
            return RedirectToAction(nameof(Users));
        }

        await _users.ResetAccessFailedCountAsync(user);
        await _users.UpdateSecurityStampAsync(user);
        TempData["Message"] = $"{user.Email} has been {(isActive ? "deactivated" : "reactivated")}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Services()
        => View(await _db.ServiceCatalog.AsNoTracking().OrderBy(service => service.Name).ToListAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveService(int id, string name, string? description, decimal price)
    {
        name = name?.Trim() ?? "";
        description = description?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || description.Length > 500 || price <= 0 || price > 100000)
        {
            TempData["Message"] = "Enter a service name, a description under 500 characters, and a price greater than zero.";
            return RedirectToAction(nameof(Services));
        }

        var service = id == 0 ? new ServiceCatalogItem() : await _db.ServiceCatalog.FindAsync(id);
        if (service == null) return NotFound();

        service.Name = name;
        service.Description = description;
        service.Price = price;
        service.UpdatedAt = DateTime.UtcNow;
        if (id == 0) _db.ServiceCatalog.Add(service);

        await _db.SaveChangesAsync();
        TempData["Message"] = $"Service '{service.Name}' saved.";
        return RedirectToAction(nameof(Services));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleService(int id)
    {
        var service = await _db.ServiceCatalog.FindAsync(id);
        if (service == null) return NotFound();

        service.IsActive = !service.IsActive;
        service.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Service '{service.Name}' {(service.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Services));
    }

    [HttpGet]
    public async Task<IActionResult> ExportOrders()
    {
        var orders = await _db.Orders.AsNoTracking()
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();
        var rows = new List<string>
        {
            CsvRow("OrderId", "CustomerName", "CustomerId", "Status", "PickupAddress", "PickupAt", "Subtotal", "Discount", "Total", "Items", "CreatedAt")
        };

        rows.AddRange(orders.Select(order => CsvRow(
            order.Id.ToString(), order.CustomerName, order.CustomerId, order.Status, order.PickupAddress,
            order.PickupAt.ToString("O"), order.Subtotal.ToString("F2"), order.Discount.ToString("F2"),
            order.Total.ToString("F2"), string.Join("; ", order.Items.Select(item => $"{item.ServiceName} x{item.Quantity}")), order.CreatedAt.ToString("O"))));

        return CsvFile("laundry-orders.csv", rows);
    }

    [HttpGet]
    public async Task<IActionResult> ExportUsers()
    {
        var users = await _users.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync();
        var rows = new List<string> { CsvRow("UserId", "FullName", "Email", "Roles") };
        foreach (var user in users)
            rows.Add(CsvRow(user.Id, user.FullName, user.Email ?? "", string.Join("; ", await _users.GetRolesAsync(user))));

        return CsvFile("laundry-users.csv", rows);
    }

    [HttpGet]
    public async Task<IActionResult> ExportServices()
    {
        var services = await _db.ServiceCatalog.AsNoTracking().OrderBy(service => service.Name).ToListAsync();
        var rows = new List<string> { CsvRow("ServiceId", "Name", "Description", "Price", "Active", "UpdatedAt") };
        rows.AddRange(services.Select(service => CsvRow(service.Id.ToString(), service.Name, service.Description,
            service.Price.ToString("F2"), service.IsActive ? "true" : "false", service.UpdatedAt.ToString("O"))));

        return CsvFile("laundry-services.csv", rows);
    }

    private async Task<AdminUsersViewModel> BuildUsersViewModelAsync(CreateStaffForm? form = null)
    {
        var users = await _users.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync();
        var rows = new List<AdminUserRow>(users.Count);
        foreach (var user in users)
        {
            var roles = await _users.GetRolesAsync(user);
            rows.Add(new AdminUserRow
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? "Unassigned",
                IsActive = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow
            });
        }

        return new AdminUsersViewModel { Users = rows, NewStaff = form ?? new CreateStaffForm() };
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);
    }

    private static string CsvRow(params string[] fields)
        => string.Join(",", fields.Select(CsvField));

    private static string CsvField(string value)
        => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static FileContentResult CsvFile(string fileName, IEnumerable<string> rows)
        => new(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, rows)), "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName
        };
}