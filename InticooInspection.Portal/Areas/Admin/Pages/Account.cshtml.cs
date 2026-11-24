using System.Security.Claims;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class AccountModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly PasswordService _pw;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    public AccountModel(PortalDbContext db, PasswordService pw, SettingsService settings, AuditService audit) { _db = db; _pw = pw; _settings = settings; _audit = audit; }

    public SuperAdmin? Me { get; set; }
    public List<SuperAdmin> Admins { get; set; } = new();
    public int Uid => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task OnGetAsync()
    {
        Me = await _db.SuperAdmins.AsNoTracking().FirstAsync(a => a.Id == Uid);
        Admins = await _db.SuperAdmins.AsNoTracking().OrderBy(a => a.FullName).ToListAsync();
    }

    public async Task<IActionResult> OnPostPasswordAsync(string current, string next, string confirm)
    {
        var me = await _db.SuperAdmins.FirstAsync(a => a.Id == Uid);
        if (!_pw.Verify(me.PasswordHash, current ?? "")) { SetError("Current password is incorrect."); return RedirectToPage(); }
        if (next != confirm) { SetError("New passwords do not match."); return RedirectToPage(); }
        var err = PasswordService.Validate(next, await _settings.GetIntAsync("security.minPasswordLength", 8));
        if (err != null) { SetError(err); return RedirectToPage(); }
        me.PasswordHash = _pw.Hash(next);
        me.SecurityStamp = Guid.NewGuid().ToString("N");       // đăng xuất mọi phiên cũ
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Password.change", "SuperAdmin", me.Id.ToString());
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/admin/login");
    }

    public async Task<IActionResult> OnPostAddAdminAsync(string email, string fullName, string password)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _) || string.IsNullOrWhiteSpace(fullName)) { SetError("Enter a valid name and email."); return RedirectToPage(); }
        var err = PasswordService.Validate(password, await _settings.GetIntAsync("security.minPasswordLength", 8));
        if (err != null) { SetError(err); return RedirectToPage(); }
        if (await _db.SuperAdmins.AnyAsync(a => a.Email == email)) { SetError("Email already exists."); return RedirectToPage(); }
        _db.SuperAdmins.Add(new SuperAdmin { Email = email, FullName = fullName.Trim(), PasswordHash = _pw.Hash(password) });
        await _db.SaveChangesAsync();
        await _audit.LogAsync("SuperAdmin.create", "SuperAdmin", null, email);
        SetFlash("Administrator added.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        if (id == Uid) { SetError("You cannot deactivate your own account."); return RedirectToPage(); }
        var a = await _db.SuperAdmins.FindAsync(id);
        if (a == null) return NotFound();
        if (a.IsActive && await _db.SuperAdmins.CountAsync(x => x.IsActive) <= 1) { SetError("At least one active administrator is required."); return RedirectToPage(); }
        a.IsActive = !a.IsActive; a.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync();
        await _audit.LogAsync(a.IsActive ? "SuperAdmin.activate" : "SuperAdmin.deactivate", "SuperAdmin", id.ToString(), a.Email);
        SetFlash("Administrator updated.");
        return RedirectToPage();
    }
}
