using LaundryMVC.Data;
using LaundryMVC.Hubs;
using LaundryMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LaundryMVC.Controllers;

[Authorize(Roles = "Staff")]
public class StaffController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IHubContext<OrderHub> _hubContext;

    public StaffController(AppDbContext db, UserManager<AppUser> userManager, IHubContext<OrderHub> hubContext)
    {
        _db = db;
        _userManager = userManager;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var orders = await _db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        ViewBag.FullName = user?.FullName;
        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Advance(int id)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        if (Enum.TryParse<OrderStages>(order.Status, out var currentStage))
        {
            if (currentStage != OrderStages.Delivered && currentStage != OrderStages.Cancelled)
            {
                var nextStage = currentStage + 1;
                order.Status = nextStage.ToString();
                order.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                // SignalR push notification
                await _hubContext.Clients.Group(order.CustomerId).SendAsync("OrderStatusChanged", order.Id, order.Status);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", order.Id, order.Status);

                TempData["Message"] = $"Order #{order.Id} advanced to {order.Status}.";
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        if (order.Status != OrderStages.Delivered.ToString())
        {
            order.Status = OrderStages.Cancelled.ToString();
            order.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // SignalR push notification
            await _hubContext.Clients.Group(order.CustomerId).SendAsync("OrderStatusChanged", order.Id, "Cancelled");
            await _hubContext.Clients.All.SendAsync("OrderStatusChanged", order.Id, "Cancelled");

            TempData["Message"] = $"Order #{order.Id} has been cancelled.";
        }

        return RedirectToAction(nameof(Index));
    }
}