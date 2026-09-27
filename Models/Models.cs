using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace LaundryMVC.Models;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum OrderStages
{
    Created,
    Received,
    Washing,
    Drying,
    Ironing,
    QualityCheck,
    ReadyForDelivery,
    Delivered,
    Cancelled
}

public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Status { get; set; } = OrderStages.Created.ToString();
    public string PickupAddress { get; set; } = "";
    public DateTime PickupAt { get; set; } = DateTime.UtcNow.AddHours(2);
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string? QrToken { get; set; }
    public string? QrPngBase64 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ServiceName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
}

public class ServiceCatalogItem
{
    public int Id { get; set; }
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    [StringLength(500)]
    public string Description { get; set; } = "";
    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class BookOrderItemInputModel
{
    public int ServiceId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class BookOrderViewModel
{
    public string PickupAddress { get; set; } = "";
    public DateTime PickupAt { get; set; } = DateTime.Now.AddHours(2);
    public string? Notes { get; set; }
    public string? Coupon { get; set; }
    public List<BookOrderItemInputModel> Items { get; set; } = new();
}

public class LoginForm
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegisterForm
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class CreateStaffForm
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = "";
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, MinLength(6)]
    public string Password { get; set; } = "";
}

public class AdminUserRow
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; }
}

public class AdminUsersViewModel
{
    public List<AdminUserRow> Users { get; set; } = new();
    public CreateStaffForm NewStaff { get; set; } = new();
}

public class RevenueMonthViewModel
{
    public string Month { get; set; } = "";
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}

public class OrderStatusCountViewModel
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public class AdminDashboardViewModel
{
    public int TotalOrders { get; set; }
    public int ActiveOrders { get; set; }
    public int CustomerCount { get; set; }
    public int StaffCount { get; set; }
    public int ServiceCount { get; set; }
    public decimal Revenue { get; set; }
    public List<RevenueMonthViewModel> MonthlyRevenue { get; set; } = new();
    public List<OrderStatusCountViewModel> OrdersByStatus { get; set; } = new();
}
