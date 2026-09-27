using System.Security.Claims;
using LaundryMVC.Data;
using LaundryMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LaundryMVC.Controllers;

// Login / register / logout and the post-login landing page.
public class HomeController : Controller
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly AppDbContext _db;

    public HomeController(UserManager<AppUser> users, SignInManager<AppUser> signIn, AppDbContext db)
    {
        _users = users; _signIn = signIn; _db = db;
    }

    // GET / — login + register tabs.
    [HttpGet("/")]
    public IActionResult Index()
        => User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Dashboard))
            : View(new LoginForm());

    // POST /Home/Login
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginForm f)
    {
        var user = await _users.FindByEmailAsync(f.Email);
        if (user == null) { Fail(); return View("Index", f); }
        var result = await _signIn.PasswordSignInAsync(user, f.Password, true, false);
        if (!result.Succeeded) { Fail(); return View("Index", f); }

        return RedirectToAction(nameof(Dashboard));
    }

    private void Fail() => ModelState.AddModelError("", "Invalid email or password.");

    // POST /Home/Register — every new signup is a Customer.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterForm f)
    {
        if (await _users.FindByEmailAsync(f.Email) != null)
        {
            ModelState.AddModelError("Email", "Email is already registered.");
            ViewBag.Register = f;
            ViewBag.ShowRegister = true;
            return View("Index", new LoginForm { Email = f.Email });
        }

        var user = new AppUser { UserName = f.Email, Email = f.Email, FullName = f.FullName, EmailConfirmed = true };
        var result = await _users.CreateAsync(user, f.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            ViewBag.Register = f;
            ViewBag.ShowRegister = true;
            return View("Index", new LoginForm { Email = f.Email });
        }

        await _users.AddToRoleAsync(user, "Customer");
        await _signIn.SignInAsync(user, true);
        return RedirectToAction(nameof(Dashboard));
    }

    // POST /Home/Logout
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET /Home/Dashboard — the blank landing page shown after login.
    [Authorize]
    [HttpGet]
    public IActionResult Dashboard()
    {
        var role = User.IsInRole("Admin") ? "Admin"
                 : User.IsInRole("Staff") ? "Staff"
                 : "Customer";
        ViewBag.Role = role;
        ViewBag.FullName = User.FindFirstValue("FullName") ?? User.Identity!.Name;
        return View();
    }
}