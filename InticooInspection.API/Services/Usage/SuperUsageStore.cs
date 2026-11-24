using System.Globalization;
using Microsoft.Data.SqlClient;

namespace InticooInspection.API.Services.Usage
{
    /// <summary>
    /// Đọc/ghi phần "đo sử dụng + hạn mức gói" trong DB quản trị của Portal (InticooInspectionSupper, schema sup).
    /// SQL thuần (API không tham chiếu Portal.Core). Thiếu ConnectionStrings:Super → tắt (không đo, không chặn).
    /// </summary>
    public sealed class SuperUsageStore
    {
        private readonly string? _cs;
        public SuperUsageStore(IConfiguration cfg)
        {
            var cs = cfg.GetConnectionString("Super");
            _cs = string.IsNullOrWhiteSpace(cs) ? null : cs;
        }

        public bool Enabled => _cs != null;

        private async Task<SqlConnection> OpenAsync(CancellationToken ct)
        {
            var cn = new SqlConnection(_cs);
            await cn.OpenAsync(ct);
            return cn;
        }

        /// <summary>Các khóa usage.* trong sup.Settings (khóa chưa lưu → dùng giá trị mặc định ở Portal.Core SettingsService).</summary>
        public async Task<Dictionary<string, string>> SettingsAsync(CancellationToken ct = default)
        {
            var d = new Dictionary<string, string>
            {
                ["usage.intervalHours"] = "6", ["usage.warnPercent"] = "80", ["usage.enforce"] = "true",
                ["usage.measureRequestedAt"] = "", ["usage.lastRunAt"] = ""
            };
            await using var cn = await OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT [Key], [Value] FROM sup.Settings WHERE [Key] LIKE 'usage.%'", cn);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) d[rd.GetString(0)] = rd.GetString(1);
            return d;
        }

        /// <summary>Nhận lượt đo (chống 2 instance API cùng đo): chỉ thành công khi usage.lastRunAt chưa bị instance khác đổi.</summary>
        public async Task<bool> ClaimRunAsync(string? previous, DateTime now, CancellationToken ct = default)
        {
            await using var cn = await OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sup.Settings WHERE [Key]='usage.lastRunAt')
    INSERT INTO sup.Settings([Key],[Value]) VALUES ('usage.lastRunAt', @now);
ELSE
    UPDATE sup.Settings SET [Value]=@now WHERE [Key]='usage.lastRunAt' AND [Value]=@prev;
SELECT @@ROWCOUNT;", cn);
            cmd.Parameters.AddWithValue("@now", now.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@prev", previous ?? "");
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
        }

