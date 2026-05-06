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
        // ── Vendor (cũ) ───────────────────────────────────────────────
        Task<MailRunResult> SendWeeklyVendorMailsAsync(CancellationToken ct = default);
        Task<(bool ok, string? error, int inspectionCount)> SendForVendorAsync(string vendorCode, CancellationToken ct = default);

        // ── Customer (mới) ────────────────────────────────────────────
        Task<MailRunResult> SendWeeklyCustomerMailsAsync(CancellationToken ct = default);
        Task<(bool ok, string? error, int inspectionCount)> SendForCustomerAsync(string customerId, CancellationToken ct = default);

        // ── Test mail (preview template thực tế) ──────────────────────
        Task<(bool ok, string? error)> SendTestVendorMailAsync(string toEmail, CancellationToken ct = default);
        Task<(bool ok, string? error)> SendTestCustomerMailAsync(string toEmail, CancellationToken ct = default);
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

        // Subject của email (đã đưa từ body lên)
        private const string WEEKLY_SUBJECT = "Summary Schedule : 14 Days Inspection";

        public InspectionMailService(
            AppDbContext db,
            IMailConfigProvider configProvider,
            ILogger<InspectionMailService> logger)
        {
            _db             = db;
            _configProvider = configProvider;
            _logger         = logger;
        }

        // ═════════════════════════════════════════════════════════════
        //  VENDOR FLOW
        // ═════════════════════════════════════════════════════════════
        public async Task<MailRunResult> SendWeeklyVendorMailsAsync(CancellationToken ct = default)
        {
            var result = new MailRunResult();
            var cfg    = await _configProvider.GetAsync(ct);

            var fromDate = DateTime.Today;
            var toDate   = fromDate.AddDays(cfg.LookAheadDays);

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
                _logger.LogInformation("Không có inspection nào trong {days} ngày tới (vendor)", cfg.LookAheadDays);
                return result;
            }

            var byVendor = upcoming
                .Where(i => !string.IsNullOrEmpty(i.VendorId))
                .GroupBy(i => i.VendorId!)
                .ToDictionary(g => g.Key, g => g.ToList());

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

                var html = BuildVendorEmailHtml(vendor, vendorInspections);

                var (ok, error) = await SendMailAsync(cfg,
                    new[] { vendor.ContactEmail },
                    vendor.ContactName ?? vendor.Name,
                    WEEKLY_SUBJECT, html, ct);

                _db.MailLogs.Add(new MailLog
                {
                    VendorCode      = vendor.Code,
                    ToEmail         = vendor.ContactEmail,
                    Subject         = WEEKLY_SUBJECT,
                    SentAt          = DateTime.UtcNow,
                    IsSuccess       = ok,
                    ErrorMessage    = error,
                    InspectionCount = vendorInspections.Count
                });

                if (ok)
                {
                    result.VendorsSent++;
                    _logger.LogInformation("Đã gửi Vendor mail tới {email} ({count} inspection)",
                        vendor.ContactEmail, vendorInspections.Count);
                }
                else
                {
                    result.VendorsFailed++;
                    result.Errors.Add($"{vendor.Code}: {error}");
                }

                await Task.Delay(500, ct);
            }

            await _db.SaveChangesAsync(ct);
            return result;
        }

        public async Task<(bool ok, string? error, int inspectionCount)> SendForVendorAsync(
            string vendorCode, CancellationToken ct = default)
        {
            var cfg = await _configProvider.GetAsync(ct);

            var vendor = await _db.Vendors.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Code == vendorCode, ct);

            if (vendor == null) return (false, "Không tìm thấy vendor", 0);
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

            var html = BuildVendorEmailHtml(vendor, inspections);

            var (ok, error) = await SendMailAsync(cfg,
                new[] { vendor.ContactEmail },
                vendor.ContactName ?? vendor.Name,
                WEEKLY_SUBJECT, html, ct);

            _db.MailLogs.Add(new MailLog
            {
                VendorCode      = vendor.Code,
                ToEmail         = vendor.ContactEmail,
                Subject         = WEEKLY_SUBJECT,
                SentAt          = DateTime.UtcNow,
                IsSuccess       = ok,
                ErrorMessage    = error,
                InspectionCount = inspections.Count
            });
            await _db.SaveChangesAsync(ct);

            return (ok, error, inspections.Count);
        }

        // ═════════════════════════════════════════════════════════════
        //  CUSTOMER FLOW
        // ═════════════════════════════════════════════════════════════
        public async Task<MailRunResult> SendWeeklyCustomerMailsAsync(CancellationToken ct = default)
        {
            var result = new MailRunResult();
            var cfg    = await _configProvider.GetAsync(ct);

            var fromDate = DateTime.Today;
            var toDate   = fromDate.AddDays(cfg.LookAheadDays);

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
                _logger.LogInformation("Không có inspection nào trong {days} ngày tới (customer)", cfg.LookAheadDays);
                return result;
            }

            // Group theo CustomerId
            var byCustomer = upcoming
                .Where(i => !string.IsNullOrEmpty(i.CustomerId))
                .GroupBy(i => i.CustomerId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            var customerIds = byCustomer.Keys.ToList();

            // Lấy customer active + có nhận inspection report
            var customers = await _db.Customers
                .AsNoTracking()
                .Where(c => customerIds.Contains(c.CustomerId)
                         && c.IsActive
                         && c.ReceiveInspectionReport)
                .ToListAsync(ct);

            // Field VendorsTotal/VendorsSent... được tái sử dụng cho customer counts
            result.VendorsTotal = customers.Count;

            foreach (var customer in customers)
            {
                ct.ThrowIfCancellationRequested();

                var recipients = ResolveCustomerRecipients(customer);
                if (recipients.Count == 0)
                {
                    result.VendorsSkipped++;
                    _logger.LogWarning("Customer {id} - {name} không có email hợp lệ (Type={type})",
                        customer.CustomerId, customer.CompanyName, customer.ReportEmailType);
                    continue;
                }

                if (!byCustomer.TryGetValue(customer.CustomerId, out var customerInspections) || customerInspections.Count == 0)
                {
                    result.VendorsSkipped++;
                    continue;
                }

                var html = BuildCustomerEmailHtml(customer, customerInspections);

                var (ok, error) = await SendMailAsync(cfg,
                    recipients,
                    customer.ContactPerson ?? customer.CompanyName,
                    WEEKLY_SUBJECT, html, ct);

                _db.MailLogs.Add(new MailLog
                {
                    VendorCode      = $"CUS:{customer.CustomerId}",
                    ToEmail         = string.Join("; ", recipients),
                    Subject         = WEEKLY_SUBJECT,
                    SentAt          = DateTime.UtcNow,
                    IsSuccess       = ok,
                    ErrorMessage    = error,
                    InspectionCount = customerInspections.Count
                });

                if (ok)
                {
                    result.VendorsSent++;
                    _logger.LogInformation("Đã gửi Customer mail tới {emails} ({count} inspection)",
                        string.Join(", ", recipients), customerInspections.Count);
                }
                else
                {
                    result.VendorsFailed++;
                    result.Errors.Add($"{customer.CustomerId}: {error}");
                }

                await Task.Delay(500, ct);
            }

            await _db.SaveChangesAsync(ct);
            return result;
        }

        public async Task<(bool ok, string? error, int inspectionCount)> SendForCustomerAsync(
            string customerId, CancellationToken ct = default)
        {
            var cfg = await _configProvider.GetAsync(ct);

            var customer = await _db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

            if (customer == null) return (false, "Không tìm thấy customer", 0);

            var recipients = ResolveCustomerRecipients(customer);
            if (recipients.Count == 0)
                return (false, $"Customer chưa có email hợp lệ (Type={customer.ReportEmailType ?? "Registered"})", 0);

            var fromDate = DateTime.Today;
            var toDate   = fromDate.AddDays(cfg.LookAheadDays);

            var inspections = await _db.Inspections.AsNoTracking()
                .Where(i => i.CustomerId == customerId
                         && i.InspectionDate >= fromDate
                         && i.InspectionDate <  toDate
                         && (i.Status == InspectionStatus.Pending
                          || i.Status == InspectionStatus.InProgress))
                .OrderBy(i => i.InspectionDate)
                .ToListAsync(ct);

            if (inspections.Count == 0)
                return (false, "Customer không có inspection nào trong khoảng thời gian này", 0);

            var html = BuildCustomerEmailHtml(customer, inspections);

            var (ok, error) = await SendMailAsync(cfg,
                recipients,
                customer.ContactPerson ?? customer.CompanyName,
                WEEKLY_SUBJECT, html, ct);

            _db.MailLogs.Add(new MailLog
            {
                VendorCode      = $"CUS:{customer.CustomerId}",
                ToEmail         = string.Join("; ", recipients),
                Subject         = WEEKLY_SUBJECT,
                SentAt          = DateTime.UtcNow,
                IsSuccess       = ok,
                ErrorMessage    = error,
                InspectionCount = inspections.Count
            });
            await _db.SaveChangesAsync(ct);

            return (ok, error, inspections.Count);
        }

        // ═════════════════════════════════════════════════════════════
        //  TEST MAIL (preview template thực tế bằng dữ liệu mẫu)
        // ═════════════════════════════════════════════════════════════
        public async Task<(bool ok, string? error)> SendTestVendorMailAsync(string toEmail, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return (false, "Email không được để trống");

            var cfg = await _configProvider.GetAsync(ct);

            var sampleVendor = new Vendor
            {
                Code         = "VD-TEST",
                Name         = "Sample Vendor Co., Ltd.",
                ContactName  = "Vendor Contact",
                ContactEmail = toEmail
            };
            var sampleInspections = BuildSampleInspections();

            var html = BuildVendorEmailHtml(sampleVendor, sampleInspections);
            var subject = "[TEST] " + WEEKLY_SUBJECT;

            var (ok, error) = await SendMailAsync(cfg,
                new[] { toEmail }, sampleVendor.ContactName!, subject, html, ct);

            _db.MailLogs.Add(new MailLog
            {
                VendorCode      = "TEST-VENDOR",
                ToEmail         = toEmail,
                Subject         = subject,
                SentAt          = DateTime.UtcNow,
                IsSuccess       = ok,
                ErrorMessage    = error,
                InspectionCount = sampleInspections.Count
            });
            await _db.SaveChangesAsync(ct);

            return (ok, error);
        }

        public async Task<(bool ok, string? error)> SendTestCustomerMailAsync(string toEmail, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return (false, "Email không được để trống");

            var cfg = await _configProvider.GetAsync(ct);

            var sampleCustomer = new Customer
            {
                CustomerId    = "CP-TEST",
                CompanyName   = "Sample Customer Co., Ltd.",
                ContactPerson = "Customer Contact",
                Email         = toEmail
            };
            var sampleInspections = BuildSampleInspections();

            var html = BuildCustomerEmailHtml(sampleCustomer, sampleInspections);
            var subject = "[TEST] " + WEEKLY_SUBJECT;

            var (ok, error) = await SendMailAsync(cfg,
                new[] { toEmail }, sampleCustomer.ContactPerson!, subject, html, ct);

            _db.MailLogs.Add(new MailLog
            {
                VendorCode      = "TEST-CUSTOMER",
                ToEmail         = toEmail,
                Subject         = subject,
                SentAt          = DateTime.UtcNow,
                IsSuccess       = ok,
                ErrorMessage    = error,
                InspectionCount = sampleInspections.Count
            });
            await _db.SaveChangesAsync(ct);

            return (ok, error);
        }

        // ═════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════

        /// <summary>
        /// Tính danh sách email nhận của customer theo ReportEmailType:
        ///   - "Registered" (default): gửi cả Email VÀ AlternateReportEmail (nếu có)
        ///   - "Alternate":             chỉ gửi AlternateReportEmail
        /// </summary>
        private static List<string> ResolveCustomerRecipients(Customer c)
        {
            var list = new List<string>();
            var type = (c.ReportEmailType ?? "Registered").Trim();

            if (string.Equals(type, "Alternate", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(c.AlternateReportEmail))
                    list.Add(c.AlternateReportEmail.Trim());
            }
            else // Registered (default)
            {
                if (!string.IsNullOrWhiteSpace(c.Email))
                    list.Add(c.Email.Trim());
                if (!string.IsNullOrWhiteSpace(c.AlternateReportEmail))
                    list.Add(c.AlternateReportEmail.Trim());
            }

            // Khử trùng (case-insensitive) và validate cơ bản
            return list
                .Where(e => e.Contains('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<Inspection> BuildSampleInspections()
        {
            var today = DateTime.Today;
            return new List<Inspection>
            {
                new Inspection
                {
                    InspectionDate = today.AddDays(2),
                    JobNumber      = "JB-2025-0001",
                    InspectionType = InspectionType.PST,
                    PoNumber       = "PO-1001",
                    ProductName    = "Wooden Side Table",
                    ItemNumber     = "WST-001",
                    CustomerName   = "Sample Customer Co., Ltd.",
                    VendorName     = "Sample Vendor Co., Ltd.",
                    InspectorName  = "John Doe",
                },
                new Inspection
                {
                    InspectionDate = today.AddDays(5),
                    JobNumber      = "JB-2025-0002",
                    InspectionType = InspectionType.DPI,
                    PoNumber       = "PO-1002",
                    ProductName    = "Storage Cabinet",
                    ItemNumber     = "SCB-002",
                    CustomerName   = "Sample Customer Co., Ltd.",
                    VendorName     = "Sample Vendor Co., Ltd.",
                    InspectorName  = "Jane Smith",
                }
            };
        }

        private async Task<(bool ok, string? error)> SendMailAsync(
            MailConfigSnapshot cfg,
            IReadOnlyCollection<string> toEmails,
            string toDisplayName,
            string subject, string htmlBody, CancellationToken ct)
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

                bool first = true;
                foreach (var addr in toEmails)
                {
                    if (string.IsNullOrWhiteSpace(addr)) continue;
                    if (first)
                    {
                        msg.To.Add(new MailAddress(addr, toDisplayName));
                        first = false;
                    }
                    else
                    {
                        msg.To.Add(new MailAddress(addr));
                    }
                }

                if (msg.To.Count == 0)
                    return (false, "Không có địa chỉ email hợp lệ");

                await smtp.SendMailAsync(msg, ct);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi mail tới {emails}", string.Join(", ", toEmails));
                return (false, ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  TEMPLATE: VENDOR
        //  Theo ảnh: header "Vendor", subject "Summary Schedule : 14 Days Inspection",
        //  bảng có cột Customer Name, kèm đoạn "48 hours rule".
        // ─────────────────────────────────────────────────────────────
        private static string BuildVendorEmailHtml(Vendor vendor, List<Inspection> inspections)
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>Summary Schedule - Vendor</title></head>
<body style='font-family:Times New Roman,Times,serif;color:#000;background:#fff;padding:20px;margin:0;'>
  <div style='max-width:900px;margin:0 auto;'>
    <p style='margin:0 0 12px;'>Dear Vendor,</p>
    <p style='margin:0 0 14px;'>Please find below the summary schedule for your upcoming 14-day inspection:</p>");

            sb.Append(@"
    <table style='width:100%;border-collapse:collapse;font-size:13px;'>
      <thead>
        <tr>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>No</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Inspection Date</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Job Number</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Customer Name</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Inspection Type</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>P.O. Number</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Product Name</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Product Number</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Inspector Name</th>
        </tr>
      </thead>
      <tbody>");

            int no = 1;
            foreach (var i in inspections)
            {
                sb.Append($@"
        <tr>
          <td style='border:1px solid #000;padding:6px 8px;'>{no++}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{i.InspectionDate:dd/MM/yyyy}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.JobNumber)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.CustomerName)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(InspTypeLabel(i.InspectionType))}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.PoNumber)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.ProductName)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.ItemNumber)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.InspectorName)}</td>
        </tr>");
            }

            sb.Append(@"
      </tbody>
    </table>

    <p style='margin:20px 0 12px;'>Kindly note that changes to the schedule are not permitted within 48 hours of the inspection date.</p>
    <p style='margin:0 0 12px;'>Should you have any questions or require adjustments outside of this window, please reach out to our support team.</p>
    <p style='margin:0 0 12px;'>Thank you for your attention.</p>
    <p style='margin:0 0 4px;'>Best Regards,</p>
    <p style='margin:0;font-weight:700;'>Inticoo Global Services</p>
  </div>
</body></html>");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────
        //  TEMPLATE: CUSTOMER
        //  Theo ảnh: header "Customer", subject như vendor,
        //  bảng có cột Vendor Name, KHÔNG có "48 hours rule",
        //  có dòng "This schedule is provided for your reference...".
        // ─────────────────────────────────────────────────────────────
        private static string BuildCustomerEmailHtml(Customer customer, List<Inspection> inspections)
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>Summary Schedule - Customer</title></head>
<body style='font-family:Times New Roman,Times,serif;color:#000;background:#fff;padding:20px;margin:0;'>
  <div style='max-width:900px;margin:0 auto;'>
    <p style='margin:0 0 12px;'>Dear Customer,</p>
    <p style='margin:0 0 14px;'>Please find below the summary schedule for your upcoming 14-day inspection:</p>");

            sb.Append(@"
    <table style='width:100%;border-collapse:collapse;font-size:13px;'>
      <thead>
        <tr>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>No</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Inspection Date</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Job Number</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Vendor Name</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Inspection Type</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>P.O. Number</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Product Name</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Product Number</th>
          <th style='border:1px solid #000;padding:6px 8px;text-align:left;'>Inspector Name</th>
        </tr>
      </thead>
      <tbody>");

            int no = 1;
            foreach (var i in inspections)
            {
                sb.Append($@"
        <tr>
          <td style='border:1px solid #000;padding:6px 8px;'>{no++}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{i.InspectionDate:dd/MM/yyyy}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.JobNumber)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.VendorName)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(InspTypeLabel(i.InspectionType))}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.PoNumber)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.ProductName)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.ItemNumber)}</td>
          <td style='border:1px solid #000;padding:6px 8px;'>{Esc(i.InspectorName)}</td>
        </tr>");
            }

            sb.Append(@"
      </tbody>
    </table>

    <p style='margin:20px 0 12px;'>This schedule is provided for your reference to help you prepare accordingly. Should you have any questions or require adjustments, please reach out to our support team.</p>
    <p style='margin:0 0 12px;'>Thank you for your attention.</p>
    <p style='margin:0 0 4px;'>Best Regards,</p>
    <p style='margin:0;font-weight:700;'>Inticoo Global Services</p>
  </div>
</body></html>");

            return sb.ToString();
        }

        private static string InspTypeLabel(InspectionType t) => t switch
        {
            InspectionType.PPT => "PPT (Pre-Production)",
            InspectionType.DPI => "DPI (During Production)",
            InspectionType.PST => "PST (Pre-Shipment)",
            _                  => t.ToString()
        };

        private static string Esc(string? s)
            => string.IsNullOrEmpty(s) ? "" : WebUtility.HtmlEncode(s);
    }
}
