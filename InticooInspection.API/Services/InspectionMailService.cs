using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace InticooInspection.API.Services
{
    public interface IInspectionMailService
    {
        /// <summary>
        /// Gửi mail thông báo inspection trong 7 ngày tới cho TẤT CẢ vendor active.
        /// Mỗi vendor chỉ nhận 1 mail tổng hợp các inspection của riêng họ.
        /// </summary>
        Task<MailRunResult> SendWeeklyVendorMailsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gửi mail cho 1 vendor cụ thể (dùng để test hoặc gửi tay).
        /// `vendorCode` chính là Inspection.VendorId (Vendor.Code).
        /// </summary>
        Task<(bool ok, string? error, int inspectionCount)> SendForVendorAsync(
            string vendorCode, CancellationToken ct = default);
    }

    public class MailRunResult
    {
        public int VendorsTotal      { get; set; }
        public int VendorsSent       { get; set; }
        public int VendorsSkipped    { get; set; } // không có inspection sắp tới
        public int VendorsFailed     { get; set; }
        public int InspectionsTotal  { get; set; }
        public List<string> Errors   { get; set; } = new();
    }

    public class InspectionMailService : IInspectionMailService
    {
        private readonly AppDbContext   _db;
        private readonly IConfiguration _config;
        private readonly ILogger<InspectionMailService> _logger;

        public InspectionMailService(
            AppDbContext db,
            IConfiguration config,
            ILogger<InspectionMailService> logger)
        {
            _db     = db;
            _config = config;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────────────

        public async Task<MailRunResult> SendWeeklyVendorMailsAsync(CancellationToken ct = default)
        {
            var result = new MailRunResult();
            var lookAheadDays = _config.GetValue<int>("MailSettings:LookAheadDays", 7);

            // Khoảng thời gian: từ hôm nay đến 7 ngày tới
            var fromDate = DateTime.Today;
            var toDate   = fromDate.AddDays(lookAheadDays);

            // ── Lấy tất cả inspection sắp tới (status = Pending hoặc InProgress) ──
            var upcoming = await _db.Inspections
                .AsNoTracking()
                .Where(i => i.InspectionDate >= fromDate
                         && i.InspectionDate <  toDate
                         && (i.Status == InspectionStatus.Pending
                          || i.Status == InspectionStatus.InProgress))
                .OrderBy(i => i.InspectionDate)
                .ToListAsync(ct);

            result.InspectionsTotal = upcoming.Count;

            if (upcoming.Count == 0)
            {
                _logger.LogInformation("Không có inspection nào trong {days} ngày tới", lookAheadDays);
                return result;
            }

            // ── Group theo VendorId (kiểu string = Vendor.Code) ──
            var byVendor = upcoming
                .Where(i => !string.IsNullOrEmpty(i.VendorId))
                .GroupBy(i => i.VendorId!)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // ── Lấy vendor info cho các vendor có inspection ──
            // Inspection.VendorId chính là Vendor.Code → lookup theo Code
            var vendorCodes = byVendor.Keys.ToList();
            var vendors = await _db.Vendors
                .AsNoTracking()
                .Where(v => vendorCodes.Contains(v.Code) && v.Status == VendorStatus.Active)
                .ToListAsync(ct);

            result.VendorsTotal = vendors.Count;

            foreach (var vendor in vendors)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(vendor.ContactEmail))
                {
                    result.VendorsSkipped++;
                    _logger.LogWarning("Vendor {code} - {name} không có ContactEmail",
                        vendor.Code, vendor.Name);
                    continue;
                }

                if (!byVendor.TryGetValue(vendor.Code, out var vendorInspections) || vendorInspections.Count == 0)
                {
                    result.VendorsSkipped++;
                    continue;
                }

                var subject = $"[Intico] Lịch Inspection tuần này - {vendor.Name}";
                var html    = BuildEmailHtml(vendor, vendorInspections);

                var (ok, error) = await SendMailAsync(
                    vendor.ContactEmail,
                    vendor.ContactName ?? vendor.Name,
                    subject, html, ct);

                // TODO: Lưu MailLog khi entity MailLog đã được tạo + add DbSet vào AppDbContext + chạy migration
                // _db.MailLogs.Add(new MailLog
                // {
                //     VendorId         = vendor.Id,
                //     VendorCode       = vendor.Code,
                //     ToEmail          = vendor.ContactEmail,
                //     Subject          = subject,
                //     SentAt           = DateTime.UtcNow,
                //     IsSuccess        = ok,
                //     ErrorMessage     = error,
                //     InspectionCount  = vendorInspections.Count
                // });

                if (ok)
                {
                    result.VendorsSent++;
                    _logger.LogInformation("Đã gửi tới {email} ({count} inspection)",
                        vendor.ContactEmail, vendorInspections.Count);
                }
                else
                {
                    result.VendorsFailed++;
                    result.Errors.Add($"{vendor.Code}: {error}");
                }

                // Delay tránh SMTP rate-limit
                await Task.Delay(500, ct);
            }

            // await _db.SaveChangesAsync(ct);  // bật lại khi đã có MailLog
            return result;
        }

        public async Task<(bool ok, string? error, int inspectionCount)> SendForVendorAsync(
            string vendorCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vendorCode))
                return (false, "Vendor code không hợp lệ", 0);

            var vendor = await _db.Vendors.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Code == vendorCode, ct);

            if (vendor == null)
                return (false, "Không tìm thấy vendor", 0);

            if (string.IsNullOrWhiteSpace(vendor.ContactEmail))
                return (false, "Vendor chưa có ContactEmail", 0);

            var lookAheadDays = _config.GetValue<int>("MailSettings:LookAheadDays", 7);
            var fromDate = DateTime.Today;
            var toDate   = fromDate.AddDays(lookAheadDays);

            var inspections = await _db.Inspections.AsNoTracking()
                .Where(i => i.VendorId == vendorCode
                         && i.InspectionDate >= fromDate
                         && i.InspectionDate <  toDate
                         && (i.Status == InspectionStatus.Pending
                          || i.Status == InspectionStatus.InProgress))
                .OrderBy(i => i.InspectionDate)
                .ToListAsync(ct);

            if (inspections.Count == 0)
                return (false, "Vendor không có inspection nào trong khoảng thời gian này", 0);

            var subject = $"[Intico] Lịch Inspection tuần này - {vendor.Name}";
            var html    = BuildEmailHtml(vendor, inspections);

            var (ok, error) = await SendMailAsync(
                vendor.ContactEmail,
                vendor.ContactName ?? vendor.Name,
                subject, html, ct);

            // TODO: Lưu MailLog khi entity MailLog đã được tạo + add DbSet vào AppDbContext + chạy migration
            // _db.MailLogs.Add(new MailLog
            // {
            //     VendorId        = vendor.Id,
            //     VendorCode      = vendor.Code,
            //     ToEmail         = vendor.ContactEmail,
            //     Subject         = subject,
            //     SentAt          = DateTime.UtcNow,
            //     IsSuccess       = ok,
            //     ErrorMessage    = error,
            //     InspectionCount = inspections.Count
            // });
            // await _db.SaveChangesAsync(ct);

            return (ok, error, inspections.Count);
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS - SMTP gửi mail (dùng System.Net.Mail giống InspectionController)
        // ─────────────────────────────────────────────────────────────

        private async Task<(bool ok, string? error)> SendMailAsync(
            string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
        {
            try
            {
                var host         = _config["MailSettings:SmtpHost"] ?? "";
                var port         = _config.GetValue<int>("MailSettings:SmtpPort", 587);
                var senderEmail  = _config["MailSettings:SenderEmail"] ?? "";
                var senderName   = _config["MailSettings:SenderName"] ?? "Intico Inspection";
                var username     = _config["MailSettings:Username"] ?? "";
                var password     = _config["MailSettings:Password"] ?? "";
                var useSsl       = _config.GetValue<bool>("MailSettings:UseSsl", true);

                using var smtp = new SmtpClient(host, port)
                {
                    EnableSsl   = useSsl,
                    Credentials = new NetworkCredential(username, password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000
                };

                using var msg = new MailMessage
                {
                    From       = new MailAddress(senderEmail, senderName),
                    Subject    = subject,
                    Body       = htmlBody,
                    IsBodyHtml = true,
                    BodyEncoding    = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                msg.To.Add(new MailAddress(toEmail, toName));

                await smtp.SendMailAsync(msg, ct);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi mail tới {email}", toEmail);
                return (false, ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS - Build HTML email
        // ─────────────────────────────────────────────────────────────

        private static string BuildEmailHtml(Vendor vendor, List<Inspection> inspections)
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>Inspection Schedule</title></head>
<body style='font-family:Segoe UI,Arial,sans-serif;color:#333;background:#f5f7fb;padding:20px;'>
  <div style='max-width:760px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);'>
    <div style='background:linear-gradient(135deg,#1e6091 0%,#2a9d8f 100%);color:#fff;padding:24px;'>
      <h2 style='margin:0;font-size:22px;'>📋 Inspection Schedule - Weekly Notice</h2>
      <p style='margin:6px 0 0;opacity:.9;font-size:14px;'>Intico Inspection System</p>
    </div>
    <div style='padding:24px;'>");

            sb.Append($@"
      <p>Dear <strong>{Esc(vendor.ContactName ?? vendor.Name)}</strong>,</p>
      <p>This is a friendly reminder of the upcoming inspections scheduled for <strong>{Esc(vendor.Name)}</strong> ({Esc(vendor.Code)}) in the next 7 days.</p>
      <p>Please prepare the following items and ensure your team is available on the scheduled dates:</p>");

            sb.Append(@"
      <table style='width:100%;border-collapse:collapse;margin-top:16px;font-size:14px;'>
        <thead>
          <tr style='background:#1e6091;color:#fff;'>
            <th style='padding:10px;text-align:left;border:1px solid #1e6091;'>Date</th>
            <th style='padding:10px;text-align:left;border:1px solid #1e6091;'>Job No.</th>
            <th style='padding:10px;text-align:left;border:1px solid #1e6091;'>Type</th>
            <th style='padding:10px;text-align:left;border:1px solid #1e6091;'>Product</th>
            <th style='padding:10px;text-align:left;border:1px solid #1e6091;'>Customer</th>
            <th style='padding:10px;text-align:left;border:1px solid #1e6091;'>PO Number</th>
          </tr>
        </thead>
        <tbody>");

            foreach (var i in inspections)
            {
                var typeLabel = i.InspectionType switch
                {
                    InspectionType.PPT => "PPT (Pre-Production)",
                    InspectionType.DPI => "DPI (During Production)",
                    InspectionType.PST => "PST (Pre-Shipment)",
                    _ => i.InspectionType.ToString()
                };

                sb.Append($@"
          <tr>
            <td style='padding:10px;border:1px solid #ddd;'><strong>{i.InspectionDate:dd/MM/yyyy}</strong> ({i.InspectionDate:dddd})</td>
            <td style='padding:10px;border:1px solid #ddd;'>{Esc(i.JobNumber)}</td>
            <td style='padding:10px;border:1px solid #ddd;'>{Esc(typeLabel)}</td>
            <td style='padding:10px;border:1px solid #ddd;'>{Esc(i.ProductName)}</td>
            <td style='padding:10px;border:1px solid #ddd;'>{Esc(i.CustomerName)}</td>
            <td style='padding:10px;border:1px solid #ddd;'>{Esc(i.PoNumber)}</td>
          </tr>");
            }

            sb.Append(@"
        </tbody>
      </table>");

            sb.Append(@"
      <div style='margin-top:24px;padding:16px;background:#fff8e1;border-left:4px solid #f9a825;border-radius:4px;'>
        <strong>⚠️ Important reminders:</strong>
        <ul style='margin:8px 0 0 18px;padding:0;'>
          <li>Ensure goods are 100% finished and packed before the inspection date.</li>
          <li>Have all necessary documents (PO, BOM, samples, golden samples) ready on site.</li>
          <li>Contact your inspector or the Intico team immediately if there is any change of schedule.</li>
        </ul>
      </div>
      <p style='margin-top:24px;'>If you have any questions, please reply to this email or contact the Intico Inspection team.</p>
      <p>Best regards,<br/><strong>Intico Inspection Team</strong></p>
    </div>
    <div style='padding:14px 24px;background:#f5f7fb;color:#888;font-size:12px;text-align:center;border-top:1px solid #eee;'>
      This is an automated email from Intico Inspection System. Please do not reply directly to this address if not necessary.
    </div>
  </div>
</body></html>");

            return sb.ToString();
        }

        private static string Esc(string? s)
            => string.IsNullOrEmpty(s) ? "" : System.Net.WebUtility.HtmlEncode(s);
    }
}
