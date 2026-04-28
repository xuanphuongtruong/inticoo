namespace InticooInspection.API.Services
{
    /// <summary>
    /// Background worker chạy mỗi 30 phút. Nếu đang là thứ Hai, giờ >= 8:00 (giờ VN)
    /// và TUẦN NÀY CHƯA GỬI thì sẽ kích hoạt SendWeeklyVendorMailsAsync().
    /// Trạng thái "đã gửi tuần này" được kiểm tra qua bảng MailLogs.
    /// </summary>
    public class WeeklyMailWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<WeeklyMailWorker> _logger;
        private readonly IConfiguration _config;

        // Giờ VN
        private static readonly TimeZoneInfo VnTz =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

        public WeeklyMailWorker(
            IServiceProvider sp,
            IConfiguration config,
            ILogger<WeeklyMailWorker> logger)
        {
            _sp     = sp;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WeeklyMailWorker đã khởi động");

            // Đợi 1 phút sau khi app start để DB sẵn sàng
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { return; }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
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
            // Cấu hình: thứ + giờ chạy (mặc định: Thứ Hai 8h sáng)
            var sendDayOfWeek = (DayOfWeek)_config.GetValue<int>("MailSettings:SendDayOfWeek", (int)DayOfWeek.Monday);
            var sendHour      = _config.GetValue<int>("MailSettings:SendHour", 8);
            var enabled       = _config.GetValue<bool>("MailSettings:WeeklyJobEnabled", true);

            if (!enabled)
                return;

            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz);

            if (nowVn.DayOfWeek != sendDayOfWeek || nowVn.Hour < sendHour)
                return;

            // Mốc đầu tuần (00:00 thứ Hai theo giờ VN, đổi sang UTC để so với MailLogs)
            var startOfWeekVn  = nowVn.Date;
            var startOfWeekUtc = TimeZoneInfo.ConvertTimeToUtc(startOfWeekVn, VnTz);

            await using var scope = _sp.CreateAsyncScope();
            var db   = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
            var mail = scope.ServiceProvider.GetRequiredService<IInspectionMailService>();

            // Đã gửi trong tuần này chưa? (kiểm tra MailLogs)
            var alreadySent = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .AnyAsync(db.MailLogs, l => l.SentAt >= startOfWeekUtc && l.IsSuccess, ct);

            if (alreadySent)
            {
                _logger.LogDebug("Tuần này đã gửi mail vendor, bỏ qua");
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
