using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services.Tenancy
{
    /// <summary>
    /// Đọc claim <c>inticoo_company</c> (Id công ty) trong JWT đã kiểm chữ ký và đặt <see cref="TenantContext"/> cho request.
    /// Token không có tid = DB gốc. Công ty bị Pause/Cancel hoặc không còn DB → 401 để Client đăng xuất.
    /// </summary>
    public sealed class TenantMiddleware
    {
        // KHÔNG dùng "tid": JwtSecurityTokenHandler tự đổi "tid" thành claim tenantid của Azure AD khi đọc token
        public const string Claim = "inticoo_company";
        private readonly RequestDelegate _next;
        public TenantMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext ctx, TenantContext tenant, TenantDirectory dir, IConfiguration cfg)
        {
            var principal = UserAccess.ValidateBearer(ctx, cfg);
            var tid = principal?.FindFirst(Claim)?.Value;
            if (!string.IsNullOrEmpty(tid))
            {
                if (!Guid.TryParse(tid, out var id)) { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
                var t = await dir.GetAsync(id, ctx.RequestAborted);
                // Cache (2 phút) báo bị khóa → đọc lại DB Super: công ty có thể vừa được Resume ở Portal
                if (t == null || !TenantDirectory.CanUse(t.Status)) t = await dir.GetAsync(id, ctx.RequestAborted, fresh: true);
                if (t == null || !TenantDirectory.CanUse(t.Status))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Your company account is not active. Please contact Inticoo support." });
                    return;
                }
                tenant.Set(t);
            }
            await _next(ctx);
        }
    }

    public static class TenantMaintenance
    {
        public static readonly string[] Roles = { "Admin", "Inspector", "User", "Customer", "QAQCManager", "Vendor", "Management", "CustomRole" };

        /// <summary>Tạo scope DI đã gắn sẵn tenant (dùng cho worker / tác vụ nền).</summary>
        public static AsyncServiceScope CreateScope(IServiceProvider root, TenantInfo? tenant)
        {
            var scope = root.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
            return scope;
        }

        /// <summary>Danh sách tenant cần chạy tác vụ nền: DB gốc (null) + mọi công ty đang dùng được.</summary>
        public static async Task<List<TenantInfo?>> AllAsync(IServiceProvider root, CancellationToken ct = default)
        {
            var list = new List<TenantInfo?> { null };
            var dir = root.GetRequiredService<TenantDirectory>();
            try { list.AddRange((await dir.ListAsync(ct)).Where(t => TenantDirectory.CanUse(t.Status))); }
            catch (Exception ex) { root.GetRequiredService<ILogger<TenantDirectory>>().LogError(ex, "Không đọc được danh sách công ty từ DB Super"); }
            return list;
        }

        public static async Task MigrateAllAsync(IServiceProvider root)
        {
            var log = root.GetRequiredService<ILogger<TenantDirectory>>();
            var dir = root.GetRequiredService<TenantDirectory>();
            if (!dir.Enabled) return;
            List<TenantInfo> tenants;
            try { tenants = await dir.ListAsync(); }
            catch (Exception ex) { log.LogError(ex, "Không đọc được danh sách công ty từ DB Super — bỏ qua migrate DB khách"); return; }

            foreach (var t in tenants)
            {
                try
                {
                    await using var scope = CreateScope(root, t);
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.MigrateAsync();
                    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    foreach (var r in Roles)
                        if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));
                    log.LogInformation("Đã migrate DB công ty {Code}", t.Code);
                }
                catch (Exception ex) { log.LogError(ex, "Migrate DB công ty {Code} lỗi", t.Code); }
            }
        }
    }
}
