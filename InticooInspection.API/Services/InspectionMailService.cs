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
        Task<MailRunResult> SendWeeklyVendorMailsAsync(CancellationToken ct = default);
        Task<(bool ok, string? error, int inspectionCount)> SendForVendorAsync(string vendorCode, CancellationToken ct = default);
    }

    public class MailRunResult
    {
        public int VendorsTotal      { get; set; }
        public int VendorsSent       { get; set; }
        public int VendorsSkipped    { get; set; }
        public int VendorsFailed     { get; set; }
        public int InspectionsTotal  { get; set; }
        public List<string> Errors   { get; set; } = new();
    }

    public class InspectionMailService : IInspectionMailService
    {
        private readonly AppDbContext _db;
        private readonly IMailConfigProvider _configProvider;
        private readonly ILogger<InspectionMailService> _logger;

        public InspectionMailService(
            AppDbContext db,
            IMailConfigProvider configProvider,
            ILogger<InspectionMailService> logger)
        {
            _db             = db;
            _configProvider = configProvider;
            _logger         = logger;
        }

        // ─────────────────────────────────────────────────────────────
        public async Task<MailRunResult> SendWeeklyVendorMailsAsync(CancellationToken ct = default)
        {
            var result = new MailRunResult();
            var cfg    = await _configProvider.GetAsync(ct);

            var fromDate = DateTime.Today;
            var toDate   = fromDate.AddDays(cfg.LookAheadDays);

            // Lấy inspection sắp tới (Pending hoặc InProgress)
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
                _logger.LogInformation("Không có inspection nào trong {days} ngày tới", cfg.LookAheadDays);
                return result;
            }

            // Group theo VendorId (trên Inspection là string code)
            var byVendor = upcoming
                .Where(i => !string.IsNullOrEmpty(i.VendorId))
                .GroupBy(i => i.VendorId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Lấy vendor info active
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

                var subject = $"[Inticoo] Lịch Inspection tuần này - {vendor.Name}";
                var html    = BuildEmailHtml(vendor, vendorInspections);

                var (ok, error) = await SendMailAsync(cfg,
                    vendor.ContactEmail,
                    vendor.ContactName ?? vendor.Name,
                    subject, html, ct);

                _db.MailLogs.Add(new MailLog
                {
                    VendorCode      = vendor.Code,
                    ToEmail         = vendor.ContactEmail,
                    Subject         = subject,
                    SentAt          = DateTime.UtcNow,
                    IsSuccess       = ok,
                    ErrorMessage    = error,
                    InspectionCount = vendorInspections.Count
                });

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

                await Task.Delay(500, ct);  // tránh SMTP rate-limit
            }

            await _db.SaveChangesAsync(ct);
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        public async Task<(bool ok, string? error, int inspectionCount)> SendForVendorAsync(
            string vendorCode, CancellationToken ct = default)
        {
            var cfg = await _configProvider.GetAsync(ct);

            var vendor = await _db.Vendors.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Code == vendorCode, ct);

            if (vendor == null)             return (false, "Không tìm thấy vendor", 0);
            if (string.IsNullOrWhiteSpace(vendor.ContactEmail))
                return (false, "Vendor chưa có ContactEmail", 0);

            var fromDate = DateTime.Today;
            var toDate   = fromDate.AddDays(cfg.LookAheadDays);

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

            var subject = $"[Inticoo] Lịch Inspection tuần này - {vendor.Name}";
            var html    = BuildEmailHtml(vendor, inspections);

            var (ok, error) = await SendMailAsync(cfg,
                vendor.ContactEmail,
                vendor.ContactName ?? vendor.Name,
                subject, html, ct);

            _db.MailLogs.Add(new MailLog
            {
                VendorCode      = vendor.Code,
                ToEmail         = vendor.ContactEmail,
                Subject         = subject,
                SentAt          = DateTime.UtcNow,
                IsSuccess       = ok,
                ErrorMessage    = error,
                InspectionCount = inspections.Count
            });
            await _db.SaveChangesAsync(ct);

            return (ok, error, inspections.Count);
        }

        // ─────────────────────────────────────────────────────────────
        private async Task<(bool ok, string? error)> SendMailAsync(
            MailConfigSnapshot cfg,
            string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
        {
            try
            {
                using var smtp = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort)
                {
                    EnableSsl      = cfg.UseSsl,
                    Credentials    = new NetworkCredential(cfg.Username, cfg.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout        = 30000
                };

                using var msg = new MailMessage
                {
                    From            = new MailAddress(cfg.SenderEmail, cfg.SenderName),
                    Subject         = subject,
                    Body            = htmlBody,
                    IsBodyHtml      = true,
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
        private static string BuildEmailHtml(Vendor vendor, List<Inspection> inspections)
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>Inspection Schedule</title></head>
<body style='font-family:Segoe UI,Arial,sans-serif;color:#333;background:#f5f7fb;padding:20px;'>
  <div style='max-width:760px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);'>
    <div style='background:linear-gradient(135deg,#1e6091 0%,#2a9d8f 100%);color:#fff;padding:24px;'>
      <h2 style='margin:0;font-size:22px;'>📋 Inspection Schedule - Weekly Notice</h2>
      <p style='margin:6px 0 0;opacity:.9;font-size:14px;'>Inticoo Inspection System</p>
    </div>
    <div style='padding:24px;'>");

            sb.Append($@"
      <p>Dear <strong>{Esc(vendor.ContactName ?? vendor.Name)}</strong>,</p>
      <p>This is a friendly reminder of the upcoming inspections scheduled for <strong>{Esc(vendor.Name)}</strong> ({Esc(vendor.Code)}) in the next few days.</p>
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
          <li>Contact your inspector or the Inticoo team immediately if there is any change of schedule.</li>
        </ul>
      </div>
      <p style='margin-top:24px;'>If you have any questions, please reply to this email or contact the Inticoo Inspection team.</p>
      <p>Best regards,<br/><strong>Inticoo Inspection Team</strong></p>
    </div>
    <div style='padding:14px 24px;background:#f5f7fb;color:#888;font-size:12px;text-align:center;border-top:1px solid #eee;'>
      This is an automated email from Inticoo Inspection System.
    </div>
  </div>
</body></html>");

            return sb.ToString();
        }

        private static string Esc(string? s)
            => string.IsNullOrEmpty(s) ? "" : WebUtility.HtmlEncode(s);
    }
}
