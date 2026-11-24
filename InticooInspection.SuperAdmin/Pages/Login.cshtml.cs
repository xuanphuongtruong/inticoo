using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Pages;

[EnableRateLimiting("login")]
public class LoginModel : PageModel
{
    private readonly PortalDbContext _db;
    private readonly PasswordService _pw;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    public LoginModel(PortalDbContext db, PasswordService pw, SettingsService settings, AuditService audit) { _db = db; _pw = pw; _settings = settings; _audit = audit; }

    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required] public string Password { get; set; } = "";
    [BindProperty] public string? ReturnUrl { get; set; }
    public string? Error { get; set; }

    public IActionResult OnGet(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { Error = "Enter your email and password."; return Page(); }
        var email = Email.Trim().ToLowerInvariant();
        var admin = await _db.SuperAdmins.FirstOrDefaultAsync(a => a.Email == email);
        var maxFails = await _settings.GetIntAsync("security.maxFailedLogins", 5);
        var lockMin = await _settings.GetIntAsync("security.lockoutMinutes", 15);

        if (admin != null && admin.LockoutEnd > DateTime.UtcNow)
        {
            await _audit.LogAsync("LoginLocked", "SuperAdmin", admin.Id.ToString(), email, actor: email);
            Error = "Account temporarily locked. Try again later."; return Page();
        }
        // Luôn băm/so sánh kể cả khi không có tài khoản để thời gian phản hồi không lộ email tồn tại
        var ok = admin != null ? admin.IsActive && _pw.Verify(admin.PasswordHash, Password) : _pw.Verify(_dummy, Password) && false;
        if (!ok)
        {
            if (admin != null)
            {
                admin.FailedCount++;
                if (admin.FailedCount >= maxFails) { admin.LockoutEnd = DateTime.UtcNow.AddMinutes(lockMin); admin.FailedCount = 0; }
                await _db.SaveChangesAsync();
            }
            await _audit.LogAsync("LoginFailed", "SuperAdmin", admin?.Id.ToString(), email, actor: email);
            Error = "Invalid email or password."; return Page();
        }

        admin!.FailedCount = 0; admin.LockoutEnd = null; admin.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()), new(ClaimTypes.Name, admin.Email),
            new("fullname", admin.FullName), new("title", admin.Role), new("role", "SuperAdmin"), new("stamp", admin.SecurityStamp)
        };
        var minutes = await _settings.GetIntAsync("security.sessionMinutes", 60);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(minutes) });
        await _audit.LogAsync("Login", "SuperAdmin", admin.Id.ToString(), null, actor: admin.Email);
        // Chỉ chấp nhận đường dẫn nội bộ (chống open redirect)
        return LocalRedirect(!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) && ReturnUrl.StartsWith("/") ? ReturnUrl : "/");
    }

    private static readonly string _dummy = new PasswordService().Hash("dummy-password-1A!");
}
