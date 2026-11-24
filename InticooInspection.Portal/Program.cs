using InticooInspection.Infrastructure.Tenancy;
using System.Security.Claims;
using System.Threading.RateLimiting;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Secret (chuỗi kết nối DB Super, khóa mã hóa, mật khẩu super admin) nằm ngoài repo — xem appsettings.json
builder.Configuration
    .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Secrets.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Super")))
    throw new InvalidOperationException("Thiếu ConnectionStrings:Super (DB InticooInspectionSupper). Đặt trong appsettings.Secrets.json hoặc biến môi trường ConnectionStrings__Super.");

builder.Services.AddDbContext<PortalDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Super"),
        s => s.MigrationsHistoryTable("__EFMigrationsHistory", PortalDbContext.Schema)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SecretProtector>();
builder.Services.AddSingleton<LocalizationStore>();
builder.Services.AddScoped<Loc>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<IPaymentGateway, TestPaymentGateway>();
builder.Services.AddHttpClient<PayPalClient>();        // PayPal Checkout (PayPal:Mode/ClientId/Secret)
builder.Services.AddScoped<PayPalCheckoutService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddScoped<TenantAccess>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddHostedService<OverdueWorker>();
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "inticoo.super";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // HTTPS ở production (IIS/proxy) → cookie Secure
        o.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        o.SlidingExpiration = true;
        o.LoginPath = "/admin/login";
        o.AccessDeniedPath = "/admin/login";
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

builder.Services.AddAntiforgery(o => { o.Cookie.Name = "inticoo.af"; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    RateLimitPartition<string> Part(HttpContext c, int permit, int minutes) => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromMinutes(minutes), QueueLimit = 0 });
    o.AddPolicy("login", c => Part(c, 10, 1));
    o.AddPolicy("register", c => Part(c, 30, 10));
    o.AddPolicy("contact", c => Part(c, 5, 10));
    o.AddPolicy("paypal", c => Part(c, 60, 10));   // link quay về từ PayPal: bị chặn = khách đã duyệt nhưng chưa thu được tiền → nới hơn "register"
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeAreaFolder("Admin", "/", "SuperAdmin");
    o.Conventions.AllowAnonymousToAreaPage("Admin", "/Login");
    o.Conventions.AllowAnonymousToAreaPage("Admin", "/Logout");
});
builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();
// Kiểm thử với PayPal giả lập (PayPal:ApiBase): cho phép form chuyển hướng sang trang duyệt giả
var payPalTestOrigin = Uri.TryCreate(app.Configuration["PayPal:ApiBase"], UriKind.Absolute, out var ppBase) ? " " + ppBase.GetLeftPart(UriPartial.Authority) : "";

// Tạo/cập nhật schema sup + dữ liệu khởi tạo
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(scope.ServiceProvider);
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/error"); app.UseHsts(); }
app.UseStatusCodePagesWithReExecute("/error", "?code={0}");

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none'; form-action 'self' https://www.paypal.com https://www.sandbox.paypal.com" + payPalTestOrigin + "; base-uri 'self'";
    if (ctx.Request.Path.StartsWithSegments("/admin")) h["Cache-Control"] = "no-store";
    await next();
});

// Chế độ bảo trì: chặn website công khai, vẫn cho vào /admin
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path;
    if (!p.StartsWithSegments("/admin") && !p.StartsWithSegments("/css") && !p.StartsWithSegments("/js") && !p.StartsWithSegments("/img") && !p.StartsWithSegments("/error"))
    {
        var cache = ctx.RequestServices.GetRequiredService<IMemoryCache>();
        var on = await cache.GetOrCreateAsync("maintenance", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20);
            return await ctx.RequestServices.GetRequiredService<SettingsService>().GetBoolAsync("system.maintenance");
        });
        if (on)
        {
            ctx.Response.StatusCode = 503;
            ctx.Response.Headers.RetryAfter = "3600";
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync("<!doctype html><meta charset=utf-8><title>Maintenance</title><body style=\"font-family:sans-serif;text-align:center;padding:80px\"><h1>Inticoo is under maintenance</h1><p>We will be back shortly.</p>");
            return;
        }
    }
    await next();
});

// Ghi nhớ ngôn ngữ người dùng chọn (?lang=xx) vào cookie
app.Use(async (ctx, next) =>
{
    var l = ctx.Request.Query["lang"].ToString();
    if (LocalizationStore.Supported.Contains(l))
        ctx.Response.Cookies.Append("lang", l, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// PayPal quay về sau khi khách duyệt / hủy (?token=<order id>&PayerID=...)
app.MapGet("/pay/paypal/return", async (string? token, PayPalCheckoutService pp, PortalDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(token)) return Results.Redirect("/");
    var (companyId, _) = await pp.CompleteAsync(token);
    return await BackToRegistration(db, companyId);
}).RequireRateLimiting("paypal");
app.MapGet("/pay/paypal/cancel", async (string? token, PayPalCheckoutService pp, PortalDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(token)) return Results.Redirect("/");
    return await BackToRegistration(db, await pp.CancelAsync(token));
}).RequireRateLimiting("paypal");

app.MapRazorPages();
app.Run();

static async Task<IResult> BackToRegistration(PortalDbContext db, Guid? companyId)
{
    if (companyId == null) return Results.Redirect("/");
    var regId = await db.Registrations.Where(r => r.CompanyId == companyId).Select(r => (Guid?)r.Id).FirstOrDefaultAsync();
    return Results.Redirect(regId == null ? "/" : $"/register/complete/{regId}");
}

