using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet() => Redirect("/admin/login");     // GET không đăng xuất (chống CSRF qua link)
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/admin/login");
    }
}
