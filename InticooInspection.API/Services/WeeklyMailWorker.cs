using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Background worker chạy mỗi 5 phút. Đọc cấu hình từ DB (qua MailConfigProvider).
    /// Khi đến đúng thứ + giờ + phút (giờ VN) đã cấu hình, kích hoạt
    /// SendWeeklyVendorMailsAsync(). Chống gửi trùng bằng MailLogs (1 ngày 1 lần).
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

            // Tick mỗi 5 phút - chính xác hơn cho cấu hình minute
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

        private async Task TickAsync(CancellationToken ct)
        {
            await using var scope = _sp.CreateAsyncScope();
            var provider = scope.ServiceProvider.GetRequiredService<IMailConfigProvider>();
            var cfg      = await provider.GetAsync(ct);

            if (!cfg.WeeklyJobEnabled)
                return;

            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz);

            if ((int)nowVn.DayOfWeek != cfg.SendDayOfWeek) return;

            // Tính tổng số phút từ đầu ngày để so sánh với cấu hình (giờ + phút)
            var nowMinutes    = nowVn.Hour * 60 + nowVn.Minute;
            var targetMinutes = cfg.SendHour * 60 + cfg.SendMinute;

            // Cho phép trễ tối đa 30 phút (để worker tick 5 phút có khả năng bắt được)
            // Phải sau hoặc bằng giờ target, và không quá 30 phút sau
            if (nowMinutes < targetMinutes || nowMinutes > targetMinutes + 30)
                return;

            // Mốc đầu ngày gửi (00:00 giờ VN, đổi sang UTC để so MailLog.SentAt)
            var startOfSendDayVn  = nowVn.Date;
            var startOfSendDayUtc = TimeZoneInfo.ConvertTimeToUtc(startOfSendDayVn, VnTz);

            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mail = scope.ServiceProvider.GetRequiredService<IInspectionMailService>();

            // Đã gửi hôm nay chưa? Check qua MailLogs
            var alreadySent = await db.MailLogs
                .AsNoTracking()
                .AnyAsync(l => l.SentAt >= startOfSendDayUtc && l.IsSuccess, ct);

            if (alreadySent)
            {
                _logger.LogDebug("Hôm nay đã gửi mail vendor, bỏ qua");
                return;
            }

            _logger.LogInformation("Bắt đầu gửi mail vendor hàng tuần lúc {time} (VN)", nowVn);
            var result = await mail.SendWeeklyVendorMailsAsync(ct);
            _logger.LogInformation(
                "Hoàn tất tuần. Vendors: {sent}/{total}, Skipped: {skip}, Failed: {fail}, Inspections: {ins}",
                result.VendorsSent, result.VendorsTotal,
                result.VendorsSkipped, result.VendorsFailed, result.InspectionsTotal);
        }
    }
}
