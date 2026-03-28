using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 6;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase       = true;
    options.Lockout.AllowedForNewUsers      = true;
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromDays(999);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
        policy.SetIsOriginAllowed(origin =>
        {
            // Cho phép tất cả localhost (mọi port) trong môi trường dev
            var uri = new Uri(origin);
            return uri.Host == "localhost" || uri.Host == "127.0.0.1" ||
                   uri.Host.EndsWith("azurewebsites.net") ||
                   uri.Host.EndsWith("azurestaticapps.net");
        })
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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(userManager, roleManager);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "InticooInspection API v1");
    c.RoutePrefix = "swagger";
});

// ── Middleware order (quan trọng) ──────────────────────────────
// 1. Static files phải đứng TRƯỚC UseCors để browser nhận được
//    header Access-Control trên file ảnh khi Blazor WebAssembly gọi cross-origin

// Tạo thư mục wwwroot/uploads nếu chưa có
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads", "photos"));
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads", "references"));
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads", "cv"));

// Static files — phải đứng trước UseCors và UseHttpsRedirection
app.UseStaticFiles(); // serve wwwroot/ → URL /uploads/photos/...

// Explicit fallback cho uploads nếu WebRootPath bị null (trường hợp IIS/hosting)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads")),
    RequestPath = "/uploads"
});

app.UseHttpsRedirection();

// CORS sau static files
app.UseCors("BlazorClient");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
