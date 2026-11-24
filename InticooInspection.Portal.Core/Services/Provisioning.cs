using InticooInspection.Infrastructure.Tenancy;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using InticooInspection.Portal.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Services;

/// <summary>
/// Cấp hạ tầng riêng cho một khách hàng: 1 database + 1 SQL login chỉ có quyền trong DB đó,
/// áp migration của app chính, tạo tài khoản Admin đầu tiên. Chuỗi kết nối của khách được mã hóa trong DB Super.
/// Cách ly dữ liệu giữa các công ty = cách ly ở mức database (không dùng cột TenantId dùng chung).
/// </summary>
public class ProvisioningService
{
    private static readonly Regex SafeName = new("^[a-z0-9_]{1,40}$");

    private readonly PortalDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly SecretProtector _secret;
    private readonly AuditService _audit;
    private readonly ILogger<ProvisioningService> _log;

    public ProvisioningService(PortalDbContext db, IConfiguration cfg, SecretProtector secret, AuditService audit, ILogger<ProvisioningService> log)
    { _db = db; _cfg = cfg; _secret = secret; _audit = audit; _log = log; }

    public bool Enabled => string.Equals(_cfg["Provisioning:Mode"], "Database", StringComparison.OrdinalIgnoreCase);

    private SqlConnectionStringBuilder Admin(string database)
    {
        var b = new SqlConnectionStringBuilder(_cfg.GetConnectionString("Super")) { InitialCatalog = database };
        return b;
    }

    public async Task<bool> ProvisionAsync(Company c)
    {
        if (!Enabled) { _log.LogInformation("Provisioning tắt (Mode != Database) — bỏ qua {Code}", c.Code); return false; }
        if (c.ProvisionStatus == ProvisionStatus.Provisioned) return true;
        if (!SafeName.IsMatch(c.Code)) throw new InvalidOperationException("Mã công ty không hợp lệ để đặt tên database.");

        var reg = await _db.Registrations.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync(r => r.CompanyId == c.Id);
        var dbName = (_cfg["Provisioning:DatabasePrefix"] ?? "InticooInspection_") + c.Code;
        var login = "tn_" + c.Code;
        var loginPw = RandomPassword(28);

        try
        {
            // 1) Tạo DB (nếu DB tồn tại mà công ty chưa từng cấp thành công và cũng không phải lần thử trước của chính nó → từ chối, tránh chiếm DB khác)
            await using (var master = new SqlConnection(Admin("master").ConnectionString))
            {
                await master.OpenAsync();
                var exists = (int)(await Exec(master, "SELECT COUNT(1) FROM sys.databases WHERE name=@n", ("@n", dbName)))! > 0;
                if (exists && c.ProvisionStatus != ProvisionStatus.Failed && c.DatabaseName != dbName)
                    throw new InvalidOperationException($"Database '{dbName}' đã tồn tại và không thuộc công ty này.");
                if (!exists) await Exec(master, $"CREATE DATABASE [{dbName}]");

                var loginExists = (int)(await Exec(master, "SELECT COUNT(1) FROM sys.server_principals WHERE name=@n", ("@n", login)))! > 0;
                if (loginExists) await Exec(master, $"ALTER LOGIN [{login}] WITH PASSWORD='{loginPw}', CHECK_POLICY=OFF");
                else await Exec(master, $"CREATE LOGIN [{login}] WITH PASSWORD='{loginPw}', CHECK_POLICY=OFF, DEFAULT_DATABASE=[{dbName}]");
            }

            // 2) Gán quyền db_owner chỉ trong DB này
            await using (var tdb = new SqlConnection(Admin(dbName).ConnectionString))
            {
                await tdb.OpenAsync();
                var userExists = (int)(await Exec(tdb, "SELECT COUNT(1) FROM sys.database_principals WHERE name=@n", ("@n", login)))! > 0;
                if (!userExists) await Exec(tdb, $"CREATE USER [{login}] FOR LOGIN [{login}]");
                await Exec(tdb, $"ALTER ROLE db_owner ADD MEMBER [{login}]");
            }

            // 3) Migration + dữ liệu khởi tạo (dùng quyền admin để migrate, rồi khách chỉ dùng login riêng)
            var tenantAdminCs = Admin(dbName).ConnectionString;
            await MigrateAndSeedAsync(tenantAdminCs, c, reg);

            var tenantCs = new SqlConnectionStringBuilder(Admin(dbName).ConnectionString) { UserID = login, Password = loginPw }.ConnectionString;
            c.DatabaseName = dbName;
            c.ConnectionStringEnc = _secret.Protect(tenantCs);
            c.ProvisionStatus = ProvisionStatus.Provisioned;
            c.ProvisionError = null;
            c.ProvisionedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await RefreshStorageAsync(c);
            await _audit.LogAsync("Provisioned", "Company", c.Id.ToString(), $"DB {dbName}");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Provision lỗi cho {Code}", c.Code);
            c.ProvisionStatus = ProvisionStatus.Failed;
            c.ProvisionError = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("ProvisionFailed", "Company", c.Id.ToString(), c.ProvisionError);
            return false;
        }
    }

