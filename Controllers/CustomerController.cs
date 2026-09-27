using LaundryMVC.Data;
using LaundryMVC.Models;
using LaundryMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaundryMVC.Controllers;

[Authorize(Roles = "Customer")]
public class CustomerController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IQrCodeService _qrCodeService;

    public CustomerController(AppDbContext db, UserManager<AppUser> userManager, IQrCodeService qrCodeService)
    {
        _db = db;
        _userManager = userManager;
        _qrCodeService = qrCodeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Index", "Home");

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        ViewBag.FullName = user.FullName;
        ViewBag.CustomerId = user.Id;
        ViewBag.Services = await _db.ServiceCatalog
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .ToListAsync();
        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookOrderViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Index", "Home");

        var requestedItems = model.Items?
            .Where(item => item.ServiceId > 0 && item.Quantity > 0)
            .ToList() ?? new List<BookOrderItemInputModel>();

        if (requestedItems.Count == 0)
        {
            TempData["Message"] = "Please add at least one valid laundry item to your order.";
            return RedirectToAction(nameof(Index));
        }

        var requestedServiceIds = requestedItems.Select(item => item.ServiceId).Distinct().ToArray();
        var services = await _db.ServiceCatalog
            .Where(service => service.IsActive && requestedServiceIds.Contains(service.Id))
            .ToDictionaryAsync(service => service.Id);

        if (services.Count != requestedServiceIds.Length)
        {
            TempData["Message"] = "One or more selected services are no longer available. Please review your order.";
            return RedirectToAction(nameof(Index));
        }

        var subtotal = requestedItems.Sum(item => services[item.ServiceId].Price * item.Quantity);
        var hasCoupon = !string.IsNullOrWhiteSpace(model.Coupon);
        var discount = hasCoupon ? Math.Round(subtotal * 0.10m, 2) : 0m;
        var total = subtotal - discount;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var order = new Order
        {
            CustomerId = user.Id,
            CustomerName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "Customer" : user.FullName,
            PickupAddress = model.PickupAddress ?? "Store Pickup",
            PickupAt = model.PickupAt,
            Notes = model.Notes,
            Subtotal = subtotal,
            Discount = discount,
            Total = total,
            Status = OrderStages.Created.ToString(),
            QrToken = $"pending-{Guid.NewGuid():N}",
            QrPngBase64 = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Mint QR Token & PNG Image
        var (token, base64Png) = _qrCodeService.GenerateQr(order.Id);
        order.QrToken = token;
        order.QrPngBase64 = base64Png;

        // Add order items
        foreach (var item in requestedItems)
        {
            order.Items.Add(new OrderItem
            {
                OrderId = order.Id,
                ServiceName = services[item.ServiceId].Name,
                UnitPrice = services[item.ServiceId].Price,
                Quantity = item.Quantity
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Message"] = $"Order #{order.Id} placed successfully!";
        return RedirectToAction(nameof(Index));
    }
}