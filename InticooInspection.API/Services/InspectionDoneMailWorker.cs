using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Background worker chạy mỗi 10 phút. Quét các Inspection:
    ///   - Status = Completed (Done)
    ///   - Hoàn thành (CompletedAt) TRONG HÔM NAY (giờ VN)
    ///   - TinhTrangGuiMail = 0 (chưa gửi mail)
    ///   - Có khách hàng kèm Email hợp lệ (mới gửi được)
    /// Với mỗi inspection: gửi mail "Inspection Report Completed"
    /// (tái dùng IInspectionMailService.SendForInspectionAsync) rồi set cờ
    /// TinhTrangGuiMail = 1. Nếu gửi lỗi (SMTP...) thì giữ cờ = 0 để lần quét
    /// sau gửi lại (trong phạm vi cùng ngày).
    /// </summary>
    public class InspectionDoneMailWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<InspectionDoneMailWorker> _logger;

        // Giờ VN — dùng chung quy ước với WeeklyMailWorker
        private static readonly TimeZoneInfo VnTz =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

        public InspectionDoneMailWorker(IServiceProvider sp, ILogger<InspectionDoneMailWorker> logger)
        {
            _sp     = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("InspectionDoneMailWorker đã khởi động (quét mỗi 10 phút)");

            // Chờ app warm up; lệch 2 phút so với WeeklyMailWorker để không trùng nhịp
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
            catch (OperationCanceledException) { return; }

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
            do
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi InspectionDoneMailWorker");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task TickAsync(CancellationToken ct)
        {
            await using var scope = _sp.CreateAsyncScope();
            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mail = scope.ServiceProvider.GetRequiredService<IInspectionMailService>();

            // Mốc đầu/cuối ngày hôm nay theo giờ VN, đổi sang UTC để so với CompletedAt
            // (CompletedAt lưu theo DateTime.UtcNow lúc bấm Done).
            var nowVn        = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz);
            var todayStartVn = nowVn.Date;
            var utcFrom      = TimeZoneInfo.ConvertTimeToUtc(todayStartVn, VnTz);
            var utcTo        = TimeZoneInfo.ConvertTimeToUtc(todayStartVn.AddDays(1), VnTz);

            // Chỉ lấy inspection Done hôm nay, chưa gửi, và khách hàng có email hợp lệ
            // (lọc sẵn để không gửi hụt lặp lại + không tạo log lỗi rác mỗi 10 phút).
            var pending = await (
                from i in db.Inspections.AsNoTracking()
                where i.Status == InspectionStatus.Completed
                   && i.TinhTrangGuiMail == 0
                   && i.CompletedAt != null
                   && i.CompletedAt >= utcFrom
                   && i.CompletedAt <  utcTo
                   && i.CustomerId != null
                join c in db.Customers.AsNoTracking()
                    on i.CustomerId equals c.CustomerId
                where c.Email != null && c.Email != ""
                orderby i.CompletedAt
                select new { i.Id, i.JobNumber }
            ).ToListAsync(ct);

            if (pending.Count == 0)
                return;

            _logger.LogInformation("InspectionDoneMailWorker: {count} inspection Done hôm nay cần gửi mail", pending.Count);

            int sent = 0, failed = 0;
            foreach (var insp in pending)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var (ok, error, toEmail) = await mail.SendForInspectionAsync(insp.Id, null, ct);

                    if (ok)
                    {
                        // Cập nhật cờ trực tiếp (không qua change tracker) để chắc chắn
                        // không gửi lại ở lần quét sau.
                        await db.Inspections
                            .Where(x => x.Id == insp.Id)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TinhTrangGuiMail, 1), ct);

                        sent++;
                        _logger.LogInformation("Đã gửi mail Done cho inspection {id} ({job}) tới {email}",
                            insp.Id, insp.JobNumber, toEmail);
                    }
                    else
                    {
                        failed++;
                        _logger.LogWarning("Gửi mail Done THẤT BẠI cho inspection {id} ({job}): {err} — sẽ thử lại lần sau",
                            insp.Id, insp.JobNumber, error);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Lỗi gửi mail Done cho inspection {id} ({job})", insp.Id, insp.JobNumber);
                }

                // Nhẹ tay với SMTP, giống WeeklyMailWorker
                await Task.Delay(500, ct);
            }

            _logger.LogInformation("InspectionDoneMailWorker hoàn tất: đã gửi {sent}, thất bại {failed}", sent, failed);
        }
    }
}
