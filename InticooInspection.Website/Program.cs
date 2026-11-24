using System.Threading.RateLimiting;
using InticooInspection.Portal;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

// InticooInspection.Website — website công khai bán app Inticoo (giới thiệu, bảng giá, liên hệ, đăng ký + thanh toán).
// Super Admin là app riêng (InticooInspection.SuperAdmin). Schema DB do Super Admin migrate; website không migrate.
var builder = WebApplication.CreateBuilder(args);
builder.AddPortalSecrets(args);

builder.Services.AddPortalCore(builder.Configuration);
builder.Services.AddSingleton<LocalizationStore>();
builder.Services.AddScoped<Loc>();

builder.Services.AddAntiforgery(o => { o.Cookie.Name = "inticoo.af"; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    RateLimitPartition<string> Part(HttpContext c, int permit, int minutes) => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromMinutes(minutes), QueueLimit = 0 });
    o.AddPolicy("register", c => Part(c, 30, 10));
    o.AddPolicy("contact", c => Part(c, 5, 10));
    o.AddPolicy("paypal", c => Part(c, 60, 10));   // link quay về từ PayPal: bị chặn = khách đã duyệt nhưng chưa thu được tiền → nới hơn "register"
});

builder.Services.AddRazorPages();
builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();
app.LogSuperDatabase();
// Kiểm thử với PayPal giả lập (PayPal:ApiBase): cho phép form chuyển hướng sang trang duyệt giả
var payPalTestOrigin = Uri.TryCreate(app.Configuration["PayPal:ApiBase"], UriKind.Absolute, out var ppBase) ? " " + ppBase.GetLeftPart(UriPartial.Authority) : "";

// Không migrate ở đây — chỉ cảnh báo nếu DB Super chưa được Super Admin cập nhật
using (var scope = app.Services.CreateScope())
{
    try
    {
        var pending = await scope.ServiceProvider.GetRequiredService<PortalDbContext>().Database.GetPendingMigrationsAsync();
        if (pending.Any()) app.Logger.LogWarning("DB Super còn {N} migration chưa chạy — hãy khởi động InticooInspection.SuperAdmin trước.", pending.Count());
    }
    catch (Exception ex) { app.Logger.LogError(ex, "Không kết nối được DB Super"); }
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/error"); app.UseHsts(); }
app.UseStatusCodePagesWithReExecute("/error", "?code={0}");
// frame-src: video YouTube/Vimeo nhúng trong bài Resources
app.UseSecurityHeaders("'self' https://www.paypal.com https://www.sandbox.paypal.com" + payPalTestOrigin,
    frameSrc: "https://www.youtube-nocookie.com https://player.vimeo.com");

// Chế độ bảo trì (bật ở Super Admin › Settings › System): chặn website, vẫn cho tải css/js/ảnh
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path;
    if (!p.StartsWithSegments("/css") && !p.StartsWithSegments("/js") && !p.StartsWithSegments("/img") && !p.StartsWithSegments("/error"))
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

ContentStore.UseContentFiles(app);   // file upload của Resources (/content/...)
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
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
