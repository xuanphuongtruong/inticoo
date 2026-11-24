using System.Globalization;
using InticooInspection.API.Services.Tenancy;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services.Usage
{
    /// <summary>
    /// Đo mức sử dụng của từng công ty khách + DB gốc rồi ghi vào DB Super cho Super Admin:
    /// kích thước DB (sys.database_files), dung lượng/số file upload (wwwroot/uploads/t/{mã}), người dùng, hoạt động,
    /// số phiếu kiểm tra / CLC / CAPA mở / nhà cung cấp / sản phẩm; kèm dung lượng ổ đĩa server.
    /// Chạy theo chu kỳ <c>usage.intervalHours</c> (sup.Settings) hoặc ngay khi Super Admin bấm "Measure now".
    /// Tắt bằng cấu hình <c>Usage:WorkerEnabled=false</c> (vd. instance API phụ không giữ file upload của khách).
    /// </summary>
    public sealed class UsageWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly SuperUsageStore _store;
        private readonly UsageMeter _meter;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;
        private readonly ILogger<UsageWorker> _log;

        public UsageWorker(IServiceProvider sp, SuperUsageStore store, UsageMeter meter, IWebHostEnvironment env, IConfiguration cfg, ILogger<UsageWorker> log)
        { _sp = sp; _store = store; _meter = meter; _env = env; _cfg = cfg; _log = log; }

        private string WebRoot => _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        private string UploadsRoot => Path.Combine(WebRoot, "uploads");

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (!_store.Enabled || !_cfg.GetValue("Usage:WorkerEnabled", true)) { _log.LogInformation("UsageWorker tắt (thiếu ConnectionStrings:Super hoặc Usage:WorkerEnabled=false)"); return; }
            try { await Task.Delay(TimeSpan.FromSeconds(_cfg.GetValue("Usage:StartDelaySeconds", 60)), ct); } catch (OperationCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var s = await _store.SettingsAsync(ct);
                    var hours = Math.Clamp(int.TryParse(s["usage.intervalHours"], out var h) ? h : 6, 1, 168);
                    var last = Parse(s["usage.lastRunAt"]);
                    var requested = Parse(s["usage.measureRequestedAt"]);
                    var now = DateTime.UtcNow;
                    var due = last == null || now - last.Value >= TimeSpan.FromHours(hours) || (requested != null && requested > last);
                    if (due && await _store.ClaimRunAsync(s["usage.lastRunAt"], now, ct))
                        await RunAsync(now, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { _log.LogError(ex, "UsageWorker lỗi"); }

                try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { break; }
            }
        }

        private static DateTime? Parse(string? v) =>
            DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;

        public async Task RunAsync(DateTime at, CancellationToken ct)
        {
            var tenants = await TenantMaintenance.AllAsync(_sp, ct);
            var n = 0;
            foreach (var t in tenants)
            {
                ct.ThrowIfCancellationRequested();
                var r = await MeasureAsync(t, at, ct);
                try { await _store.SaveSnapshotAsync(r, ct); n++; if (t != null && r.Error == null) _meter.Reset(t.Code); }
                catch (Exception ex) { _log.LogError(ex, "Ghi số đo sử dụng lỗi ({Code})", r.TenantCode); }
            }
            await MeasureDiskAsync(at, ct);
            _log.LogInformation("Đã đo mức sử dụng {N} công ty", n);
        }

        private async Task<UsageResult> MeasureAsync(TenantInfo? t, DateTime at, CancellationToken ct)
        {
            var r = new UsageResult { CompanyId = t?.Id, TenantCode = t?.Code ?? "inticoo", At = at };
            // File: công ty khách ở uploads/t/{mã}; DB gốc ở uploads/ (trừ thư mục t/ của các công ty khách)
            try
            {
                var (bytes, count) = t == null ? DirSize(UploadsRoot, Path.Combine(UploadsRoot, "t")) : DirSize(Path.Combine(UploadsRoot, "t", t.Code), null);
                r.FilesMb = Math.Round(bytes / 1024d / 1024d, 3); r.FileCount = count;
            }
            catch (Exception ex) { _log.LogWarning(ex, "Đo file lỗi ({Code})", r.TenantCode); }

            try
            {
                await using var scope = TenantMaintenance.CreateScope(_sp, t);
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var files = await db.Database.SqlQueryRaw<DbFileSize>(
                    "SELECT CAST(type AS int) AS FileType, CAST(SUM(CAST(size AS bigint)) * 8 / 1024.0 AS float) AS Mb FROM sys.database_files GROUP BY type")
                    .ToListAsync(ct);
                r.DbDataMb = Math.Round(files.Where(f => f.FileType == 0).Sum(f => f.Mb), 3);
                r.DbLogMb = Math.Round(files.Where(f => f.FileType == 1).Sum(f => f.Mb), 3);

                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                r.Users = await db.Users.CountAsync(ct);
                r.ActiveUsers = await db.Users.CountAsync(u => u.IsActive, ct);
                r.LockedUsers = await db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow, ct);
                r.ActiveUsers7d = await db.Users.CountAsync(u => u.LastLoginAt >= now.AddDays(-7), ct);
                r.ActiveUsers30d = await db.Users.CountAsync(u => u.LastLoginAt >= now.AddDays(-30), ct);
                r.LastLoginAt = await db.Users.MaxAsync(u => u.LastLoginAt, ct);
                r.Inspections = await db.Inspections.CountAsync(ct);
                r.InspectionsThisMonth = await db.Inspections.CountAsync(i => i.CreatedAt >= monthStart, ct);
                r.ContainerChecks = await db.ContainerChecks.CountAsync(ct);
                r.OpenCapa = await db.DefectCapas.CountAsync(c => c.Status != "Closed", ct);
                r.Vendors = await db.Vendors.CountAsync(ct);
                r.Products = await db.Products.CountAsync(ct);
                var lastInspection = await db.Inspections.MaxAsync(i => (DateTime?)i.CreatedAt, ct);
                r.LastActivityAt = new[] { r.LastLoginAt, lastInspection }.Where(d => d != null).DefaultIfEmpty(null).Max();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Đo DB lỗi ({Code})", r.TenantCode);
                r.Error = ex.GetBaseException().Message;
            }
            return r;
        }

        private async Task MeasureDiskAsync(DateTime at, CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(UploadsRoot);
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(UploadsRoot))!);
                var (bytes, _) = DirSize(UploadsRoot, null);
                const double gb = 1024d * 1024 * 1024;
                await _store.SaveDiskAsync(Environment.MachineName, drive.Name, drive.TotalSize / gb, drive.AvailableFreeSpace / gb, bytes / gb, at, ct);
            }
            catch (Exception ex) { _log.LogWarning(ex, "Đo ổ đĩa server lỗi"); }
        }

        /// <summary>Tổng byte + số file trong thư mục (đệ quy), bỏ qua thư mục <paramref name="exclude"/>.</summary>
        private static (long Bytes, int Count) DirSize(string dir, string? exclude)
        {
            if (!Directory.Exists(dir)) return (0, 0);
            long bytes = 0; var count = 0;
            var stack = new Stack<string>(); stack.Push(dir);
            var ex = exclude == null ? null : Path.GetFullPath(exclude).TrimEnd(Path.DirectorySeparatorChar);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                if (ex != null && string.Equals(Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar), ex, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(d)) { try { bytes += new FileInfo(f).Length; count++; } catch { } }
                    foreach (var s in Directory.EnumerateDirectories(d)) stack.Push(s);
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }
            return (bytes, count);
        }

        public sealed class DbFileSize { public int FileType { get; set; } public double Mb { get; set; } }
    }
}
