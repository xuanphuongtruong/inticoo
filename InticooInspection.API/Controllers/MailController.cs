using InticooInspection.API.Services;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/mail")]
    [Authorize]
    public class MailController : ControllerBase
    {
        private readonly AppDbContext             _db;
        private readonly IInspectionMailService   _mail;
        private readonly IConfiguration           _config;

        public MailController(AppDbContext db, IInspectionMailService mail, IConfiguration config)
        {
            _db     = db;
            _mail   = mail;
            _config = config;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST api/mail/run-weekly
        // Trigger thủ công job hàng tuần (gửi cho tất cả vendor có inspection sắp tới)
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("run-weekly")]
        [AllowAnonymous]
        public async Task<IActionResult> RunWeekly(CancellationToken ct)
        {
            var result = await _mail.SendWeeklyVendorMailsAsync(ct);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST api/mail/send-vendor/{vendorCode}
        // Gửi mail cho 1 vendor cụ thể (vendorCode = Vendor.Code)
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("send-vendor/{vendorCode}")]
        [AllowAnonymous]
        public async Task<IActionResult> SendVendor(string vendorCode, CancellationToken ct)
        {
            var (ok, error, count) = await _mail.SendForVendorAsync(vendorCode, ct);
            if (!ok) return BadRequest(new { success = false, error, inspectionCount = count });
            return Ok(new { success = true, inspectionCount = count });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST api/mail/send-test
        // Gửi 1 mail test tới địa chỉ bất kỳ (KHÔNG có data inspection thật,
        // chỉ để kiểm tra cấu hình SMTP có chạy không)
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("send-test")]
        [AllowAnonymous]
        public async Task<IActionResult> SendTest([FromBody] TestMailRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.ToEmail))
                return BadRequest(new { success = false, error = "Email không được để trống" });

            var subject = req.Subject ?? "[Intico] Email Test - Kiểm tra cấu hình SMTP";
            var html    = $@"<html><body style='font-family:Segoe UI,Arial;color:#333;'>
<div style='max-width:600px;margin:0 auto;padding:24px;background:#f8f9fa;border-radius:8px;'>
  <h2 style='color:#1e6091;'>✅ Email Test thành công!</h2>
  <p>Đây là email test từ <strong>Intico Inspection System</strong>.</p>
  <p>Nếu bạn nhận được email này, cấu hình SMTP đang hoạt động bình thường.</p>
  <hr/>
  <p style='font-size:12px;color:#888;'>
    Gửi lúc: {DateTime.Now:dd/MM/yyyy HH:mm:ss}<br/>
    Người nhận: {WebUtility.HtmlEncode(req.ToEmail)}
  </p>
