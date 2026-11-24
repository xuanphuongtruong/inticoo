using InticooInspection.Portal.Services;
using InticooInspection.Portal.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Pages;

public class LanguagesModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    public LanguagesModel(PortalDbContext db, SettingsService settings, AuditService audit) { _db = db; _settings = settings; _audit = audit; }

    public Dictionary<string, string> S { get; set; } = new();
    public Dictionary<string, int> Companies { get; set; } = new();
    public Dictionary<string, int> Users { get; set; } = new();

    public async Task OnGetAsync()
    {
        S = await _settings.AllAsync();
        var regs = await _db.Registrations.AsNoTracking().Where(r => r.Company!.Status != CompanyStatus.Cancelled).Select(r => r.Languages).ToListAsync();
        foreach (var l in LocalizationStore.Supported) Companies[l] = regs.Count(x => x.Split(',').Contains(l));
        var ul = await _db.CompanyUsers.AsNoTracking().GroupBy(u => u.Language).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        foreach (var l in LocalizationStore.Supported) Users[l] = ul.FirstOrDefault(x => x.Key == l)?.N ?? 0;
    }

    public async Task<IActionResult> OnPostAsync(List<string>? enabled, string defaultLanguage)
    {
        var on = (enabled ?? new()).Where(LocalizationStore.Supported.Contains).ToHashSet();
        if (!LocalizationStore.Supported.Contains(defaultLanguage)) defaultLanguage = "en";
        on.Add(defaultLanguage);   // ngôn ngữ mặc định luôn bật
        var d = LocalizationStore.Supported.ToDictionary(l => "lang." + l, l => on.Contains(l) ? "true" : "false");
        d["default.language"] = defaultLanguage;
        await _settings.SetManyAsync(d);
        await _audit.LogAsync("Languages.update", "Setting", null, string.Join(',', on));
        SetFlash("Language settings saved.");
        return RedirectToPage();
    }
}
