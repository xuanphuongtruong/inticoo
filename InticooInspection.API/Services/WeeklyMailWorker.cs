using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InticooInspection.API.Services.Tenancy;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Background worker chạy mỗi 5 phút. Đọc cấu hình từ DB (qua MailConfigProvider).
    /// Khi đến đúng thứ + giờ + phút (giờ VN) đã cấu hình, kích hoạt:
    ///   1. SendWeeklyVendorMailsAsync()
    ///   2. SendWeeklyCustomerMailsAsync()
    /// Chống gửi trùng bằng MailLogs (1 ngày 1 lần cho mỗi loại).
    /// </summary>
    public class WeeklyMailWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<WeeklyMailWorker> _logger;

        // Giờ VN
        private static readonly TimeZoneInfo VnTz =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

        public WeeklyMailWorker(IServiceProvider sp, ILogger<WeeklyMailWorker> logger)
        {
            _sp     = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WeeklyMailWorker đã khởi động");

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { return; }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            do
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi WeeklyMailWorker");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        // Mỗi công ty khách có DB riêng → chạy lần lượt cho DB gốc + từng công ty đang hoạt động.
        private async Task TickAsync(CancellationToken ct)
        {
            foreach (var tenant in await TenantMaintenance.AllAsync(_sp, ct))
            {
                try { await TickTenantAsync(tenant, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _logger.LogError(ex, "Lỗi WeeklyMailWorker ở công ty {Tenant}", tenant?.Code ?? "(DB gốc)"); }
            }
        }

        private async Task TickTenantAsync(TenantInfo? tenant, CancellationToken ct)
        {
            await using var scope = TenantMaintenance.CreateScope(_sp, tenant);
            var provider = scope.ServiceProvider.GetRequiredService<IMailConfigProvider>();
            var cfg      = await provider.GetAsync(ct);

            if (!cfg.WeeklyJobEnabled)
                return;

            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz);

            if ((int)nowVn.DayOfWeek != cfg.SendDayOfWeek) return;

            var nowMinutes    = nowVn.Hour * 60 + nowVn.Minute;
            var targetMinutes = cfg.SendHour * 60 + cfg.SendMinute;

            // Cho phép trễ tối đa 30 phút (worker tick 5 phút)
            if (nowMinutes < targetMinutes || nowMinutes > targetMinutes + 30)
                return;

            // Mốc đầu ngày gửi (00:00 giờ VN, đổi sang UTC để so MailLog.SentAt)
            var startOfSendDayVn  = nowVn.Date;
            var startOfSendDayUtc = TimeZoneInfo.ConvertTimeToUtc(startOfSendDayVn, VnTz);

            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mail = scope.ServiceProvider.GetRequiredService<IInspectionMailService>();

            // ── 1) VENDOR ────────────────────────────────────────────
            // Đã gửi vendor hôm nay chưa? Check log có VendorCode KHÔNG bắt đầu bằng "CUS:"
            // và không phải TEST.
            var vendorAlreadySent = await db.MailLogs
                .AsNoTracking()
                .AnyAsync(l => l.SentAt >= startOfSendDayUtc
                            && l.IsSuccess
                            && l.VendorCode != null
                            && !l.VendorCode.StartsWith("CUS:")
                            && !l.VendorCode.StartsWith("TEST"), ct);

            if (vendorAlreadySent)
            {
                _logger.LogDebug("Hôm nay đã gửi mail vendor, bỏ qua phần vendor");
            }
            else
            {
                _logger.LogInformation("Bắt đầu gửi mail VENDOR hàng tuần lúc {time} (VN)", nowVn);
                var vRes = await mail.SendWeeklyVendorMailsAsync(ct);
                _logger.LogInformation(
                    "Weekly run completed (Vendor). Sent: {sent}/{total}, Skipped: {skip}, Failed: {fail}, Inspections: {ins}",
                    vRes.VendorsSent, vRes.VendorsTotal,
                    vRes.VendorsSkipped, vRes.VendorsFailed, vRes.InspectionsTotal);
            }

            // ── 2) CUSTOMER ──────────────────────────────────────────
            var customerAlreadySent = await db.MailLogs
                .AsNoTracking()
                .AnyAsync(l => l.SentAt >= startOfSendDayUtc
                            && l.IsSuccess
                            && l.VendorCode != null
                            && l.VendorCode.StartsWith("CUS:"), ct);

            if (customerAlreadySent)
            {
                _logger.LogDebug("Hôm nay đã gửi mail customer, bỏ qua phần customer");
            }
            else
            {
                _logger.LogInformation("Bắt đầu gửi mail CUSTOMER hàng tuần lúc {time} (VN)", nowVn);
                var cRes = await mail.SendWeeklyCustomerMailsAsync(ct);
                _logger.LogInformation(
                    "Weekly run completed (Customer). Sent: {sent}/{total}, Skipped: {skip}, Failed: {fail}, Inspections: {ins}",
                    cRes.VendorsSent, cRes.VendorsTotal,
                    cRes.VendorsSkipped, cRes.VendorsFailed, cRes.InspectionsTotal);
            }
        }
    }
}
