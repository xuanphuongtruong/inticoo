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

        // ═════════════════════════════════════════════════════════════════════
        //  VENDOR ENDPOINTS
        // ═════════════════════════════════════════════════════════════════════

        // POST api/mail/run-weekly
        // Gửi mail HÀNG LOẠT cho tất cả vendor có inspection sắp tới.
        [HttpPost("run-weekly")]
        [AllowAnonymous]
        public async Task<IActionResult> RunWeekly(CancellationToken ct)
        {
            var result = await _mail.SendWeeklyVendorMailsAsync(ct);
            return Ok(result);
        }

        // POST api/mail/send-vendor/{vendorCode}
        [HttpPost("send-vendor/{vendorCode}")]
        [AllowAnonymous]
        public async Task<IActionResult> SendVendor(string vendorCode, CancellationToken ct)
        {
            var (ok, error, count) = await _mail.SendForVendorAsync(vendorCode, ct);
            if (!ok) return BadRequest(new { success = false, error, inspectionCount = count });
            return Ok(new { success = true, inspectionCount = count });
        }

        // GET api/mail/upcoming-vendors
        [HttpGet("upcoming-vendors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUpcomingVendors()
        {
            var lookAhead = _config.GetValue<int>("MailSettings:LookAheadDays", 14);
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
                    vendorCode      = g.Key,
                    vendorName      = g.First().VendorName,
                    inspectionCount = g.Count(),
                    inspections     = g.OrderBy(x => x.InspectionDate).Select(x => new
                    {
                        x.JobNumber, x.InspectionDate, x.ProductName
                    })
                })
                .ToList();

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
                    contactEmail = v?.ContactEmail,
                    contactName  = v?.ContactName,
                    isActive     = v?.Status == VendorStatus.Active,
                    canSend      = v?.Status == VendorStatus.Active
                                && !string.IsNullOrWhiteSpace(v?.ContactEmail),
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

        // ═════════════════════════════════════════════════════════════════════
        //  CUSTOMER ENDPOINTS (NEW)
        // ═════════════════════════════════════════════════════════════════════

        // POST api/mail/run-weekly-customers
        [HttpPost("run-weekly-customers")]
        [AllowAnonymous]
        public async Task<IActionResult> RunWeeklyCustomers(CancellationToken ct)
        {
            var result = await _mail.SendWeeklyCustomerMailsAsync(ct);
            return Ok(result);
        }

        // POST api/mail/send-customer/{customerId}
        [HttpPost("send-customer/{customerId}")]
        [AllowAnonymous]
        public async Task<IActionResult> SendCustomer(string customerId, CancellationToken ct)
        {
            var (ok, error, count) = await _mail.SendForCustomerAsync(customerId, ct);
            if (!ok) return BadRequest(new { success = false, error, inspectionCount = count });
            return Ok(new { success = true, inspectionCount = count });
        }

        // GET api/mail/upcoming-customers
        [HttpGet("upcoming-customers")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUpcomingCustomers()
        {
            var lookAhead = _config.GetValue<int>("MailSettings:LookAheadDays", 14);
            var fromDate  = DateTime.Today;
            var toDate    = fromDate.AddDays(lookAhead);

            var inspections = await _db.Inspections.AsNoTracking()
                .Where(i => i.InspectionDate >= fromDate
                         && i.InspectionDate <  toDate
                         && (i.Status == InspectionStatus.Pending
                          || i.Status == InspectionStatus.InProgress))
                .Select(i => new { i.CustomerId, i.CustomerName, i.JobNumber, i.InspectionDate, i.ProductName })
                .ToListAsync();

            var grouped = inspections
                .Where(i => !string.IsNullOrEmpty(i.CustomerId))
                .GroupBy(i => i.CustomerId!)
                .Select(g => new
                {
                    customerId      = g.Key,
                    customerName    = g.First().CustomerName,
                    inspectionCount = g.Count(),
                    inspections     = g.OrderBy(x => x.InspectionDate).Select(x => new
                    {
                        x.JobNumber, x.InspectionDate, x.ProductName
                    })
                })
                .ToList();

            var ids = grouped.Select(g => g.customerId).ToList();
            var customers = await _db.Customers.AsNoTracking()
                .Where(c => ids.Contains(c.CustomerId))
                .Select(c => new
                {
                    c.CustomerId, c.CompanyName, c.ContactPerson,
                    c.Email, c.AlternateReportEmail, c.ReportEmailType,
                    c.ReceiveInspectionReport, c.IsActive
                })
                .ToListAsync();

            var cDict = customers.ToDictionary(c => c.CustomerId);

            var items = grouped.Select(g =>
            {
                cDict.TryGetValue(g.customerId, out var c);

                // Tính email theo logic ResolveCustomerRecipients
                var recipients = new List<string>();
                if (c != null)
                {
                    var type = (c.ReportEmailType ?? "Registered").Trim();
                    if (string.Equals(type, "Alternate", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(c.AlternateReportEmail))
                            recipients.Add(c.AlternateReportEmail.Trim());
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(c.Email))
                            recipients.Add(c.Email.Trim());
                        if (!string.IsNullOrWhiteSpace(c.AlternateReportEmail))
                            recipients.Add(c.AlternateReportEmail.Trim());
                    }
                    recipients = recipients
                        .Where(e => e.Contains('@'))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                return new
                {
                    g.customerId,
                    g.customerName,
                    contactPerson           = c?.ContactPerson,
                    email                   = c?.Email,
                    alternateReportEmail    = c?.AlternateReportEmail,
                    reportEmailType         = c?.ReportEmailType ?? "Registered",
                    recipients              = recipients,
                    receiveInspectionReport = c?.ReceiveInspectionReport ?? false,
                    isActive                = c?.IsActive ?? false,
                    canSend                 = (c?.IsActive ?? false)
                                           && (c?.ReceiveInspectionReport ?? false)
                                           && recipients.Count > 0,
                    g.inspectionCount,
                    g.inspections
                };
            }).ToList();

            return Ok(new
            {
                fromDate, toDate, lookAhead,
                totalInspections = inspections.Count,
                customers = items
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TEST MAIL ENDPOINTS
        // ═════════════════════════════════════════════════════════════════════

        // POST api/mail/send-test
        // Gửi 1 mail test "trống" - chỉ kiểm tra cấu hình SMTP
        [HttpPost("send-test")]
        [AllowAnonymous]
        public async Task<IActionResult> SendTest([FromBody] TestMailRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.ToEmail))
                return BadRequest(new { success = false, error = "Email không được để trống" });

            var subject = req.Subject ?? "[Inticoo] Email Test - Kiểm tra cấu hình SMTP";
            var html    = $@"<html><body style='font-family:Segoe UI,Arial;color:#333;'>
<div style='max-width:600px;margin:0 auto;padding:24px;background:#f8f9fa;border-radius:8px;'>
  <h2 style='color:#1e6091;'>✅ Email Test thành công!</h2>
  <p>Đây là email test từ <strong>Inticoo Inspection System</strong>.</p>
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
                var senderName  = _config["MailSettings:SenderName"] ?? "Inticoo Inspection";
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

        // POST api/mail/send-test-vendor
        // Gửi mail test với template VENDOR thực tế (data mẫu) tới 1 email
        [HttpPost("send-test-vendor")]
        [AllowAnonymous]
        public async Task<IActionResult> SendTestVendor([FromBody] TestMailRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.ToEmail))
                return BadRequest(new { success = false, error = "Email không được để trống" });

            var (ok, error) = await _mail.SendTestVendorMailAsync(req.ToEmail, ct);
            if (!ok) return StatusCode(500, new { success = false, error });
            return Ok(new { success = true, message = $"Đã gửi mail test (template Vendor) tới {req.ToEmail}" });
        }

        // POST api/mail/send-test-customer
        // Gửi mail test với template CUSTOMER thực tế (data mẫu) tới 1 email
        [HttpPost("send-test-customer")]
        [AllowAnonymous]
        public async Task<IActionResult> SendTestCustomer([FromBody] TestMailRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.ToEmail))
                return BadRequest(new { success = false, error = "Email không được để trống" });

            var (ok, error) = await _mail.SendTestCustomerMailAsync(req.ToEmail, ct);
            if (!ok) return StatusCode(500, new { success = false, error });
            return Ok(new { success = true, message = $"Đã gửi mail test (template Customer) tới {req.ToEmail}" });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  LOGS / STATS / SETTINGS
        // ═════════════════════════════════════════════════════════════════════

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
                lookAheadDays    = _config.GetValue<int>("MailSettings:LookAheadDays", 14),
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
