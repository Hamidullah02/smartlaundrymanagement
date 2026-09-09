using Microsoft.AspNetCore.Identity;

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
    public string QrToken { get; set; } = "";
    public string QrPngBase64 { get; set; } = "";
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

public class BookOrderItemInputModel
{
    public string ServiceName { get; set; } = "";
    public decimal UnitPrice { get; set; }
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