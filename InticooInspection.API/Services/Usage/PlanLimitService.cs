using System.Collections.Concurrent;
using InticooInspection.API.Services.Tenancy;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InticooInspection.API.Services.Usage
{
    /// <summary>Vượt hạn mức gói → middleware trả 403 kèm thông báo (Client dịch qua TA()).</summary>
    public sealed class PlanLimitException : Exception
    {
        public PlanLimitException(string message) : base(message) { }
    }

    /// <summary>Dung lượng file đã ghi thêm kể từ lần đo gần nhất (theo mã công ty) — để chặn kịp giữa 2 lần đo.</summary>
    public sealed class UsageMeter
    {
        private readonly ConcurrentDictionary<string, long> _bytes = new();
        public void Add(string code, long bytes) { if (bytes > 0) _bytes.AddOrUpdate(code, bytes, (_, v) => v + bytes); }
        public long Get(string code) => _bytes.TryGetValue(code, out var v) ? v : 0;
        public void Reset(string code) => _bytes.TryRemove(code, out _);
    }

    /// <summary>
    /// Kiểm tra hạn mức gói của công ty đang gọi API (người dùng đang hoạt động, dung lượng).
    /// DB gốc (khách cũ, không thuộc gói nào), thiếu DB Super, tắt "usage.enforce" hoặc Super Admin tạm bỏ chặn → không chặn.
    /// </summary>
    public sealed class PlanLimitService
    {
        public const string UserLimitMessage = "The user limit of your plan has been reached. Deactivate a user or contact Inticoo to upgrade your plan.";
        public const string StorageLimitMessage = "The storage limit of your plan has been reached. Delete unused files or contact Inticoo to upgrade your plan.";
        private const double Gb = 1024d * 1024 * 1024;

        private readonly TenantContext _tenant;
        private readonly SuperUsageStore _store;
        private readonly IMemoryCache _cache;
        private readonly UsageMeter _meter;
        private readonly IServiceProvider _sp;
        private readonly ILogger<PlanLimitService> _log;

        public PlanLimitService(TenantContext tenant, SuperUsageStore store, IMemoryCache cache, UsageMeter meter, IServiceProvider sp, ILogger<PlanLimitService> log)
        { _tenant = tenant; _store = store; _cache = cache; _meter = meter; _sp = sp; _log = log; }

        public sealed record Status(bool Applies, bool Enforced, string? PlanName, int MaxUsers, int ActiveUsers, int StorageGb, double UsedGb, int WarnPercent);

        private async Task<(PlanLimits? L, bool Enforce, int Warn)> LoadAsync(CancellationToken ct)
        {
            if (_tenant.Tenant is not { } t || !_store.Enabled) return (null, false, 80);
            return await _cache.GetOrCreateAsync("planlimits:" + t.Id, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                try
                {
                    var s = await _store.SettingsAsync(ct);
                    var l = await _store.LimitsAsync(t.Id, ct);
                    return (l, s["usage.enforce"].Equals("true", StringComparison.OrdinalIgnoreCase), int.TryParse(s["usage.warnPercent"], out var w) ? w : 80);
                }
                catch (Exception ex)
                {
                    // DB Super lỗi không được chặn công việc của khách
                    _log.LogWarning(ex, "Không đọc được hạn mức gói ({Code}) — tạm không chặn", t.Code);
                    e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                    return ((PlanLimits?)null, false, 80);
                }
            });
        }

        private static bool Overridden(PlanLimits l) => l.OverrideUntil is { } u && u >= DateTime.UtcNow;

        /// <summary>Số liệu cho Client hiện "đã dùng X / Y".</summary>
        public async Task<Status> StatusAsync(CancellationToken ct = default)
        {
            var (l, enforce, warn) = await LoadAsync(ct);
            if (l == null) return new Status(false, false, null, 0, 0, 0, 0, warn);
            var db = _sp.GetRequiredService<AppDbContext>();
            var active = await db.Users.CountAsync(u => u.IsActive, ct);
            var used = l.UsedGb + _meter.Get(_tenant.Key) / Gb;
            return new Status(true, enforce && !Overridden(l), l.PlanName, l.MaxUsers, active, l.StorageGb, Math.Round(used, 3), warn);
        }

        /// <summary>Gọi trước khi tạo / kích hoạt <paramref name="adding"/> người dùng. Vượt → PlanLimitException.</summary>
        public async Task EnsureUsersAsync(int adding = 1, CancellationToken ct = default)
        {
            var (l, enforce, _) = await LoadAsync(ct);
            if (l == null || !enforce || Overridden(l) || l.MaxUsers <= 0 || adding <= 0) return;
            var db = _sp.GetRequiredService<AppDbContext>();
            var active = await db.Users.CountAsync(u => u.IsActive, ct);
            if (active + adding > l.MaxUsers) throw new PlanLimitException(UserLimitMessage);
        }

        /// <summary>Gọi trước khi ghi file <paramref name="bytes"/> byte. Vượt → PlanLimitException.</summary>
        public async Task EnsureStorageAsync(long bytes, CancellationToken ct = default)
        {
            var (l, enforce, _) = await LoadAsync(ct);
            if (l == null || !enforce || Overridden(l) || l.StorageGb <= 0) return;
            var used = l.UsedGb * Gb + _meter.Get(_tenant.Key);
            if (used + Math.Max(0, bytes) > l.StorageGb * Gb) throw new PlanLimitException(StorageLimitMessage);
        }

        /// <summary>Ghi nhận dung lượng vừa ghi (để chặn kịp trước lần đo kế tiếp).</summary>
        public void Track(long bytes) { if (_tenant.Tenant != null) _meter.Add(_tenant.Key, bytes); }
    }

    /// <summary>Đổi PlanLimitException thành 403 JSON {success:false, message, error, code:"plan_limit"}.</summary>
    public sealed class PlanLimitMiddleware
    {
        private readonly RequestDelegate _next;
        public PlanLimitMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext ctx)
        {
            try { await _next(ctx); }
            catch (PlanLimitException ex) when (!ctx.Response.HasStarted)
            {
                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { success = false, message = ex.Message, error = ex.Message, code = "plan_limit" });
            }
        }
    }
}
