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
    [Route("api/mail-settings")]
    [Authorize]   // bắt buộc đăng nhập
    public class MailSettingsController : ControllerBase
    {
        private readonly AppDbContext         _db;
        private readonly IMailConfigProvider  _provider;
        private readonly IConfiguration       _config;
        private readonly ILogger<MailSettingsController> _logger;

        public MailSettingsController(
            AppDbContext db,
            IMailConfigProvider provider,
            IConfiguration config,
            ILogger<MailSettingsController> logger)
        {
            _db       = db;
            _provider = provider;
            _config   = config;
            _logger   = logger;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET api/mail-settings
        // Lấy cấu hình hiện tại (KHÔNG trả về Password)
        // ─────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var snap = await _provider.GetAsync(ct);

            return Ok(new MailSettingsDto
            {
                SmtpHost         = snap.SmtpHost,
                SmtpPort         = snap.SmtpPort,
                UseSsl           = snap.UseSsl,
                SenderEmail      = snap.SenderEmail,
                SenderName       = snap.SenderName,
                Username         = snap.Username,
                LookAheadDays    = snap.LookAheadDays,
                SendDayOfWeek    = snap.SendDayOfWeek,
                SendHour         = snap.SendHour,
                SendMinute       = snap.SendMinute,
                WeeklyJobEnabled = snap.WeeklyJobEnabled,
                UpdatedAt        = snap.UpdatedAt,
                UpdatedBy        = snap.UpdatedBy,
                FromDatabase     = snap.FromDatabase,
                HasPassword      = !string.IsNullOrEmpty(snap.Password)
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT api/mail-settings
        // Cập nhật cấu hình (ghi xuống DB, KHÔNG nhận password ở đây)
        // ─────────────────────────────────────────────────────────────────
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] MailSettingsDto dto, CancellationToken ct)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(dto.SmtpHost))    return BadRequest(new { error = "SmtpHost không được để trống" });
            if (string.IsNullOrWhiteSpace(dto.SenderEmail)) return BadRequest(new { error = "SenderEmail không được để trống" });
            if (string.IsNullOrWhiteSpace(dto.Username))    return BadRequest(new { error = "Username không được để trống" });
            if (dto.SmtpPort < 1 || dto.SmtpPort > 65535)   return BadRequest(new { error = "SmtpPort phải trong khoảng 1-65535" });
            if (dto.SendDayOfWeek < 0 || dto.SendDayOfWeek > 6) return BadRequest(new { error = "SendDayOfWeek phải trong khoảng 0-6" });
            if (dto.SendHour < 0 || dto.SendHour > 23)      return BadRequest(new { error = "SendHour phải trong khoảng 0-23" });
            if (dto.SendMinute < 0 || dto.SendMinute > 59)  return BadRequest(new { error = "SendMinute phải trong khoảng 0-59" });
            if (dto.LookAheadDays < 1 || dto.LookAheadDays > 60) return BadRequest(new { error = "LookAheadDays phải trong khoảng 1-60" });

            var current = await _db.MailConfigs.FirstOrDefaultAsync(ct);
            var isNew   = current == null;

            if (current == null)
            {
                current = new MailConfig { Id = 1 };
                _db.MailConfigs.Add(current);
            }

            current.SmtpHost         = dto.SmtpHost.Trim();
            current.SmtpPort         = dto.SmtpPort;
            current.UseSsl           = dto.UseSsl;
            current.SenderEmail      = dto.SenderEmail.Trim();
            current.SenderName       = (dto.SenderName ?? "").Trim();
            current.Username         = dto.Username.Trim();
            current.LookAheadDays    = dto.LookAheadDays;
            current.SendDayOfWeek    = dto.SendDayOfWeek;
            current.SendHour         = dto.SendHour;
            current.SendMinute       = dto.SendMinute;
            current.WeeklyJobEnabled = dto.WeeklyJobEnabled;
            current.UpdatedAt        = DateTime.UtcNow;
            current.UpdatedBy        = User.Identity?.Name ?? "unknown";

            await _db.SaveChangesAsync(ct);

            // Invalidate cache để các service khác lấy ngay giá trị mới
            _provider.InvalidateCache();

            _logger.LogInformation("MailConfig đã được cập nhật bởi {user}", current.UpdatedBy);

            return Ok(new { success = true, isNew, updatedAt = current.UpdatedAt });
        }

        // ─────────────────────────────────────────────────────────────────
        // POST api/mail-settings/test-smtp
        // Test SMTP với cấu hình HIỆN TẠI bằng cách gửi 1 mail thử
        // (Không cần lưu trước - dùng config đang có trong provider)
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("test-smtp")]
        public async Task<IActionResult> TestSmtp([FromBody] TestSmtpRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.ToEmail))
                return BadRequest(new { error = "ToEmail không được để trống" });

            var snap = await _provider.GetAsync(ct);

            if (string.IsNullOrEmpty(snap.Password))
                return BadRequest(new { error = "Password chưa được cấu hình trong Environment Variable Azure (MailSettings__Password)" });

            try
            {
                using var smtp = new SmtpClient(snap.SmtpHost, snap.SmtpPort)
                {
                    EnableSsl       = snap.UseSsl,
                    Credentials     = new NetworkCredential(snap.Username, snap.Password),
                    DeliveryMethod  = SmtpDeliveryMethod.Network,
                    Timeout         = 30000
                };

                using var msg = new MailMessage
                {
                    From            = new MailAddress(snap.SenderEmail, snap.SenderName),
                    Subject         = "[Inticoo] Test SMTP Configuration",
                    Body            = $@"<html><body style='font-family:Segoe UI,Arial;'>
<div style='max-width:600px;margin:0 auto;padding:24px;background:#f8f9fa;border-radius:8px;'>
  <h2 style='color:#1e6091;'>✅ SMTP Test Success</h2>
  <p>Cấu hình SMTP đang hoạt động bình thường.</p>
  <hr/>
  <table style='font-size:13px;color:#555;'>
    <tr><td style='padding:4px 12px 4px 0;'><strong>SMTP Host:</strong></td><td>{snap.SmtpHost}:{snap.SmtpPort}</td></tr>
    <tr><td style='padding:4px 12px 4px 0;'><strong>Sender:</strong></td><td>{WebUtility.HtmlEncode(snap.SenderName)} &lt;{snap.SenderEmail}&gt;</td></tr>
    <tr><td style='padding:4px 12px 4px 0;'><strong>Config source:</strong></td><td>{(snap.FromDatabase ? "Database" : "Environment Variable")}</td></tr>
    <tr><td style='padding:4px 12px 4px 0;'><strong>Sent at:</strong></td><td>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</td></tr>
  </table>
</div></body></html>",
                    IsBodyHtml      = true,
                    BodyEncoding    = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                msg.To.Add(new MailAddress(req.ToEmail));

                await smtp.SendMailAsync(msg, ct);

                return Ok(new { success = true, message = $"Đã gửi mail test tới {req.ToEmail}" });
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "Test SMTP failed");
                return StatusCode(500, new { success = false, error = ex.Message, statusCode = ex.StatusCode.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test SMTP failed");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST api/mail-settings/reset-cache
        // Force refresh cache (debug/admin)
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("reset-cache")]
        public IActionResult ResetCache()
        {
            _provider.InvalidateCache();
            return Ok(new { success = true, message = "Cache đã được reset" });
        }

        // ─────────────────────────────────────────────────────────────────
        // POST api/mail-settings/reset-to-env
        // Xóa record DB → app sẽ fallback hoàn toàn về Environment Variables
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("reset-to-env")]
        public async Task<IActionResult> ResetToEnvVars(CancellationToken ct)
        {
            var current = await _db.MailConfigs.FirstOrDefaultAsync(ct);
            if (current != null)
            {
                _db.MailConfigs.Remove(current);
                await _db.SaveChangesAsync(ct);
            }
            _provider.InvalidateCache();

            return Ok(new { success = true, message = "Đã xóa cấu hình DB. App sẽ dùng Environment Variables." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────────────────────────────
    public class MailSettingsDto
    {
        public string SmtpHost    { get; set; } = "";
        public int    SmtpPort    { get; set; } = 587;
        public bool   UseSsl      { get; set; } = true;
        public string SenderEmail { get; set; } = "";
        public string SenderName  { get; set; } = "";
        public string Username    { get; set; } = "";

        public int  LookAheadDays    { get; set; } = 7;
        public int  SendDayOfWeek    { get; set; } = 5;
        public int  SendHour         { get; set; } = 18;
        public int  SendMinute       { get; set; } = 0;
        public bool WeeklyJobEnabled { get; set; } = true;

        // Read-only fields (chỉ trả về từ GET)
        public DateTime? UpdatedAt    { get; set; }
        public string?   UpdatedBy    { get; set; }
        public bool      FromDatabase { get; set; }
        public bool      HasPassword  { get; set; }
    }

    public class TestSmtpRequest
    {
        public string ToEmail { get; set; } = "";
    }
}
