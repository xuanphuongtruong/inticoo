namespace InticooInspection.API.Services
{
    /// <summary>
    /// Background worker chạy mỗi 30 phút. Nếu đang là thứ Hai, giờ >= 8:00 (giờ VN)
    /// và TUẦN NÀY CHƯA GỬI thì sẽ kích hoạt SendWeeklyVendorMailsAsync().
    /// (Tạm thời) Trạng thái "đã gửi tuần này" được giữ trong memory — sẽ reset
    /// khi app restart. Khi entity MailLog được tạo, mở lại block check DB ở dưới.
    /// </summary>
    public class WeeklyMailWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<WeeklyMailWorker> _logger;
        private readonly IConfiguration _config;

        // In-memory: lưu mốc đầu tuần (UTC) đã gửi thành công gần nhất
        private DateTime? _lastSentWeekStartUtc;

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

            // Mốc đầu tuần (00:00 thứ Hai theo giờ VN, đổi sang UTC để so với log)
            var startOfWeekVn  = nowVn.Date;
            var startOfWeekUtc = TimeZoneInfo.ConvertTimeToUtc(startOfWeekVn, VnTz);

            await using var scope = _sp.CreateAsyncScope();
            // var db   = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
            var mail = scope.ServiceProvider.GetRequiredService<IInspectionMailService>();

            // ── Đã gửi tuần này chưa? ──
            // TODO: Khi MailLog đã có, đổi sang check DB:
            // var alreadySent = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            //     .AnyAsync(db.MailLogs, l => l.SentAt >= startOfWeekUtc && l.IsSuccess, ct);
            //
            // Tạm thời dùng in-memory flag — reset khi app restart (chấp nhận được vì
            // worker chạy 30 phút/lần, ít khi restart đúng giờ gửi).
            var alreadySent = _lastSentWeekStartUtc.HasValue
                              && _lastSentWeekStartUtc.Value >= startOfWeekUtc;

            if (alreadySent)
            {
                _logger.LogDebug("Tuần này đã gửi mail vendor (in-memory flag), bỏ qua");
                return;
            }

            _logger.LogInformation("Bắt đầu gửi mail vendor hàng tuần lúc {time} (VN)", nowVn);
            var result = await mail.SendWeeklyVendorMailsAsync(ct);
            _logger.LogInformation(
                "Hoàn tất tuần. Vendors: {sent}/{total}, Skipped: {skip}, Failed: {fail}, Inspections: {ins}",
                result.VendorsSent, result.VendorsTotal,
                result.VendorsSkipped, result.VendorsFailed, result.InspectionsTotal);

            // Đánh dấu tuần này đã gửi (chỉ khi có ít nhất 1 vendor được gửi thành công)
            if (result.VendorsSent > 0)
                _lastSentWeekStartUtc = startOfWeekUtc;
        }
    }
}
