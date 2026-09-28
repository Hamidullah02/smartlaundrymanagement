using System.Net;
using LaundryMVC.Models;
using LaundryMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LaundryMVC.Controllers;

// Login / register (OTP) / forgot password (OTP) / logout / dashboard.
public class HomeController : Controller
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly IEmailSender _email;
    private readonly OtpService _otp;
    private readonly ILogger<HomeController> _log;

    public HomeController(UserManager<AppUser> users, SignInManager<AppUser> signIn,
                          IEmailSender email, OtpService otp, ILogger<HomeController> log)
    {
        _users = users; _signIn = signIn; _email = email; _otp = otp; _log = log;
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

    // POST /Home/Register — creates the user and emails an OTP.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterForm f)
    {
        // If the email is already a half-finished signup, resend the OTP and skip the error.
        var existing = await _users.FindByEmailAsync(f.Email);
        if (existing != null && !existing.EmailConfirmed)
        {
            var (ok, err) = await SendOtp(f.Email, OtpService.Purpose.Register);
            ViewBag.OtpOk = ok;
            ViewBag.OtpError = err;
            if (ok) TempData["Message"] = $"We re-sent a 6-digit code to {f.Email}.";
            return View("VerifyOtp", new OtpVerifyForm { Email = f.Email, Purpose = "register" });
        }

        if (existing != null)
        {
            ModelState.AddModelError("Email", "Email is already registered. Try logging in instead.");
            ViewBag.Register = f;
            ViewBag.ShowRegister = true;
            return View("Index", new LoginForm { Email = f.Email });
        }

        var user = new AppUser { UserName = f.Email, Email = f.Email, FullName = f.FullName };
        var result = await _users.CreateAsync(user, f.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            ViewBag.Register = f;
            ViewBag.ShowRegister = true;
            return View("Index", new LoginForm { Email = f.Email });
        }

        await _users.AddToRoleAsync(user, "Customer");

        var (sendOk, sendErr) = await SendOtp(f.Email, OtpService.Purpose.Register);
        ViewBag.OtpOk = sendOk;
        ViewBag.OtpError = sendErr;
        if (sendOk) TempData["Message"] = $"We sent a 6-digit code to {f.Email}.";
        return View("VerifyOtp", new OtpVerifyForm { Email = f.Email, Purpose = "register" });
    }

    // POST /Home/VerifyOtp
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(OtpVerifyForm f)
    {
        if (string.IsNullOrWhiteSpace(f.Email) || string.IsNullOrWhiteSpace(f.Code))
        {
            ModelState.AddModelError("", "Enter the 6-digit code from your email.");
            return View(f);
        }

        var purpose = f.Purpose == "reset" ? OtpService.Purpose.ResetPassword : OtpService.Purpose.Register;
        if (!_otp.Verify(f.Email, purpose, f.Code.Trim()))
        {
            ModelState.AddModelError("", "Invalid or expired code. Please try again.");
            return View(f);
        }

        if (purpose == OtpService.Purpose.Register)
        {
            var user = await _users.FindByEmailAsync(f.Email);
            if (user == null) { ModelState.AddModelError("", "Account not found."); return View(f); }
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _users.UpdateAsync(user);
            }
            await _signIn.SignInAsync(user, true);
            return RedirectToAction(nameof(Dashboard));
        }

        return RedirectToAction(nameof(ResetPassword), new { email = f.Email });
    }

    // POST /Home/ResendOtp
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email, string purpose)
    {
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Index));
        var p = purpose == "reset" ? OtpService.Purpose.ResetPassword : OtpService.Purpose.Register;

        var user = await _users.FindByEmailAsync(email);
        if (user == null) return RedirectToAction(nameof(Index));
        if (p == OtpService.Purpose.Register && user.EmailConfirmed)
            return RedirectToAction(nameof(Index));

        var (ok, err) = await SendOtp(email, p);
        ViewBag.OtpOk = ok;
        ViewBag.OtpError = err;
        TempData["Message"] = ok ? "A new code has been sent to your email." : "Couldn't send the code — see message below.";
        return View("VerifyOtp", new OtpVerifyForm { Email = email, Purpose = purpose });
    }

    // GET /Home/ForgotPassword
    [HttpGet]
    public IActionResult ForgotPassword() => View(new OtpVerifyForm { Purpose = "reset" });

    // POST /Home/ForgotPassword — sends the reset OTP (if the email exists).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(OtpVerifyForm f)
    {
        if (string.IsNullOrWhiteSpace(f.Email))
        {
            ModelState.AddModelError("Email", "Enter your email.");
            return View(f);
        }

        var user = await _users.FindByEmailAsync(f.Email);
        if (user != null)
        {
            var (ok, err) = await SendOtp(f.Email, OtpService.Purpose.ResetPassword);
            ViewBag.OtpOk = ok;
            ViewBag.OtpError = err;
        }
        return View("VerifyOtp", new OtpVerifyForm { Email = f.Email, Purpose = "reset" });
    }

    // GET /Home/ResetPassword?email=...
    [HttpGet]
    public IActionResult ResetPassword(string email)
        => View(new ResetPasswordForm { Email = email });

    // POST /Home/ResetPassword
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordForm f)
    {
        if (string.IsNullOrWhiteSpace(f.Email) ||
            string.IsNullOrWhiteSpace(f.Password) ||
            f.Password != f.Confirm)
        {
            ModelState.AddModelError("", "Passwords don't match.");
            return View(f);
        }

        var user = await _users.FindByEmailAsync(f.Email);
        if (user == null) return RedirectToAction(nameof(Index));

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, f.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(f);
        }

        TempData["Message"] = "Password reset. You can now log in.";
        return RedirectToAction(nameof(Index));
    }

    // POST /Home/Logout
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET /Home/Dashboard
    [Authorize]
    [HttpGet]
    public IActionResult Dashboard()
    {
        var role = User.IsInRole("Admin") ? "Admin"
                 : User.IsInRole("Staff") ? "Staff"
                 : "Customer";
        ViewBag.Role = role;
        ViewBag.FullName = User.FindFirst("FullName")?.Value ?? User.Identity!.Name;
        return View();
    }

    // ---- helpers ----

    // Generates an OTP, emails it. Returns (success, errorMessage).
    private async Task<(bool ok, string? error)> SendOtp(string email, OtpService.Purpose purpose)
    {
        var code = _otp.Generate(email, purpose);
        var subject = purpose == OtpService.Purpose.Register
            ? "Your Smart Laundry verification code"
            : "Your Smart Laundry password reset code";
        var body = $@"
            <div style='font-family:Arial,sans-serif;max-width:520px;margin:auto'>
              <h2 style='color:#0f766e'>{(purpose == OtpService.Purpose.Register ? "Verify your email" : "Reset your password")}</h2>
              <p>Your 6-digit verification code is:</p>
              <p style='font-size:32px;letter-spacing:6px;font-weight:bold;color:#0f766e;margin:18px 0'>{code}</p>
              <p>This code expires in <strong>5 minutes</strong>. If you didn't request this, ignore the email.</p>
            </div>";

        try
        {
            await _email.SendAsync(email, subject, body);
            _log.LogInformation("OTP email sent to {Email} ({Purpose})", email, purpose);
            return (true, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send OTP email to {Email}", email);
            return (false, $"Couldn't reach Gmail SMTP: {ex.Message}");
        }
    }
}