    private async Task MigrateAndSeedAsync(string cs, Company c, Registration? reg)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs, s => s.CommandTimeout(300)));
        services.AddIdentityCore<AppUser>(o => { o.User.RequireUniqueEmail = false; })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>();
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { "Admin", "Inspector", "User", "Customer", "QAQCManager", "Vendor", "Management", "CustomRole" })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        if (reg == null) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(reg.AdminEmail) != null) return;
        var admin = new AppUser
        {
            UserName = reg.AdminEmail, Email = reg.AdminEmail, FullName = reg.AdminName,
            IsActive = true, EmailConfirmed = true, LockoutEnabled = true,
            PasswordHash = reg.AdminPasswordHash,      // đã băm từ lúc đăng ký — không bao giờ giữ mật khẩu gốc
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        var res = await users.CreateAsync(admin);
        if (!res.Succeeded) throw new InvalidOperationException("Không tạo được tài khoản Admin: " + string.Join("; ", res.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(admin, "Admin");
    }

    /// <summary>Đo dung lượng thật của DB khách (data + log) từ sys.master_files.</summary>
    public async Task<double?> RefreshStorageAsync(Company c)
    {
        if (string.IsNullOrEmpty(c.DatabaseName) || !Enabled) return null;
        try
        {
            await using var cn = new SqlConnection(Admin("master").ConnectionString);
            await cn.OpenAsync();
            var pages = await Exec(cn, "SELECT ISNULL(SUM(CAST(size AS bigint)),0) FROM sys.master_files WHERE database_id = DB_ID(@n)", ("@n", c.DatabaseName));
            var gb = Math.Round(Convert.ToInt64(pages) * 8d / 1024 / 1024, 3);
            // chỉ đo được DB ở đây; file upload do API đo (UsageWorker) → giữ phần file đã đo
            c.DbSizeGb = gb; c.StorageUsedGb = gb + c.FileSizeGb; c.StorageMeasuredAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return gb;
        }
        catch (Exception ex) { _log.LogWarning(ex, "Đo dung lượng lỗi {Code}", c.Code); return null; }
    }

    /// <summary>Xóa hẳn DB + login của khách (chỉ dùng cho khách do chính portal cấp; hành động không thể hoàn tác).</summary>
    public async Task DropTenantAsync(Company c)
    {
        if (string.IsNullOrEmpty(c.DatabaseName) || !Enabled) return;
        var prefix = _cfg["Provisioning:DatabasePrefix"] ?? "InticooInspection_";
        if (!c.DatabaseName.StartsWith(prefix) || !SafeName.IsMatch(c.DatabaseName[prefix.Length..]))
            throw new InvalidOperationException("Tên database không do portal cấp — từ chối xóa.");
        await using var cn = new SqlConnection(Admin("master").ConnectionString);
        await cn.OpenAsync();
        await Exec(cn, $"IF DB_ID(N'{c.DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{c.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{c.DatabaseName}]; END");
        var login = "tn_" + c.Code;
        await Exec(cn, $"IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'{login}') DROP LOGIN [{login}]");
        c.DatabaseName = null; c.ConnectionStringEnc = null; c.ProvisionStatus = ProvisionStatus.None; c.StorageUsedGb = 0;
        await _db.SaveChangesAsync();
    }

    private static async Task<object?> Exec(SqlConnection cn, string sql, params (string, object)[] ps)
    {
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 300 };
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        return await cmd.ExecuteScalarAsync();
    }

    private static string RandomPassword(int len)
    {
        const string chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var sb = new System.Text.StringBuilder("Aa1");   // đảm bảo đủ loại ký tự
        while (sb.Length < len) sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
        return sb.ToString();
    }
}

