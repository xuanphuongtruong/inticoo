using System.ComponentModel.DataAnnotations;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace InticooInspection.Website.Pages;

[EnableRateLimiting("contact")]
public class ContactModel : PageModel
{
    public static readonly string[] Subjects = { "Book a Demo", "Request a Quote", "General Enquiry", "Partnership", "Technical Support", "Request a Resource" };
    private readonly PortalDbContext _db;
    private readonly Loc _l;
    public ContactModel(PortalDbContext db, Loc l) { _db = db; _l = l; }

    [BindProperty, Required(ErrorMessage = "Please enter your full name."), StringLength(150, ErrorMessage = "Full name is too long.")] public string FullName { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Please enter your company name."), StringLength(200, ErrorMessage = "Company name is too long.")] public string CompanyName { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address."), StringLength(200, ErrorMessage = "Email address is too long.")] public string Email { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Please select your country / region."), StringLength(80, ErrorMessage = "Please select your country / region.")] public string Country { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Please select a subject."), StringLength(60, ErrorMessage = "Please select a subject.")] public string Subject { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Please enter your message."), StringLength(500, ErrorMessage = "Message must be 500 characters or fewer.")] public string Message { get; set; } = "";
    [BindProperty] public string? Hp { get; set; }      // honeypot chống bot
    public bool Sent { get; set; }

    public void OnGet(string? subject)
    {
        if (subject != null && Subjects.Contains(subject)) Subject = subject;
        Sent = TempData["sent"] != null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!string.IsNullOrEmpty(Hp)) return RedirectToPage();           // bot: im lặng bỏ qua
        if (!Subjects.Contains(Subject)) ModelState.AddModelError(nameof(Subject), "Please select a subject.");
        if (!ModelState.IsValid) { ModelState.Localize(_l); return Page(); }
        _db.ContactMessages.Add(new ContactMessage
        {
            FullName = FullName.Trim(), Company = CompanyName.Trim(), Email = Email.Trim(), Country = Country.Trim(),
            Subject = Subject, Message = Message.Trim(), IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
        TempData["sent"] = "1";
        return RedirectToPage();
    }
}
