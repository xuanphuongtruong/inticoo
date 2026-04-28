using InticooInspection.API.Services;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/mail")]
    [Authorize]
    public class MailController : ControllerBase
    {
        private readonly AppDbContext             _db;
        private readonly IInspectionMailService   _mail;

        public MailController(AppDbContext db, IInspectionMailService mail)
        {
            _db   = db;
            _mail = mail;
        }

        // POST api/mail/run-weekly
        // Trigger thủ công job hàng tuần (dùng cho test hoặc gửi bù)
        [HttpPost("run-weekly")]
        public async Task<IActionResult> RunWeekly(CancellationToken ct)
        {
            var result = await _mail.SendWeeklyVendorMailsAsync(ct);
            return Ok(result);
        }

        // POST api/mail/send-vendor/{vendorId}
        // Gửi mail cho 1 vendor cụ thể
        [HttpPost("send-vendor/{vendorId:int}")]
        public async Task<IActionResult> SendVendor(int vendorId, CancellationToken ct)
        {
            var (ok, error, count) = await _mail.SendForVendorAsync(vendorId, ct);
            if (!ok) return BadRequest(new { success = false, error, inspectionCount = count });
            return Ok(new { success = true, inspectionCount = count });
        }

        // GET api/mail/logs?take=100&vendorId=
        [HttpGet("logs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int take = 100,
            [FromQuery] int? vendorId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var q = _db.MailLogs.AsNoTracking().AsQueryable();

            if (vendorId.HasValue) q = q.Where(l => l.VendorId == vendorId);
            if (from.HasValue)     q = q.Where(l => l.SentAt >= from.Value);
            if (to.HasValue)       q = q.Where(l => l.SentAt <= to.Value);

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

        // GET api/mail/stats?days=30
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
                vendorsUnique    = logs.Where(l => l.VendorId.HasValue)
                                       .Select(l => l.VendorId).Distinct().Count(),
                lastRun          = logs.OrderByDescending(l => l.SentAt)
                                       .Select(l => (DateTime?)l.SentAt).FirstOrDefault()
            });
        }
    }
}