        public async Task SaveSnapshotAsync(UsageResult r, CancellationToken ct = default)
        {
            await using var cn = await OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
INSERT INTO sup.CompanyUsageSnapshots (CompanyId, TenantCode, [At], DbDataMb, DbLogMb, FilesMb, FileCount, Users, ActiveUsers, LockedUsers,
    ActiveUsers7d, ActiveUsers30d, LastLoginAt, LastActivityAt, Inspections, InspectionsThisMonth, ContainerChecks, OpenCapa, Vendors, Products, Error)
VALUES (@cid, @code, @at, @dd, @dl, @fm, @fc, @u, @au, @lu, @a7, @a30, @ll, @la, @ins, @insm, @clc, @capa, @ven, @prod, @err);
IF @cid IS NOT NULL AND @err IS NULL
    UPDATE sup.Companies SET DbSizeGb=@dbgb, FileSizeGb=@fgb, StorageUsedGb=@dbgb+@fgb, FileCount=@fc, UserCount=@u, ActiveUserCount=@au,
        LastActivityAt=@la, StorageMeasuredAt=@at WHERE Id=@cid;", cn);
            object N(object? v) => v ?? DBNull.Value;
            cmd.Parameters.AddWithValue("@cid", N(r.CompanyId));
            cmd.Parameters.AddWithValue("@code", r.TenantCode);
            cmd.Parameters.AddWithValue("@at", r.At);
            cmd.Parameters.AddWithValue("@dd", r.DbDataMb);
            cmd.Parameters.AddWithValue("@dl", r.DbLogMb);
            cmd.Parameters.AddWithValue("@fm", r.FilesMb);
            cmd.Parameters.AddWithValue("@fc", r.FileCount);
            cmd.Parameters.AddWithValue("@u", r.Users);
            cmd.Parameters.AddWithValue("@au", r.ActiveUsers);
            cmd.Parameters.AddWithValue("@lu", r.LockedUsers);
            cmd.Parameters.AddWithValue("@a7", r.ActiveUsers7d);
            cmd.Parameters.AddWithValue("@a30", r.ActiveUsers30d);
            cmd.Parameters.AddWithValue("@ll", N(r.LastLoginAt));
            cmd.Parameters.AddWithValue("@la", N(r.LastActivityAt));
            cmd.Parameters.AddWithValue("@ins", r.Inspections);
            cmd.Parameters.AddWithValue("@insm", r.InspectionsThisMonth);
            cmd.Parameters.AddWithValue("@clc", r.ContainerChecks);
            cmd.Parameters.AddWithValue("@capa", r.OpenCapa);
            cmd.Parameters.AddWithValue("@ven", r.Vendors);
            cmd.Parameters.AddWithValue("@prod", r.Products);
            cmd.Parameters.AddWithValue("@err", N(r.Error is { Length: > 500 } e ? e[..500] : r.Error));
            cmd.Parameters.AddWithValue("@dbgb", Math.Round((r.DbDataMb + r.DbLogMb) / 1024d, 4));
            cmd.Parameters.AddWithValue("@fgb", Math.Round(r.FilesMb / 1024d, 4));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveDiskAsync(string host, string drive, double totalGb, double freeGb, double uploadsGb, DateTime at, CancellationToken ct = default)
        {
            await using var cn = await OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "INSERT INTO sup.PlatformDiskSnapshots ([At], Host, Drive, TotalGb, FreeGb, UploadsGb) VALUES (@at, @h, @d, @t, @f, @u)", cn);
            cmd.Parameters.AddWithValue("@at", at);
            cmd.Parameters.AddWithValue("@h", host.Length > 100 ? host[..100] : host);
            cmd.Parameters.AddWithValue("@d", drive.Length > 20 ? drive[..20] : drive);
            cmd.Parameters.AddWithValue("@t", Math.Round(totalGb, 3));
            cmd.Parameters.AddWithValue("@f", Math.Round(freeGb, 3));
            cmd.Parameters.AddWithValue("@u", Math.Round(uploadsGb, 3));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>Hạn mức gói đang dùng của công ty + dung lượng đo gần nhất + cờ tạm bỏ chặn.</summary>
        public async Task<PlanLimits?> LimitsAsync(Guid companyId, CancellationToken ct = default)
        {
            await using var cn = await OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
SELECT p.MaxUsers, p.StorageGb, c.StorageUsedGb, c.LimitOverrideUntil, p.Name
FROM sup.Companies c
OUTER APPLY (SELECT TOP 1 s.PlanId FROM sup.Subscriptions s WHERE s.CompanyId = c.Id AND s.Status <> 'Cancelled' ORDER BY s.Id DESC) s
LEFT JOIN sup.Plans p ON p.Id = s.PlanId
WHERE c.Id = @id;", cn);
            cmd.Parameters.AddWithValue("@id", companyId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return null;
            return new PlanLimits(
                rd.IsDBNull(0) ? 0 : rd.GetInt32(0),
                rd.IsDBNull(1) ? 0 : rd.GetInt32(1),
                rd.GetDouble(2),
                rd.IsDBNull(3) ? null : rd.GetDateTime(3),
                rd.IsDBNull(4) ? null : rd.GetString(4));
        }
    }

    public sealed record PlanLimits(int MaxUsers, int StorageGb, double UsedGb, DateTime? OverrideUntil, string? PlanName);

    /// <summary>Kết quả đo 1 công ty (hoặc DB gốc).</summary>
    public sealed class UsageResult
    {
        public Guid? CompanyId { get; init; }
        public string TenantCode { get; init; } = "";
        public DateTime At { get; init; }
        public double DbDataMb, DbLogMb, FilesMb;
        public int FileCount, Users, ActiveUsers, LockedUsers, ActiveUsers7d, ActiveUsers30d;
        public DateTime? LastLoginAt, LastActivityAt;
        public int Inspections, InspectionsThisMonth, ContainerChecks, OpenCapa, Vendors, Products;
        public string? Error;
    }
}
