using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Snapshot cấu hình mail tại 1 thời điểm. Các service khác chỉ cần inject
    /// IMailConfigProvider rồi gọi GetAsync() để lấy snapshot này.
    /// </summary>
    public class MailConfigSnapshot
    {
        public string SmtpHost    { get; init; } = "";
        public int    SmtpPort    { get; init; }
        public bool   UseSsl      { get; init; }
        public string SenderEmail { get; init; } = "";
        public string SenderName  { get; init; } = "";
        public string Username    { get; init; } = "";

        // Password LUÔN từ Environment Variable, không bao giờ từ DB
        public string Password    { get; init; } = "";

        public int  LookAheadDays    { get; init; }
        public int  SendDayOfWeek    { get; init; }
        public int  SendHour         { get; init; }
        public int  SendMinute       { get; init; }
        public bool WeeklyJobEnabled { get; init; }

        public DateTime UpdatedAt { get; init; }
        public string?  UpdatedBy { get; init; }

        // Đánh dấu config đang đến từ đâu (giúp UI hiển thị)
        public bool FromDatabase { get; init; }
    }

    public interface IMailConfigProvider
    {
        /// <summary>Lấy cấu hình hiện tại (có cache 1 phút).</summary>
        Task<MailConfigSnapshot> GetAsync(CancellationToken ct = default);

        /// <summary>Gọi sau khi PUT cập nhật để invalidate cache ngay lập tức.</summary>
        void InvalidateCache();
    }

    public class MailConfigProvider : IMailConfigProvider
    {
        private readonly IServiceProvider _sp;
        private readonly IConfiguration   _config;
        private readonly ILogger<MailConfigProvider> _logger;

        // Cache trong memory (1 phút), TÁCH THEO CÔNG TY: mỗi công ty khách có DB riêng → MailConfig riêng.
        // Service là scoped (theo request/worker scope), cache là static để sống qua các request.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (MailConfigSnapshot Snap, DateTime At)> _cache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private readonly string _key;

        public MailConfigProvider(
            IServiceProvider sp,
            IConfiguration config,
            ILogger<MailConfigProvider> logger)
        {
            _sp     = sp;
            _config = config;
            _logger = logger;
            _key    = sp.GetRequiredService<Tenancy.TenantContext>().Key;
        }

        public async Task<MailConfigSnapshot> GetAsync(CancellationToken ct = default)
        {
            // Cache hit
            if (_cache.TryGetValue(_key, out var hit) && DateTime.UtcNow - hit.At < CacheDuration)
                return hit.Snap;

            await _lock.WaitAsync(ct);
            try
            {
                // Double-check sau khi lock
                if (_cache.TryGetValue(_key, out hit) && DateTime.UtcNow - hit.At < CacheDuration)
                    return hit.Snap;

                var snap = await BuildSnapshotAsync(ct);
                _cache[_key] = (snap, DateTime.UtcNow);
                return snap;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void InvalidateCache()
        {
            _cache.TryRemove(_key, out _);
        }

        // ── Build snapshot: ưu tiên DB → fallback Env Var ──
        private async Task<MailConfigSnapshot> BuildSnapshotAsync(CancellationToken ct)
        {
            MailConfig? dbConfig = null;
            try
            {
                // AppDbContext của chính scope hiện tại → đúng DB của công ty đang xử lý
                var db = _sp.GetRequiredService<AppDbContext>();
                dbConfig = await db.MailConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
            }
            catch (Exception ex)
            {
                // DB lỗi (timeout, chưa migrate...) → fallback env var, KHÔNG crash app
                _logger.LogWarning(ex, "Không đọc được MailConfig từ DB, fallback Env Var");
            }

            if (dbConfig != null)
            {
                return new MailConfigSnapshot
                {
                    SmtpHost         = dbConfig.SmtpHost,
                    SmtpPort         = dbConfig.SmtpPort,
                    UseSsl           = dbConfig.UseSsl,
                    SenderEmail      = dbConfig.SenderEmail,
                    SenderName       = dbConfig.SenderName,
                    Username         = dbConfig.Username,
                    Password         = _config["MailSettings:Password"] ?? "", // luôn lấy env
                    LookAheadDays    = dbConfig.LookAheadDays,
                    SendDayOfWeek    = dbConfig.SendDayOfWeek,
                    SendHour         = dbConfig.SendHour,
                    SendMinute       = dbConfig.SendMinute,
                    WeeklyJobEnabled = dbConfig.WeeklyJobEnabled,
                    UpdatedAt        = dbConfig.UpdatedAt,
                    UpdatedBy        = dbConfig.UpdatedBy,
                    FromDatabase     = true
                };
            }

            // Fallback hoàn toàn từ Env Var
            return new MailConfigSnapshot
            {
                SmtpHost         = _config["MailSettings:SmtpHost"]    ?? "smtp.office365.com",
                SmtpPort         = _config.GetValue<int>("MailSettings:SmtpPort", 587),
                UseSsl           = _config.GetValue<bool>("MailSettings:UseSsl", true),
                SenderEmail      = _config["MailSettings:SenderEmail"] ?? "",
                SenderName       = _config["MailSettings:SenderName"]  ?? "Inticoo Inspection",
                Username         = _config["MailSettings:Username"]    ?? "",
                Password         = _config["MailSettings:Password"]    ?? "",
                LookAheadDays    = _config.GetValue<int>("MailSettings:LookAheadDays", 7),
                SendDayOfWeek    = _config.GetValue<int>("MailSettings:SendDayOfWeek", 5),
                SendHour         = _config.GetValue<int>("MailSettings:SendHour", 18),
                SendMinute       = _config.GetValue<int>("MailSettings:SendMinute", 0),
                WeeklyJobEnabled = _config.GetValue<bool>("MailSettings:WeeklyJobEnabled", true),
                UpdatedAt        = DateTime.UtcNow,
                UpdatedBy        = null,
                FromDatabase     = false
            };
        }
    }
}
