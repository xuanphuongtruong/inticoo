using InticooInspection.Infrastructure.Tenancy;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal;

/// <summary>Cấu hình chung cho 2 app InticooInspection.Website và InticooInspection.SuperAdmin.</summary>
public static class PortalHosting
{
    /// <summary>Nạp file secret (chuỗi kết nối DB Super, khóa mã hóa...) — nằm ngoài repo, không publish.
    /// Biến môi trường + tham số dòng lệnh nạp lại sau cùng để luôn được ưu tiên hơn file secret.</summary>
    public static void AddPortalSecrets(this WebApplicationBuilder builder, string[] args)
    {
        builder.Configuration
            .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Secrets.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Super")))
            throw new InvalidOperationException("Thiếu ConnectionStrings:Super (DB InticooInspectionSupper). Đặt trong appsettings.Secrets.json hoặc biến môi trường ConnectionStrings__Super.");
    }

    /// <summary>DB Super + các service nghiệp vụ dùng chung (thanh toán, PayPal, cấp DB cho khách, đăng ký).</summary>
    public static IServiceCollection AddPortalCore(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<PortalDbContext>(o =>
            o.UseSqlServer(cfg.GetConnectionString("Super"),
                s => s.MigrationsHistoryTable("__EFMigrationsHistory", PortalDbContext.Schema)));
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<SecretProtector>();
        services.AddSingleton<PasswordService>();
        services.AddSingleton<ContentStore>();       // file trang Resources (thư mục chung Content:Root)
        services.AddSingleton<IPaymentGateway, TestPaymentGateway>();
        services.AddHttpClient<PayPalClient>();        // PayPal Checkout (PayPal:Mode/ClientId/Secret)
        services.AddScoped<PayPalCheckoutService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<AuditService>();
        services.AddScoped<ProvisioningService>();
        services.AddScoped<TenantAccess>();
        services.AddScoped<BillingService>();
        services.AddScoped<RegistrationService>();
        return services;
    }

    /// <summary>Ghi log DB Super đang dùng (server + tên DB, không ghi mật khẩu) — tránh chạy thử nhầm DB thật.</summary>
    public static void LogSuperDatabase(this WebApplication app)
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(app.Configuration.GetConnectionString("Super"));
        app.Logger.LogInformation("DB Super: {Server} / {Database}", b.DataSource, b.InitialCatalog);
    }

    /// <summary>Header bảo mật chung. formAction: thêm nguồn cho phép submit form (PayPal...).</summary>
    public static void UseSecurityHeaders(this WebApplication app, string formAction = "'self'", bool noStore = false, string frameSrc = "'none'")
    {
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none'; frame-src " + frameSrc + "; form-action " + formAction + "; base-uri 'self'";
            // đặt lúc gửi header và chỉ khi chưa có — trang có form antiforgery tự đặt "no-cache, no-store" (đặt trước sẽ bị log cảnh báo)
            if (noStore) ctx.Response.OnStarting(() => { if (!ctx.Response.Headers.ContainsKey("Cache-Control")) ctx.Response.Headers.CacheControl = "no-store"; return Task.CompletedTask; });
            await next();
        });
    }
}