</div></body></html>";

            try
            {
                var host        = _config["MailSettings:SmtpHost"] ?? "";
                var port        = _config.GetValue<int>("MailSettings:SmtpPort", 587);
                var senderEmail = _config["MailSettings:SenderEmail"] ?? "";
                var senderName  = _config["MailSettings:SenderName"] ?? "Intico Inspection";
                var username    = _config["MailSettings:Username"] ?? "";
                var password    = _config["MailSettings:Password"] ?? "";
                var useSsl      = _config.GetValue<bool>("MailSettings:UseSsl", true);

                using var smtp = new SmtpClient(host, port)
                {
                    EnableSsl      = useSsl,
                    Credentials    = new NetworkCredential(username, password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout        = 30000
                };

                using var msg = new MailMessage
                {
                    From            = new MailAddress(senderEmail, senderName),
                    Subject         = subject,
                    Body            = html,
                    IsBodyHtml      = true,
                    BodyEncoding    = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                msg.To.Add(new MailAddress(req.ToEmail));

                await smtp.SendMailAsync(msg, ct);

                // Log lại
                _db.MailLogs.Add(new MailLog
                {
                    VendorId        = null,
                    VendorCode      = "TEST",
                    ToEmail         = req.ToEmail,
                    Subject         = subject,
                    SentAt          = DateTime.UtcNow,
                    IsSuccess       = true,
                    InspectionCount = 0
                });
                await _db.SaveChangesAsync(ct);

                return Ok(new { success = true, message = "Đã gửi email test thành công" });
            }
            catch (Exception ex)
            {
                _db.MailLogs.Add(new MailLog
                {
                    VendorCode      = "TEST",
                    ToEmail         = req.ToEmail,
                    Subject         = subject,
                    SentAt          = DateTime.UtcNow,
                    IsSuccess       = false,
                    ErrorMessage    = ex.Message,
                    InspectionCount = 0
                });
                await _db.SaveChangesAsync(ct);

                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET api/mail/logs?take=100&vendorCode=&from=&to=
        // Xem lịch sử gửi mail
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("logs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int take = 100,
            [FromQuery] string? vendorCode = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var q = _db.MailLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(vendorCode))
                q = q.Where(l => l.VendorCode == vendorCode);
            if (from.HasValue) q = q.Where(l => l.SentAt >= from.Value);
            if (to.HasValue)   q = q.Where(l => l.SentAt <= to.Value);

            var items = await q
                .OrderByDescending(l => l.SentAt)
                .Take(Math.Min(take, 500))
                .Select(l => new
                {
                    l.Id, l.VendorId, l.VendorCode,
                    l.ToEmail, l.Subject,
                    l.SentAt, l.IsSuccess,
                    l.ErrorMessage, l.InspectionCount
                })
                .ToListAsync();

            return Ok(items);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET api/mail/stats?days=30
        // Thống kê gửi mail trong N ngày qua
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("stats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStats([FromQuery] int days = 30)
        {
            var from = DateTime.UtcNow.AddDays(-days);
            var logs = await _db.MailLogs
                .AsNoTracking()
                .Where(l => l.SentAt >= from)
                .ToListAsync();

            return Ok(new
            {
                total            = logs.Count,
                success          = logs.Count(l => l.IsSuccess),
                failed           = logs.Count(l => !l.IsSuccess),
                inspectionsTotal = logs.Sum(l => l.InspectionCount),
                vendorsUnique    = logs.Where(l => !string.IsNullOrEmpty(l.VendorCode))
                                       .Select(l => l.VendorCode).Distinct().Count(),
                lastRun          = logs.OrderByDescending(l => l.SentAt)
                                       .Select(l => (DateTime?)l.SentAt).FirstOrDefault()
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET api/mail/upcoming-vendors
        // Xem trước danh sách vendor sẽ nhận mail kỳ tới (preview)
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("upcoming-vendors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUpcomingVendors()
        {
            var lookAhead = _config.GetValue<int>("MailSettings:LookAheadDays", 7);
            var fromDate  = DateTime.Today;
            var toDate    = fromDate.AddDays(lookAhead);

            var inspections = await _db.Inspections.AsNoTracking()
                .Where(i => i.InspectionDate >= fromDate
                         && i.InspectionDate <  toDate
                         && (i.Status == InspectionStatus.Pending
                          || i.Status == InspectionStatus.InProgress))
                .Select(i => new { i.VendorId, i.VendorName, i.JobNumber, i.InspectionDate, i.ProductName })
                .ToListAsync();

            var grouped = inspections
                .Where(i => !string.IsNullOrEmpty(i.VendorId))
                .GroupBy(i => i.VendorId!)
                .Select(g => new
                {
                    vendorCode = g.Key,
                    vendorName = g.First().VendorName,
                    inspectionCount = g.Count(),
                    inspections = g.OrderBy(x => x.InspectionDate).Select(x => new
                    {
                        x.JobNumber, x.InspectionDate, x.ProductName
                    })
                })
                .ToList();

            // Lookup email
            var codes = grouped.Select(g => g.vendorCode).ToList();
            var vendors = await _db.Vendors.AsNoTracking()
                .Where(v => codes.Contains(v.Code))
                .Select(v => new { v.Code, v.ContactEmail, v.ContactName, v.Status })
                .ToListAsync();

            var vDict = vendors.ToDictionary(v => v.Code);

            var items = grouped.Select(g =>
            {
                vDict.TryGetValue(g.vendorCode, out var v);
                return new
                {
                    g.vendorCode,
                    g.vendorName,
                    contactEmail   = v?.ContactEmail,
                    contactName    = v?.ContactName,
                    isActive       = v?.Status == VendorStatus.Active,
                    canSend        = v?.Status == VendorStatus.Active && !string.IsNullOrWhiteSpace(v?.ContactEmail),
                    g.inspectionCount,
                    g.inspections
                };
            }).ToList();

            return Ok(new
            {
                fromDate, toDate, lookAhead,
                totalInspections = inspections.Count,
                vendors = items
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET api/mail/settings
        // Trả về cấu hình hiện tại (ẩn password) - dùng cho UI hiển thị
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("settings")]
        [AllowAnonymous]
        public IActionResult GetSettings()
        {
            return Ok(new
            {
                smtpHost         = _config["MailSettings:SmtpHost"],
                smtpPort         = _config.GetValue<int>("MailSettings:SmtpPort", 587),
                senderEmail      = _config["MailSettings:SenderEmail"],
                senderName       = _config["MailSettings:SenderName"],
                useSsl           = _config.GetValue<bool>("MailSettings:UseSsl", true),
                lookAheadDays    = _config.GetValue<int>("MailSettings:LookAheadDays", 7),
                sendDayOfWeek    = _config.GetValue<int>("MailSettings:SendDayOfWeek", 5),
                sendHour         = _config.GetValue<int>("MailSettings:SendHour", 18),
                weeklyJobEnabled = _config.GetValue<bool>("MailSettings:WeeklyJobEnabled", true),
                hasPassword      = !string.IsNullOrEmpty(_config["MailSettings:Password"])
            });
        }
    }

    public class TestMailRequest
    {
        public string  ToEmail { get; set; } = "";
        public string? Subject { get; set; }
    }
}
