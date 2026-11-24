using InticooInspection.API.Services;
using InticooInspection.API.Services.Tenancy;
using InticooInspection.API.Services.Usage;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    /// <summary>
    /// Settings › Billing &amp; Invoices của app: gói đang dùng, mức sử dụng, hóa đơn và thanh toán THẬT của công ty
    /// (đọc từ DB quản trị của Portal — sup.Subscriptions / sup.Invoices / sup.Payments). Chỉ Admin của công ty.
    /// DB gốc (khách cũ không đăng ký qua Portal) → applies=false.
    /// </summary>
    [ApiController]
    [Route("api/billing")]
    [AllowAnonymous]
    public class BillingController : ControllerBase
    {
        private readonly UserAccess _access;
        private readonly TenantContext _tenant;
        private readonly IConfiguration _cfg;
        private readonly PlanLimitService _limits;
        private readonly AppDbContext _db;
        public BillingController(UserAccess access, TenantContext tenant, IConfiguration cfg, PlanLimitService limits, AppDbContext db)
        { _access = access; _tenant = tenant; _cfg = cfg; _limits = limits; _db = db; }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            if (await _access.RequireAdminAsync() is { } denied) return denied;
            var cs = _cfg.GetConnectionString("Super");
            var upgradeUrl = string.IsNullOrWhiteSpace(_cfg["Portal:PublicUrl"]) ? null : _cfg["Portal:PublicUrl"]!.TrimEnd('/') + "/pricing";
            if (_tenant.Tenant is not { } t || string.IsNullOrWhiteSpace(cs)) return Ok(new { applies = false, upgradeUrl });

            await using var cn = new SqlConnection(cs);
            await cn.OpenAsync(ct);

            // Công ty + thuê bao gần nhất (chưa hủy) + gói
            object? company = null, subscription = null;
            string? features = null; int? planId = null;
            await using (var cmd = new SqlCommand(@"
SELECT c.Name, c.Address, c.Country, c.Email, c.Phone,
       s.BillingCycle, s.Price, s.StartDate, s.NextBillingDate, s.Status, s.AutoRenew, p.Name, p.Code, p.Features, p.MaxUsers, p.StorageGb, p.Id
FROM sup.Companies c
OUTER APPLY (SELECT TOP 1 * FROM sup.Subscriptions x WHERE x.CompanyId = c.Id AND x.Status <> 'Cancelled' ORDER BY x.Id DESC) s
LEFT JOIN sup.Plans p ON p.Id = s.PlanId
WHERE c.Id = @id", cn))
            {
                cmd.Parameters.AddWithValue("@id", t.Id);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                if (await rd.ReadAsync(ct))
                {
                    company = new { name = rd.GetString(0), address = rd.GetString(1), country = rd.GetString(2), email = rd.GetString(3), phone = rd.GetString(4) };
                    if (!rd.IsDBNull(11))
                    {
                        features = rd.IsDBNull(13) ? null : rd.GetString(13);
                        planId = rd.GetInt32(16);
                        var maxUsers = rd.GetInt32(14); var storage = rd.GetInt32(15);
                        subscription = new
                        {
                            plan = rd.GetString(11), planCode = rd.GetString(12), cycle = rd.GetString(5), price = rd.GetDecimal(6),
                            startDate = rd.GetDateTime(7), nextBillingDate = rd.IsDBNull(8) ? (DateTime?)null : rd.GetDateTime(8),
                            status = rd.GetString(9), autoRenew = rd.GetBoolean(10), maxUsers, storageGb = storage,
                            // dòng tính năng của gói (Super Admin › Settings › Subscription Plans): {users}/{storage} = hạn mức thật
                            features = (features ?? "").Split('\n').Select(x => x.Trim().TrimStart('*').Trim()).Where(x => x.Length > 0)
                                .Select(x => x.Replace("{users}", maxUsers.ToString("N0")).Replace("{storage}", storage.ToString("N0"))).ToList()
                        };
                    }
                }
            }

            var modules = new List<string>();
            if (planId != null)
            {
                await using var cmd = new SqlCommand("SELECT m.Name FROM sup.PlanModules pm JOIN sup.Modules m ON m.Id = pm.ModuleId WHERE pm.PlanId = @p ORDER BY m.SortOrder", cn);
                cmd.Parameters.AddWithValue("@p", planId);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct)) modules.Add(rd.GetString(0));
            }

            // Hóa đơn + phương thức của lần thanh toán thành công gần nhất cho từng hóa đơn
            var invoices = new List<object>();
            await using (var cmd = new SqlCommand(@"
SELECT i.Number, i.InvoiceDate, i.DueDate, i.PlanName, i.BillingCycle, i.Amount, i.Tax, i.Status, i.PaidAt,
       (SELECT TOP 1 p.Method FROM sup.Payments p WHERE p.InvoiceId = i.Id AND p.Status = 'Paid' ORDER BY p.Id DESC)
FROM sup.Invoices i WHERE i.CompanyId = @id ORDER BY i.InvoiceDate DESC, i.Id DESC", cn))
            {
                cmd.Parameters.AddWithValue("@id", t.Id);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    var date = rd.GetDateTime(1); var cycle = rd.GetString(4);
                    invoices.Add(new
                    {
                        number = rd.GetString(0), invoiceDate = date, dueDate = rd.GetDateTime(2), plan = rd.GetString(3), cycle,
                        periodStart = date, periodEnd = cycle == "Annual" ? date.AddYears(1) : date.AddMonths(1),
                        amount = rd.GetDecimal(5), tax = rd.GetDecimal(6), total = rd.GetDecimal(5) + rd.GetDecimal(6),
                        status = rd.GetString(7), paidAt = rd.IsDBNull(8) ? (DateTime?)null : rd.GetDateTime(8),
                        method = rd.IsDBNull(9) ? null : rd.GetString(9)
                    });
                }
            }

            // Phương thức thanh toán gần nhất (chỉ hãng thẻ + 4 số cuối — Portal không lưu số thẻ)
            object? payment = null;
            await using (var cmd = new SqlCommand(@"
SELECT TOP 1 Method, CardBrand, CardLast4, CardHolder, CreatedAt FROM sup.Payments
WHERE CompanyId = @id AND Status IN ('Paid', 'Refunded') ORDER BY Id DESC", cn))
            {
                cmd.Parameters.AddWithValue("@id", t.Id);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                if (await rd.ReadAsync(ct))
                    payment = new { method = rd.GetString(0), cardBrand = rd.IsDBNull(1) ? null : rd.GetString(1), last4 = rd.IsDBNull(2) ? null : rd.GetString(2),
                                    holder = rd.IsDBNull(3) ? null : rd.GetString(3), date = rd.GetDateTime(4) };
            }

            var s = await _limits.StatusAsync(ct);
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usage = new
            {
                storageGb = s.StorageGb, usedGb = s.UsedGb, maxUsers = s.MaxUsers, activeUsers = s.ActiveUsers,
                inspectionsThisMonth = await _db.Inspections.CountAsync(i => i.CreatedAt >= monthStart, ct)
            };
            return Ok(new { applies = true, company, subscription, modules, usage, invoices, payment, upgradeUrl });
        }
    }
}
