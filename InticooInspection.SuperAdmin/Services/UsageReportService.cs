using System.Globalization;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Services;

/// <summary>Một dòng mức sử dụng của 1 công ty (hoặc DB gốc "Legacy") — số đo thật do API ghi (sup.CompanyUsageSnapshots).</summary>
public class UsageRow
{
    public Guid? CompanyId;
    public string Name = "", Code = "", Country = "", CountryCode = "", Status = "", Plan = "";   // "" = không có gói (DB gốc)
    public bool IsLegacy, IsDemo;
    public double DbGb, FilesGb, UsedGb, Growth30Gb;
    public int FileCount, QuotaGb, MaxUsers, Users, ActiveUsers, ActiveUsers7d, ActiveUsers30d, Inspections, InspectionsThisMonth, OpenCapa;
    public DateTime? MeasuredAt, LastActivityAt, LimitOverrideUntil, RegisteredAt;
    public double StoragePct => QuotaGb > 0 ? UsedGb / QuotaGb * 100 : 0;
    public double UserPct => MaxUsers > 0 ? ActiveUsers * 100.0 / MaxUsers : 0;
    /// <summary>Số ngày đến khi đầy hạn mức theo tốc độ tăng 30 ngày gần nhất (null = không tăng / không có hạn mức).</summary>
    public int? DaysToFull => QuotaGb > 0 && Growth30Gb > 0 ? (int)Math.Max(0, Math.Floor((QuotaGb - UsedGb) / (Growth30Gb / 30))) : null;
    public bool Overridden => LimitOverrideUntil is { } u && u >= DateTime.UtcNow;
}

public class PlatformUsage
{
    public double UsedGb, AllocatedGb, DiskTotalGb, DiskFreeGb, UploadsGb, ExternalGb;
    public string? DiskHost, DiskDrive;
    public DateTime? DiskMeasuredAt, LastRunAt, RequestedAt;
    public double CapacityGb => DiskTotalGb + ExternalGb;
    public int Tenants, ActiveTenants, TotalUsers, ActiveUsers, OverStorage, OverUsers, NearLimit, Inactive;
}

/// <summary>Tổng hợp mức sử dụng cho Dashboard / Storage / Companies / Reports. Không có số nào viết cứng:
/// dung lượng & người dùng từ số đo của API, hạn mức từ gói, ngưỡng từ Settings (usage.*).</summary>
public class UsageReportService
{
    private readonly PortalDbContext _db;
    private readonly SettingsService _settings;
    public UsageReportService(PortalDbContext db, SettingsService settings) { _db = db; _settings = settings; }

    public int WarnPercent { get; private set; } = 80;
    public int InactiveDays { get; private set; } = 30;

    private async Task LoadSettingsAsync()
    {
        WarnPercent = Math.Clamp(await _settings.GetIntAsync("usage.warnPercent", 80), 1, 100);
        InactiveDays = Math.Clamp(await _settings.GetIntAsync("usage.inactiveDays", 30), 1, 3650);
    }

    /// <summary>Không đăng nhập / tạo phiếu quá N ngày (chưa từng hoạt động → tính từ ngày đăng ký, để khách mới không bị coi là bỏ dùng).</summary>
    public bool IsInactive(UsageRow r) => r.MeasuredAt != null && (r.LastActivityAt ?? r.RegisteredAt ?? DateTime.MinValue) < DateTime.UtcNow.AddDays(-InactiveDays);
    public bool IsOverStorage(UsageRow r) => r.QuotaGb > 0 && r.UsedGb >= r.QuotaGb;
    public bool IsOverUsers(UsageRow r) => r.MaxUsers > 0 && r.ActiveUsers >= r.MaxUsers;
    public bool IsNear(UsageRow r) => !IsOverStorage(r) && !IsOverUsers(r) && (r.StoragePct >= WarnPercent || r.UserPct >= WarnPercent);

