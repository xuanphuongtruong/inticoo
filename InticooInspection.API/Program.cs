using InticooInspection.API.Services;
using InticooInspection.API.Services.Tenancy;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Secret (chuỗi kết nối DB, Jwt:Key, MailSettings:Password) KHÔNG nằm trong appsettings.json (repo).
// Nạp từ appsettings.Secrets.json + appsettings.{Env}.Secrets.json (gitignore, chỉ có trên máy dev/server),
// sau đó nạp lại biến môi trường + tham số dòng lệnh để chúng (Azure App Settings, IIS, chạy thử với DB khác) luôn được ưu tiên.
builder.Configuration
    .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Secrets.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]) || builder.Configuration["Jwt:Key"]!.Length < 32)
    throw new InvalidOperationException("Thiếu Jwt:Key (>= 32 ký tự). Đặt trong appsettings.Secrets.json hoặc biến môi trường Jwt__Key.");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
    throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection. Đặt trong appsettings.Secrets.json hoặc biến môi trường ConnectionStrings__DefaultConnection.");

// Database — mỗi công ty khách (Portal cấp) có DB riêng; TenantContext của request quyết định nối DB nào
// (mặc định = DefaultConnection). Xem Services/Tenancy.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TenantDirectory>();
// Đo sử dụng + hạn mức gói (Super Admin xem; vượt gói → chặn thêm user / upload)
builder.Services.AddSingleton<InticooInspection.API.Services.Usage.SuperUsageStore>();
builder.Services.AddSingleton<InticooInspection.API.Services.Usage.UsageMeter>();
builder.Services.AddScoped<InticooInspection.API.Services.Usage.PlanLimitService>();
builder.Services.AddHostedService<InticooInspection.API.Services.Usage.UsageWorker>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(sp.GetRequiredService<TenantContext>().Freeze()));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 6;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase       = true;
    options.Lockout.AllowedForNewUsers      = true;
    // Khóa TẠM 15 phút sau 5 lần sai (trước đây 3 lần → khóa 999 ngày: ai biết username cũng khóa được tài khoản người khác)
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Ngăn Identity cookie redirect về /Account/Login — trả về 401/403 thuần cho API
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType            = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
    };
});

// CORS — dùng WithOrigins cụ thể, bỏ AllowAnyOrigin (xung đột với SetIsOriginAllowed)
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
        policy
            .WithOrigins(
                "https://inticoo.thuphuc.com",   // Client production (IIS)
                "http://localhost:5186",
                "https://localhost:5186",
                "https://black-grass-002608310.2.azurestaticapps.net",
                "https://lively-cliff-0413efc10.2.azurestaticapps.net",
                "https://gentle-pebble-0ad999710.2.azurestaticapps.net",
                "https://mango-hill-0001b6310.2.azurestaticapps.net"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "Admin"));
});
builder.Services.AddHttpContextAccessor();
// Mọi API bắt buộc đăng nhập, trừ action gắn [PublicApi] (xem Services/RequireSignedInFilter.cs)
builder.Services.AddControllers(o => o.Filters.Add<RequireSignedInFilter>()).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<AzureBlobService>();

// ─────────────────────────────────────────────────────────────────
// Mail service - gửi mail thông báo inspection cho Vendor hàng tuần
// (Mặc định: 18:00 thứ Sáu giờ VN — cấu hình trong appsettings.MailSettings)
// ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IMailConfigProvider, MailConfigProvider>();   // scoped: cấu hình mail theo DB của từng công ty
builder.Services.AddScoped<IInspectionMailService, InspectionMailService>();
builder.Services.AddHostedService<WeeklyMailWorker>();
// Quét mỗi 10 phút: gửi mail completion cho Inspection Done trong ngày có cờ TinhTrangGuiMail = 0
builder.Services.AddHostedService<InspectionDoneMailWorker>();
builder.Services.AddScoped<IPdfService, PuppeteerPdfService>();
builder.Services.AddScoped<UserAccess>();   // quyền module + phạm vi khách hàng theo người gọi

// Giới hạn tần suất đăng nhập / đổi mật khẩu theo IP (chống dò mật khẩu)
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
// Ghi log DB đang dùng (server / tên DB, không có mật khẩu) — tránh chạy thử nhầm DB thật
foreach (var name in new[] { "DefaultConnection", "Super" })
{
    var cs = app.Configuration.GetConnectionString(name);
    if (string.IsNullOrWhiteSpace(cs)) continue;
    try { var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs); app.Logger.LogInformation("DB {Name}: {Server} / {Db}", name, b.DataSource, b.InitialCatalog); }
    catch { }
}

// Seed database
using (var scope = app.Services.CreateScope())
{
    // var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    // var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    // await db.Database.MigrateAsync();
    // await DbSeeder.SeedAsync(userManager, roleManager);
    try
    {
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi khi seed database");
        // Không throw — app vẫn chạy tiếp
    }
}
// DB riêng của các công ty khách: áp migration mới + bảo đảm có đủ role.
// KHÔNG chạy DbSeeder (tài khoản admin@inticoo.com / user@inticoo.com mặc định chỉ dành cho DB gốc).
await TenantMaintenance.MigrateAllAsync(app.Services);

// Trang lỗi chi tiết + Swagger chỉ bật khi Development (production không lộ stack trace / danh sách API)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "InticooInspection API v1");
        c.RoutePrefix = "swagger";
    });
}

// Tạo thư mục wwwroot/uploads nếu chưa có
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads", "photos"));
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads", "references"));
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads", "cv"));
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads", "signatures"));   // chữ ký Defect (file PNG, DB chỉ lưu đường dẫn)

// Static files: wwwroot chỉ chứa uploads (file người dùng tải lên) → chỉ phục vụ các loại file an toàn.
// .html/.svg/.js... trả 404 để không thể dùng upload làm XSS trên domain API; thêm nosniff,
// file không phải ảnh/PDF thì buộc tải về (không mở trực tiếp trên trình duyệt).
var uploadContentTypes = new FileExtensionContentTypeProvider(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    [".jpg"]  = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".gif"] = "image/gif",
    [".webp"] = "image/webp", [".bmp"]  = "image/bmp",
    [".pdf"]  = "application/pdf",
    [".doc"]  = "application/msword",
    [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    [".xls"]  = "application/vnd.ms-excel",
    [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    [".ppt"]  = "application/vnd.ms-powerpoint",
    [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    [".csv"]  = "text/csv", [".txt"] = "text/plain",
    [".zip"]  = "application/zip", [".rar"] = "application/vnd.rar", [".7z"] = "application/x-7z-compressed",
    [".mp4"]  = "video/mp4", [".mov"] = "video/quicktime",
});
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = uploadContentTypes,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        var h = ctx.Context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
        var ct = ctx.Context.Response.ContentType ?? "";
        if (!ct.StartsWith("image/") && ct != "application/pdf" && !ct.StartsWith("video/"))
            h["Content-Disposition"] = "attachment";
    }
});

app.UseHttpsRedirection();

// CORS phải đứng TRƯỚC Authentication/Authorization
app.UseCors("BlazorClient");
app.UseRateLimiter();

// Chọn DB theo công ty của người gọi (claim "tid" trong JWT) — phải chạy trước controller
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<InticooInspection.API.Services.Usage.PlanLimitMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
