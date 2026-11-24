using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace InticooInspection.API.Services.Tenancy
{
    /// <summary>
    /// Danh bạ công ty khách, đọc từ DB quản trị của Portal (<c>InticooInspectionSupper</c>, schema <c>sup</c>):
    /// công ty nào có DB riêng, chuỗi kết nối (giải mã bằng <c>Security:TenantSecretKey</c> — cùng khóa với Portal),
    /// và email/username nào thuộc công ty nào (bảng <c>sup.CompanyUsers</c>) để chọn DB lúc đăng nhập.
    /// Thiếu <c>ConnectionStrings:Super</c> hoặc khóa → tắt, API chạy 1 DB như trước.
    /// </summary>
    public sealed class TenantDirectory
    {
        private readonly string? _superCs;
        private readonly SecretProtector? _secret;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TenantDirectory> _log;

        public TenantDirectory(IConfiguration cfg, IMemoryCache cache, ILogger<TenantDirectory> log)
        {
            _cache = cache; _log = log;
            var cs = cfg.GetConnectionString("Super");
            var key = cfg["Security:TenantSecretKey"];
            if (!string.IsNullOrWhiteSpace(cs) && !string.IsNullOrWhiteSpace(key) && key.Length >= 32)
            {
                _superCs = cs;
                _secret = new SecretProtector(cfg);
            }
            else log.LogWarning("Đa khách (tenant) TẮT: thiếu ConnectionStrings:Super hoặc Security:TenantSecretKey — chỉ dùng DB mặc định.");
        }

        public bool Enabled => _superCs != null;

        /// <summary>Trạng thái công ty còn được đăng nhập / gọi API (Overdue vẫn cho dùng trong thời gian chờ thanh toán).</summary>
        public static bool CanUse(string status) => status is "Active" or "Overdue";

        private const string SelectCompany =
            "SELECT c.Id, c.Code, c.Name, c.Status, c.ConnectionStringEnc FROM sup.Companies c " +
            "WHERE c.ProvisionStatus = 'Provisioned' AND c.IsDemo = 0 AND c.ConnectionStringEnc IS NOT NULL";

        /// <param name="fresh">true = bỏ qua cache (dùng khi cache báo công ty bị khóa: có thể vừa được mở lại ở Portal).</param>
        public async Task<TenantInfo?> GetAsync(Guid id, CancellationToken ct = default, bool fresh = false)
        {
            if (!Enabled) return null;
            if (fresh) _cache.Remove("tenant:" + id);
            return await _cache.GetOrCreateAsync("tenant:" + id, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);   // khóa/hủy công ty ở Portal có hiệu lực tối đa sau 2 phút
                var list = await QueryAsync(SelectCompany + " AND c.Id = @id", ct, ("@id", id));
                return list.FirstOrDefault();
            });
        }

        /// <summary>Các công ty có tài khoản mang email / username này (không phân biệt hoa thường).</summary>
        public async Task<List<TenantInfo>> FindByLoginAsync(string login, CancellationToken ct = default)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(login)) return new();
            try
            {
                return await QueryAsync(SelectCompany +
                    " AND EXISTS (SELECT 1 FROM sup.CompanyUsers u WHERE u.CompanyId = c.Id AND (u.Email = @l OR u.UserName = @l))",
                    ct, ("@l", login.Trim()));
            }
            catch (Exception ex)
            {
                // DB Super lỗi/mất kết nối KHÔNG được chặn đăng nhập của khách trên DB gốc → coi như không thuộc công ty nào
                _log.LogError(ex, "Không tra được công ty của tài khoản từ DB Super — chỉ đăng nhập vào DB gốc");
                return new();
            }
        }

        public async Task<List<TenantInfo>> ListAsync(CancellationToken ct = default) =>
            Enabled ? await QueryAsync(SelectCompany, ct) : new();

        /// <summary>Ghi/cập nhật bản sao user của công ty trong sup.CompanyUsers (để tìm công ty lúc đăng nhập + màn Users của Super Admin).</summary>
        public async Task SyncUserAsync(TenantInfo t, AppUser u, string? role, string? previousEmail = null, CancellationToken ct = default)
        {
            if (!Enabled) return;
            var email = (u.Email ?? u.UserName ?? "").Trim().ToLowerInvariant();
            if (email.Length == 0) return;
            var match = string.IsNullOrWhiteSpace(previousEmail) ? email : previousEmail.Trim().ToLowerInvariant();
            role = string.IsNullOrEmpty(role) ? "User" : role == "Admin" ? "Administrator" : role;
            try
            {
                await using var cn = new SqlConnection(_superCs);
                await cn.OpenAsync(ct);
                await using var cmd = new SqlCommand(@"
UPDATE sup.CompanyUsers SET Email=@e, UserName=@un, FullName=@fn, Phone=@ph, Role=@r, Status=@s, Department=@d, JobTitle=@j
 WHERE CompanyId=@c AND (LOWER(Email)=@m OR LOWER(Email)=@e);
IF @@ROWCOUNT = 0
  INSERT INTO sup.CompanyUsers (Id, CompanyId, FullName, Email, UserName, Phone, Role, Department, JobTitle, Language, Status, CreatedAt)
  VALUES (NEWID(), @c, @fn, @e, @un, @ph, @r, @d, @j, @lang, @s, SYSUTCDATETIME());", cn);
                cmd.Parameters.AddWithValue("@c", t.Id);
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@m", match);
                cmd.Parameters.AddWithValue("@un", (object?)u.UserName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fn", Cut(string.IsNullOrWhiteSpace(u.FullName) ? email : u.FullName, 150));
                cmd.Parameters.AddWithValue("@ph", (object?)Cut(u.Mobile ?? u.PhoneNumber, 40) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@r", Cut(role, 80)!);
                cmd.Parameters.AddWithValue("@s", u.IsActive ? "Active" : "Inactive");
                cmd.Parameters.AddWithValue("@d", (object?)Cut(u.Department, 80) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@j", (object?)Cut(u.Position, 80) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lang", Cut(u.UiLanguage ?? "en", 10)!);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                // Không chặn thao tác của khách vì lỗi đồng bộ; Super Admin có nút Sync Users để đồng bộ lại
                _log.LogError(ex, "Đồng bộ user {Email} lên sup.CompanyUsers lỗi (công ty {Code})", email, t.Code);
            }
        }

        public async Task RemoveUserAsync(TenantInfo t, string? email, CancellationToken ct = default)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(email)) return;
            try
            {
                await using var cn = new SqlConnection(_superCs);
                await cn.OpenAsync(ct);
                await using var cmd = new SqlCommand("DELETE FROM sup.CompanyUsers WHERE CompanyId=@c AND LOWER(Email)=@e", cn);
                cmd.Parameters.AddWithValue("@c", t.Id);
                cmd.Parameters.AddWithValue("@e", email.Trim().ToLowerInvariant());
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex) { _log.LogError(ex, "Xóa user {Email} khỏi sup.CompanyUsers lỗi (công ty {Code})", email, t.Code); }
        }

        public async Task TouchLoginAsync(TenantInfo t, string? email, CancellationToken ct = default)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(email)) return;
            try
            {
                await using var cn = new SqlConnection(_superCs);
                await cn.OpenAsync(ct);
                await using var cmd = new SqlCommand("UPDATE sup.CompanyUsers SET LastLoginAt=SYSUTCDATETIME(), Status='Active' WHERE CompanyId=@c AND LOWER(Email)=@e", cn);
                cmd.Parameters.AddWithValue("@c", t.Id);
                cmd.Parameters.AddWithValue("@e", email.Trim().ToLowerInvariant());
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex) { _log.LogWarning(ex, "Cập nhật LastLoginAt lỗi ({Code})", t.Code); }
        }

        private async Task<List<TenantInfo>> QueryAsync(string sql, CancellationToken ct, params (string Name, object Value)[] ps)
        {
            var list = new List<TenantInfo>();
            await using var cn = new SqlConnection(_superCs);
            await cn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, cn);
            foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var code = rd.GetString(1);
                try
                {
                    list.Add(new TenantInfo(rd.GetGuid(0), code, rd.GetString(2), rd.GetString(3), _secret!.Unprotect(rd.GetString(4))));
                }
                catch (Exception ex) { _log.LogError(ex, "Không giải mã được chuỗi kết nối của công ty {Code} (sai Security:TenantSecretKey?)", code); }
            }
            return list;
        }

        private static string? Cut(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
    }
}