    /// <summary>Mức sử dụng của các công ty (bỏ công ty đã hủy nếu <paramref name="includeCancelled"/> = false) + dòng DB gốc nếu có số đo.</summary>
    public async Task<List<UsageRow>> RowsAsync(bool includeCancelled = false, bool includeLegacy = true)
    {
        await LoadSettingsAsync();
        var q = _db.Companies.AsNoTracking().AsQueryable();
        if (!includeCancelled) q = q.Where(c => c.Status != CompanyStatus.Cancelled);
        var list = await q.Select(c => new
        {
            C = c,
            Sub = c.Subscriptions.Where(s => s.Status != SubscriptionStatus.Cancelled).OrderByDescending(s => s.Id)
                .Select(s => new { s.Plan!.Name, s.Plan.StorageGb, s.Plan.MaxUsers }).FirstOrDefault(),
            SyncedActive = c.Users.Count(u => u.Status == "Active"),
            SyncedTotal = c.Users.Count
        }).ToListAsync();

        var since = DateTime.UtcNow.AddDays(-30);
        // Số đo mới nhất và số đo cách đây ~30 ngày của mỗi công ty (để tính tốc độ tăng)
        var latest = await _db.UsageSnapshots.AsNoTracking().Where(s => s.Error == null)
            .GroupBy(s => s.TenantCode).Select(g => g.OrderByDescending(s => s.At).First()).ToListAsync();
        var old = await _db.UsageSnapshots.AsNoTracking().Where(s => s.Error == null && s.At <= since)
            .GroupBy(s => s.TenantCode).Select(g => g.OrderByDescending(s => s.At).First()).ToListAsync();
        var first = await _db.UsageSnapshots.AsNoTracking().Where(s => s.Error == null && s.At > since)
            .GroupBy(s => s.TenantCode).Select(g => g.OrderBy(s => s.At).First()).ToListAsync();
        double Growth(string code, double nowGb)
        {
            var o = old.FirstOrDefault(s => s.TenantCode == code) ?? first.FirstOrDefault(s => s.TenantCode == code);
            return o == null ? 0 : Math.Round(nowGb - o.TotalGb, 4);
        }

        var rows = new List<UsageRow>();
        foreach (var x in list)
        {
            var c = x.C;
            var snap = latest.FirstOrDefault(s => s.TenantCode == c.Code);
            rows.Add(new UsageRow
            {
                CompanyId = c.Id, Name = c.Name, Code = c.Code, Country = c.Country, CountryCode = c.CountryCode, Status = c.Status, IsDemo = c.IsDemo,
                Plan = x.Sub?.Name ?? "", QuotaGb = x.Sub?.StorageGb ?? 0, MaxUsers = x.Sub?.MaxUsers ?? 0,
                DbGb = c.DbSizeGb, FilesGb = c.FileSizeGb, UsedGb = c.StorageUsedGb, FileCount = c.FileCount,
                // Đã có số đo từ DB khách → dùng số đo; chưa có → bản sao đồng bộ sup.CompanyUsers
                Users = snap != null ? snap.Users : x.SyncedTotal,
                ActiveUsers = snap != null ? snap.ActiveUsers : x.SyncedActive,
                ActiveUsers7d = snap?.ActiveUsers7d ?? 0, ActiveUsers30d = snap?.ActiveUsers30d ?? 0,
                Inspections = snap?.Inspections ?? 0, InspectionsThisMonth = snap?.InspectionsThisMonth ?? 0, OpenCapa = snap?.OpenCapa ?? 0,
                MeasuredAt = c.StorageMeasuredAt, LastActivityAt = c.LastActivityAt, LimitOverrideUntil = c.LimitOverrideUntil, RegisteredAt = c.RegisteredAt,
                Growth30Gb = snap == null ? 0 : Growth(c.Code, c.StorageUsedGb)
            });
        }

        if (includeLegacy && latest.FirstOrDefault(s => s.CompanyId == null) is { } lg)
        {
            rows.Add(new UsageRow
            {
                IsLegacy = true, Name = "Legacy (main database)", Code = lg.TenantCode, Status = "Legacy",
                DbGb = (lg.DbDataMb + lg.DbLogMb) / 1024, FilesGb = lg.FilesMb / 1024, UsedGb = lg.TotalGb, FileCount = lg.FileCount,
                Users = lg.Users, ActiveUsers = lg.ActiveUsers, ActiveUsers7d = lg.ActiveUsers7d, ActiveUsers30d = lg.ActiveUsers30d,
                Inspections = lg.Inspections, InspectionsThisMonth = lg.InspectionsThisMonth, OpenCapa = lg.OpenCapa,
                MeasuredAt = lg.At, LastActivityAt = lg.LastActivityAt, Growth30Gb = Growth(lg.TenantCode, lg.TotalGb)
            });
        }
        return rows;
    }

