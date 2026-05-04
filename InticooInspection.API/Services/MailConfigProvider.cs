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

        // Cache đơn giản trong memory (1 phút)
        private MailConfigSnapshot? _cached;
        private DateTime _cachedAt;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
        private readonly SemaphoreSlim _lock = new(1, 1);

        public MailConfigProvider(
            IServiceProvider sp,
            IConfiguration config,
            ILogger<MailConfigProvider> logger)
        {
            _sp     = sp;
            _config = config;
            _logger = logger;
        }

        public async Task<MailConfigSnapshot> GetAsync(CancellationToken ct = default)
        {
            // Cache hit
            if (_cached != null && DateTime.UtcNow - _cachedAt < CacheDuration)
                return _cached;

            await _lock.WaitAsync(ct);
            try
            {
                // Double-check sau khi lock
                if (_cached != null && DateTime.UtcNow - _cachedAt < CacheDuration)
                    return _cached;

                _cached    = await BuildSnapshotAsync(ct);
                _cachedAt  = DateTime.UtcNow;
                return _cached;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void InvalidateCache()
        {
            _cached   = null;
            _cachedAt = DateTime.MinValue;
        }

        // ── Build snapshot: ưu tiên DB → fallback Env Var ──
        private async Task<MailConfigSnapshot> BuildSnapshotAsync(CancellationToken ct)
        {
            MailConfig? dbConfig = null;
            try
            {
                await using var scope = _sp.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
