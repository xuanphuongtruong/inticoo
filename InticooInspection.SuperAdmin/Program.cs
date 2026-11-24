using System.Security.Claims;
using System.Threading.RateLimiting;
using InticooInspection.Portal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// InticooInspection.SuperAdmin — khu quản trị của chủ nền tảng (công ty, thuê bao, hóa đơn, thanh toán...).
// Tách riêng khỏi website công khai (InticooInspection.Website); hai app dùng chung DB InticooInspectionSupper qua Portal.Core.
var builder = WebApplication.CreateBuilder(args);
builder.AddPortalSecrets(args);

builder.Services.AddPortalCore(builder.Configuration);
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<UsageReportService>();   // dung lượng / người dùng / hoạt động (số đo do API ghi)
builder.Services.AddHostedService<OverdueWorker>();   // hóa đơn quá hạn → Overdue (chỉ chạy ở app này)

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "inticoo.super";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // HTTPS ở production (IIS/proxy) → cookie Secure
        o.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        o.SlidingExpiration = true;
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/login";
        o.Events.OnValidatePrincipal = async ctx =>
        {
            // Mỗi request kiểm tra tài khoản còn hoạt động và security stamp khớp (đổi mật khẩu/khóa → đăng xuất ngay)
            var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = ctx.Principal?.FindFirstValue("stamp");
            var db = ctx.HttpContext.RequestServices.GetRequiredService<PortalDbContext>();
            var ok = int.TryParse(id, out var uid) && await db.SuperAdmins.AsNoTracking()
                .AnyAsync(a => a.Id == uid && a.IsActive && a.SecurityStamp == stamp);
            if (!ok) { ctx.RejectPrincipal(); await ctx.HttpContext.SignOutAsync(); }
        };
    });
builder.Services.AddAuthorization(o => o.AddPolicy("SuperAdmin", p => p.RequireAuthenticatedUser().RequireClaim("role", "SuperAdmin")));

builder.Services.AddAntiforgery(o => { o.Cookie.Name = "inticoo.super.af"; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/", "SuperAdmin");
    o.Conventions.AllowAnonymousToPage("/Login");
    o.Conventions.AllowAnonymousToPage("/Logout");
    o.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();
app.LogSuperDatabase();

// App này là chủ DB Super: tạo/cập nhật schema sup + dữ liệu khởi tạo (gói, module, super admin đầu tiên)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(scope.ServiceProvider);
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/error"); app.UseHsts(); }
app.UseStatusCodePagesWithReExecute("/error", "?code={0}");
app.UseSecurityHeaders(noStore: true);

ContentStore.UseContentFiles(app);   // file upload của Resources (/content/...)
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
