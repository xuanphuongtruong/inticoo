using System.Text.Json;
using InticooInspection.API.Services;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    /// <summary>
    /// Cài đặt toàn hệ thống (màn Settings): inspection-defect, system, billing.
    /// Mỗi nhóm lưu nguyên JSON do client gửi; chỉ Admin được xem/sửa.
    /// </summary>
    [ApiController]
    [Route("api/settings")]
    [AdminApi]   // đăng nhập đã được RequireSignedInFilter (toàn cục) kiểm tra; ở đây chỉ cho Admin
    public class SettingsController : ControllerBase
    {
        public static readonly string[] Sections = { SettingsStore.InspectionDefect, SettingsStore.SystemSection, SettingsStore.Billing };
        private const int MaxJsonChars = 64 * 1024;

        private readonly AppDbContext _db;
        public SettingsController(AppDbContext db) => _db = db;

        // GET api/settings/{section} → JSON đã lưu, 204 nếu chưa có (client dùng mặc định)
        [HttpGet("{section}")]
        public async Task<IActionResult> Get(string section, CancellationToken ct)
        {
            if (!Sections.Contains(section)) return NotFound(new { message = "Unknown settings section." });
            var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Section == section, ct);
            if (row == null) return NoContent();
            return Content(row.Json, "application/json");
        }

        // PUT api/settings/{section} — body: JSON object của nhóm
        [HttpPut("{section}")]
        public async Task<IActionResult> Put(string section, [FromBody] JsonElement body, CancellationToken ct)
        {
            if (!Sections.Contains(section)) return NotFound(new { message = "Unknown settings section." });
            if (body.ValueKind != JsonValueKind.Object) return BadRequest(new { message = "Settings must be a JSON object." });

            var json = body.GetRawText();
            if (json.Length > MaxJsonChars) return BadRequest(new { message = "Settings payload is too large." });

            var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Section == section, ct);
            if (row == null)
            {
                row = new AppSetting { Section = section };
                _db.AppSettings.Add(row);
            }
            row.Json = json;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = User.Identity?.Name;
            await _db.SaveChangesAsync(ct);
            return Ok(new { success = true, updatedAt = row.UpdatedAt });
        }
    }
}