/// <summary>Thao tác trực tiếp trên DB riêng của một khách (đồng bộ người dùng, khóa/mở, đặt lại mật khẩu).</summary>
public class TenantAccess
{
    private readonly PortalDbContext _db;
    private readonly SecretProtector _secret;
    private readonly ILogger<TenantAccess> _log;
    public TenantAccess(PortalDbContext db, SecretProtector secret, ILogger<TenantAccess> log) { _db = db; _secret = secret; _log = log; }

    private ServiceProvider Build(Company c)
    {
        var cs = _secret.Unprotect(c.ConnectionStringEnc!);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs));
        services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        return services.BuildServiceProvider();
    }

    public bool HasDatabase(Company c) => c.ProvisionStatus == ProvisionStatus.Provisioned && !string.IsNullOrEmpty(c.ConnectionStringEnc);

    public async Task<int> SyncUsersAsync(Company c)
    {
        if (!HasDatabase(c)) return 0;
        await using var sp = Build(c);
        await using var scope = sp.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var list = await users.Users.AsNoTracking().ToListAsync();
        var existing = await _db.CompanyUsers.Where(u => u.CompanyId == c.Id).ToListAsync();
        foreach (var u in list)
        {
            var email = (u.Email ?? u.UserName ?? "").ToLowerInvariant();
            var row = existing.FirstOrDefault(x => x.Email.ToLower() == email);
            var role = (await users.GetRolesAsync(u)).FirstOrDefault() ?? "User";
            if (row == null) { row = new CompanyUser { CompanyId = c.Id, Email = email, CreatedAt = DateTime.UtcNow }; _db.CompanyUsers.Add(row); }
            row.UserName = u.UserName;
            row.FullName = string.IsNullOrWhiteSpace(u.FullName) ? email : u.FullName;
            row.Role = role == "Admin" ? "Administrator" : role;
            row.Status = u.IsActive ? "Active" : "Inactive";
            row.Phone = u.PhoneNumber ?? row.Phone;
        }
        await _db.SaveChangesAsync();
        return list.Count;
    }

    public async Task SetActiveAsync(Company c, string email, bool active)
    {
        await using var sp = Build(c);
        await using var scope = sp.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var u = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("User not found in the company database.");
        u.IsActive = active;
        var r = await users.UpdateAsync(u);
        if (!r.Succeeded) throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description)));
        await users.UpdateSecurityStampAsync(u);     // vô hiệu hóa phiên đang đăng nhập
    }

    public async Task<string> ResetPasswordAsync(Company c, string email)
    {
        await using var sp = Build(c);
        await using var scope = sp.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var u = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("User not found in the company database.");
        var temp = "Tmp-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)) + "a!";
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        u.PasswordHash = hasher.HashPassword(u, temp);
        var r = await users.UpdateAsync(u);
        if (!r.Succeeded) throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description)));
        await users.UpdateSecurityStampAsync(u);
        return temp;
    }
}
