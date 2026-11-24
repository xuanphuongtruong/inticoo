using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;

namespace InticooInspection.SuperAdmin.Pages;

/// <summary>Dung lượng từng công ty (DB riêng + file upload) so với hạn mức gói; ổ đĩa server; xu hướng 90 ngày.
/// Số đo do API ghi (UsageWorker) — trang này chỉ đọc và gửi yêu cầu "Measure now".</summary>
public class StorageModel : AdminPageModel
{
    private readonly UsageReportService _usage;
    private readonly AuditService _audit;
    public StorageModel(UsageReportService usage, AuditService audit) { _usage = usage; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Filter { get; set; }     // over / near / inactive
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public List<UsageRow> Rows { get; set; } = new();
    public PlatformUsage P { get; set; } = new();
    public List<(DateTime Day, double Gb)> Trend { get; set; } = new();
    public UsageReportService U => _usage;
    public bool MeasurePending => P.RequestedAt != null && (P.LastRunAt == null || P.RequestedAt > P.LastRunAt);

    public async Task OnGetAsync()
    {
        var all = await _usage.RowsAsync();
        P = await _usage.PlatformAsync(all);
        Trend = await _usage.TrendAsync(90);
        IEnumerable<UsageRow> q = all;
        if (!string.IsNullOrWhiteSpace(Q)) q = q.Where(r => r.Name.Contains(Q, StringComparison.OrdinalIgnoreCase));
        q = Filter switch
        {
            "over" => q.Where(r => _usage.IsOverStorage(r) || _usage.IsOverUsers(r)),
            "near" => q.Where(_usage.IsNear),
            "inactive" => q.Where(r => !r.IsLegacy && _usage.IsInactive(r)),
            _ => q
        };
        Rows = q.OrderByDescending(r => r.UsedGb).ToList();
    }

    public async Task<IActionResult> OnPostMeasureAsync()
    {
        await _usage.RequestMeasureAsync();
        await _audit.LogAsync("Usage.measure", "Storage", null, "requested");
        SetFlash("Measurement requested. The API measures every company within about a minute — refresh this page to see the new figures.");
        return RedirectToPage(new { filter = Filter, q = Q });
    }

    public async Task<IActionResult> OnGetExportAsync(string format = "xlsx")
    {
        var rows = (await _usage.RowsAsync()).OrderByDescending(r => r.UsedGb).ToList();
        string[] h = { "Company", "Plan", "Status", "Database (GB)", "Files (GB)", "Files", "Total (GB)", "Quota (GB)", "Usage %", "Growth 30 days (GB)", "Days to full", "Active users", "Plan user limit", "Last activity", "Measured" };
        var data = rows.Select(r => new object?[] { r.Name, r.Plan, r.Status, Math.Round(r.DbGb, 3), Math.Round(r.FilesGb, 3), r.FileCount, Math.Round(r.UsedGb, 3), r.QuotaGb,
            Math.Round(r.StoragePct, 1), Math.Round(r.Growth30Gb, 3), r.DaysToFull, r.ActiveUsers, r.MaxUsers, r.LastActivityAt, r.MeasuredAt }).ToList();
        await _audit.LogAsync("Export", "Storage", null, $"{data.Count} rows");
        return Export.Xlsx("Storage", h, data, $"storage-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