    public async Task<PlatformUsage> PlatformAsync(List<UsageRow>? rows = null)
    {
        rows ??= await RowsAsync();
        var s = await _settings.AllAsync();
        var disk = await _db.DiskSnapshots.AsNoTracking().OrderByDescending(d => d.At).FirstOrDefaultAsync();
        var real = rows.Where(r => !r.IsDemo).ToList();
        return new PlatformUsage
        {
            UsedGb = real.Sum(r => r.UsedGb),
            AllocatedGb = real.Where(r => r.Status is CompanyStatus.Active or CompanyStatus.Overdue).Sum(r => r.QuotaGb),
            DiskTotalGb = disk?.TotalGb ?? 0, DiskFreeGb = disk?.FreeGb ?? 0, UploadsGb = disk?.UploadsGb ?? 0,
            DiskHost = disk?.Host, DiskDrive = disk?.Drive, DiskMeasuredAt = disk?.At,
            ExternalGb = double.TryParse(s.GetValueOrDefault("usage.externalCapacityGb"), NumberStyles.Any, CultureInfo.InvariantCulture, out var e) ? Math.Max(0, e) : 0,
            LastRunAt = ParseUtc(s.GetValueOrDefault("usage.lastRunAt")), RequestedAt = ParseUtc(s.GetValueOrDefault("usage.measureRequestedAt")),
            Tenants = real.Count(r => !r.IsLegacy),
            ActiveTenants = real.Count(r => !r.IsLegacy && r.Status is CompanyStatus.Active or CompanyStatus.Overdue),
            TotalUsers = real.Sum(r => r.Users), ActiveUsers = real.Sum(r => r.ActiveUsers),
            OverStorage = real.Count(IsOverStorage), OverUsers = real.Count(IsOverUsers), NearLimit = real.Count(IsNear),
            Inactive = real.Count(r => !r.IsLegacy && r.Status == CompanyStatus.Active && IsInactive(r))
        };
    }

    /// <summary>Tổng dung lượng theo ngày (mỗi ngày lấy số đo cuối của từng khách). <paramref name="code"/> = 1 khách.</summary>
    public async Task<List<(DateTime Day, double Gb)>> TrendAsync(int days = 90, string? code = null)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days + 1);
        var q = _db.UsageSnapshots.AsNoTracking().Where(s => s.Error == null && s.At >= from);
        if (code != null) q = q.Where(s => s.TenantCode == code);
        var snaps = await q.Select(s => new { s.TenantCode, s.At, s.DbDataMb, s.DbLogMb, s.FilesMb }).ToListAsync();
        return snaps.GroupBy(s => s.At.Date).OrderBy(g => g.Key)
            .Select(g => (g.Key, Math.Round(g.GroupBy(x => x.TenantCode).Sum(t => { var l = t.OrderByDescending(x => x.At).First(); return (l.DbDataMb + l.DbLogMb + l.FilesMb) / 1024; }), 3)))
            .ToList();
    }

    /// <summary>Super Admin bấm "Measure now": API (UsageWorker) thấy yêu cầu mới hơn lần đo cuối thì đo ngay (kiểm tra mỗi 30 giây).</summary>
    public Task RequestMeasureAsync() => _settings.SetManyAsync(new Dictionary<string, string> { ["usage.measureRequestedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) });

    public static DateTime? ParseUtc(string? v) =>
        DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;

    public static string Size(double gb) => gb >= 1024 ? (gb / 1024).ToString("0.##", CultureInfo.InvariantCulture) + " TB"
        : gb >= 1 ? gb.ToString("0.##", CultureInfo.InvariantCulture) + " GB"
        : (gb * 1024).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
}
