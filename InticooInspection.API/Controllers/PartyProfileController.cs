using InticooInspection.Domain.Security;
using System.Text.RegularExpressions;
using InticooInspection.API.Services;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    /// <summary>
    /// Người liên hệ + Report Settings cho Customer / Vendor (trang Edit 2 tab).
    ///   GET/PUT api/party/{customer|vendor}/{id}/contacts
    ///   GET/PUT api/party/{customer|vendor}/{id}/report-settings
    /// </summary>
    [ApiController]
    [Route("api/party/{type}/{id:int}")]
    public class PartyProfileController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PartyProfileController(AppDbContext db) => _db = db;

        private static readonly string[] CustomerKinds = { "PPI", "DPI", "PSI", "Schedule" };
        private static readonly string[] VendorKinds   = { "Schedule" };
        private static readonly string[] Modes         = { "Immediate", "ServerOnly", "Scheduled", "Manual" };
        private static readonly string[] Days          = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static string? Norm(string type) => type.ToLowerInvariant() switch
        {
            "customer" => "Customer",
            "vendor"   => "Vendor",
            _          => null
        };

        private async Task<bool> PartyExistsAsync(string pt, int id) => pt == "Customer"
            ? await _db.Customers.AnyAsync(c => c.Id == id)
            : await _db.Vendors.AnyAsync(v => v.Id == id);

        // ─────────────────────────────────────────────────────────────
        // CONTACTS
        // ─────────────────────────────────────────────────────────────
        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts(string type, int id)
        {
            var pt = Norm(type);
            if (pt == null) return NotFound();
            if (!await PartyExistsAsync(pt, id)) return NotFound();

            var rows = await _db.PartyContacts.AsNoTracking()
                .Where(c => c.PartyType == pt && c.PartyId == id)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .Select(c => new ContactDto
                {
                    Id = c.Id, Name = c.Name, Position = c.Position, Email = c.Email, Phone = c.Phone,
                    ReceiveReports = c.ReceiveReports, ScheduleEmail = c.ScheduleEmail,
                    IsPrimary = c.IsPrimary, IsActive = c.IsActive
                })
                .ToListAsync();

            // Dữ liệu cũ: chưa có bảng contact → dựng 1 dòng từ cột contact cũ để không mất thông tin
            if (rows.Count == 0)
            {
                var legacy = pt == "Customer"
                    ? await _db.Customers.AsNoTracking().Where(c => c.Id == id)
                        .Select(c => new ContactDto { Name = c.ContactPerson ?? "", Position = c.Position, Email = c.Email, Phone = c.Mobile }).FirstOrDefaultAsync()
                    : await _db.Vendors.AsNoTracking().Where(v => v.Id == id)
                        .Select(v => new ContactDto { Name = v.ContactName ?? "", Position = v.ContactTitle, Email = v.ContactEmail, Phone = v.ContactPhone }).FirstOrDefaultAsync();
                if (legacy != null && (!string.IsNullOrWhiteSpace(legacy.Name) || !string.IsNullOrWhiteSpace(legacy.Email)))
                {
                    legacy.IsPrimary = true; legacy.ReceiveReports = true; legacy.ScheduleEmail = pt == "Vendor";
                    rows.Add(legacy);
                }
            }
            return Ok(rows);
        }

        [HttpPut("contacts")]
        public async Task<IActionResult> SaveContacts(string type, int id, [FromBody] List<ContactDto> contacts)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.Directory, PermAction.Create), (ModuleKeys.Directory, PermAction.Edit)) is { } denied) return denied;
            var pt = Norm(type);
            if (pt == null) return NotFound();
            if (!await PartyExistsAsync(pt, id)) return NotFound();

            contacts ??= new();
            foreach (var c in contacts)
            {
                if (string.IsNullOrWhiteSpace(c.Name))
                    return BadRequest(new { message = "Contact name is required." });
                if (!string.IsNullOrWhiteSpace(c.Email) && !EmailRx.IsMatch(c.Email.Trim()))
                    return BadRequest(new { message = $"Invalid email: {c.Email}" });
            }

            // đúng 1 contact chính: ưu tiên dòng được tick, không có thì dòng đầu
            var primary = contacts.FirstOrDefault(c => c.IsPrimary) ?? contacts.FirstOrDefault();

            var old = await _db.PartyContacts.Where(c => c.PartyType == pt && c.PartyId == id).ToListAsync();
            _db.PartyContacts.RemoveRange(old);

            int order = 0;
            foreach (var c in contacts)
            {
                _db.PartyContacts.Add(new PartyContact
                {
                    PartyType = pt, PartyId = id,
                    Name = c.Name.Trim(), Position = c.Position?.Trim(), Email = c.Email?.Trim(), Phone = c.Phone?.Trim(),
                    ReceiveReports = c.ReceiveReports, ScheduleEmail = c.ScheduleEmail,
                    IsPrimary = ReferenceEquals(c, primary), IsActive = c.IsActive, SortOrder = order++
                });
            }

            // Đồng bộ contact chính về cột cũ (danh sách, import, mail cũ vẫn đọc các cột này)
            if (pt == "Customer")
            {
                var cu = await _db.Customers.FindAsync(id);
                if (cu != null)
                {
                    cu.ContactPerson = primary?.Name; cu.Position = primary?.Position; cu.Mobile = primary?.Phone;
                }
            }
            else
            {
                var v = await _db.Vendors.FindAsync(id);
                if (v != null)
                {
                    v.ContactName = primary?.Name; v.ContactTitle = primary?.Position;
                    v.ContactPhone = primary?.Phone; v.ContactEmail = primary?.Email;
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────
        // REPORT SETTINGS
        // ─────────────────────────────────────────────────────────────
        [HttpGet("report-settings")]
        public async Task<IActionResult> GetReportSettings(string type, int id)
        {
            var pt = Norm(type);
            if (pt == null) return NotFound();
            if (!await PartyExistsAsync(pt, id)) return NotFound();

            var kinds = pt == "Customer" ? CustomerKinds : VendorKinds;
            var saved = await _db.PartyReportSettings.AsNoTracking()
                .Where(s => s.PartyType == pt && s.PartyId == id).ToListAsync();

            var result = kinds.Select(k =>
            {
                var s = saved.FirstOrDefault(x => x.Kind == k);
                return s != null ? ToDto(s) : Default(pt, k);
            }).ToList();
            return Ok(result);
        }

        [HttpPut("report-settings")]
        public async Task<IActionResult> SaveReportSettings(string type, int id, [FromBody] List<ReportSettingDto> settings)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.Directory, PermAction.Create), (ModuleKeys.Directory, PermAction.Edit)) is { } denied) return denied;
            var pt = Norm(type);
            if (pt == null) return NotFound();
            if (!await PartyExistsAsync(pt, id)) return NotFound();

            var kinds = pt == "Customer" ? CustomerKinds : VendorKinds;
            var existing = await _db.PartyReportSettings.Where(s => s.PartyType == pt && s.PartyId == id).ToListAsync();

            foreach (var d in settings ?? new())
            {
                if (!kinds.Contains(d.Kind)) continue;
                if (!Modes.Contains(d.DeliveryMode)) return BadRequest(new { message = $"Invalid delivery mode: {d.DeliveryMode}" });
                if (string.IsNullOrWhiteSpace(d.Subject)) return BadRequest(new { message = "Email subject is required." });
                if (d.PeriodDays != 7 && d.PeriodDays != 14) d.PeriodDays = 7;

                var s = existing.FirstOrDefault(x => x.Kind == d.Kind);
                if (s == null)
                {
                    s = new PartyReportSetting { PartyType = pt, PartyId = id, Kind = d.Kind };
                    _db.PartyReportSettings.Add(s);
                }
                s.Enabled      = d.Enabled;
                s.DeliveryMode = d.DeliveryMode;
                s.Language     = string.IsNullOrWhiteSpace(d.Language) ? "en" : d.Language;
                s.Subject      = d.Subject.Trim();
                s.Body         = SanitizeHtml(d.Body);
                s.PeriodDays   = d.PeriodDays;
                s.SendDays     = string.Join(",", (d.SendDays ?? new()).Where(x => Days.Contains(x)).Distinct());
                s.SendTime     = d.SendTime;
                s.TimeZone     = d.TimeZone;
            }
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────
        private static readonly Regex BadTag = new(@"<\s*(script|style|iframe|object|embed)[^>]*>.*?<\s*/\s*\1\s*>|<\s*(script|style|iframe|object|embed)[^>]*/?\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex OnAttr = new(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex JsHref = new(@"(href|src)\s*=\s*(""|')?\s*javascript:[^""'>\s]*(""|')?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Bỏ script/iframe/on*=/javascript: khỏi HTML mẫu email trước khi lưu.</summary>
        private static string SanitizeHtml(string? html)
            => JsHref.Replace(OnAttr.Replace(BadTag.Replace(html ?? "", ""), ""), "");

        private static ReportSettingDto ToDto(PartyReportSetting s) => new()
        {
            Kind = s.Kind, Enabled = s.Enabled, DeliveryMode = s.DeliveryMode, Language = s.Language,
            Subject = s.Subject ?? "", Body = s.Body ?? "", PeriodDays = s.PeriodDays,
            SendDays = (s.SendDays ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            SendTime = s.SendTime ?? "09:00", TimeZone = s.TimeZone ?? "Asia/Ho_Chi_Minh"
        };

        /// <summary>Mặc định (tiếng Anh). Client đổi sang ngôn ngữ đã chọn khi người dùng chưa sửa nội dung.</summary>
        private static ReportSettingDto Default(string pt, string kind)
        {
            var d = new ReportSettingDto
            {
                Kind = kind, Enabled = true, Language = "en", PeriodDays = 7,
                SendDays = new() { "Mon" }, SendTime = "09:00", TimeZone = "Asia/Ho_Chi_Minh",
                DeliveryMode = pt == "Vendor" ? "Immediate" : "Immediate"
            };
            // subject/body rỗng → client điền mẫu mặc định theo ngôn ngữ
            return d;
        }

        public class ContactDto
        {
            public int     Id             { get; set; }
            public string  Name           { get; set; } = "";
            public string? Position       { get; set; }
            public string? Email          { get; set; }
            public string? Phone          { get; set; }
            public bool    ReceiveReports { get; set; } = true;
            public bool    ScheduleEmail  { get; set; }
            public bool    IsPrimary      { get; set; }
            public bool    IsActive       { get; set; } = true;
        }

        public class ReportSettingDto
        {
            public string       Kind         { get; set; } = "";
            public bool         Enabled      { get; set; } = true;
            public string       DeliveryMode { get; set; } = "Immediate";
            public string       Language     { get; set; } = "en";
            public string       Subject      { get; set; } = "";
            public string       Body         { get; set; } = "";
            public int          PeriodDays   { get; set; } = 7;
            public List<string> SendDays     { get; set; } = new();
            public string       SendTime     { get; set; } = "09:00";
            public string       TimeZone     { get; set; } = "Asia/Ho_Chi_Minh";
        }
    }
}
