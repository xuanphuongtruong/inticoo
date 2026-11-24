using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InticooInspection.Domain.Entities;
using InticooInspection.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Ngữ cảnh quyền của người gọi API (theo request).
    /// Middleware JwtBearer không xác thực được token (xem MeController) nên tự validate JWT ở đây,
    /// rồi tính quyền module + phạm vi khách hàng từ AppUser.
    /// </summary>
    public sealed class AccessContext
    {
        public AppUser User { get; init; } = null!;
        public IList<string> Roles { get; init; } = new List<string>();
        public bool IsAdmin => Roles.Contains("Admin");
        public ModulePermissionSet Perms { get; init; } = new();
        /// <summary>null = không giới hạn khách hàng.</summary>
        public HashSet<string>? CustomerScope { get; init; }

        public bool CanView(string module) => IsAdmin || Perms.CanView(module);
        public bool Can(string module, string action) => IsAdmin || Perms.Can(module, action);
        public bool AllowsCustomer(string? customerId) =>
            CustomerScope == null || (!string.IsNullOrEmpty(customerId) && CustomerScope.Contains(customerId));
    }

    public sealed class UserAccess
    {
        private readonly IHttpContextAccessor _http;
        private readonly UserManager<AppUser> _users;
        private readonly IConfiguration _cfg;
        private bool _resolved;
        private AccessContext? _ctx;

        public UserAccess(IHttpContextAccessor http, UserManager<AppUser> users, IConfiguration cfg)
        {
            _http = http; _users = users; _cfg = cfg;
        }

        /// <summary>Người gọi hiện tại (null = chưa đăng nhập / token không hợp lệ).</summary>
        public async Task<AccessContext?> GetAsync()
        {
            if (_resolved) return _ctx;
            _resolved = true;

            var principal = ValidateBearer(_http.HttpContext, _cfg);
            if (principal == null) return null;
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            var user = await _users.FindByIdAsync(userId);
            if (user == null || !user.IsActive) return null;
            var roles = await _users.GetRolesAsync(user);
            _ctx = Build(user, roles);
            return _ctx;
        }

        /// <summary>Kiểm tra JWT trong header Authorization (chữ ký, issuer, audience, hạn). null = không có / không hợp lệ.</summary>
        public static ClaimsPrincipal? ValidateBearer(HttpContext? http, IConfiguration cfg)
        {
            var header = http?.Request.Headers["Authorization"].ToString() ?? "";
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                var prms = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                    ValidIssuer = cfg["Jwt:Issuer"], ValidAudience = cfg["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
                return new JwtSecurityTokenHandler().ValidateToken(header["Bearer ".Length..].Trim(), prms, out _);
            }
            catch { return null; }
        }

        public static AccessContext Build(AppUser user, IList<string> roles) => new()
        {
            User = user,
            Roles = roles,
            Perms = ModulePermissionSet.Effective(user.ModulePermissions, user.PageAccess),
            CustomerScope = roles.Contains("Admin") ? null
                : CustomerAccess.Scope(user.CustomerIds, user.CustomerId, roles.Contains("Customer")),
        };

        /// <summary>Phạm vi khách hàng của người gọi (null = không giới hạn, kể cả khi chưa đăng nhập — giữ hành vi cũ cho GET).</summary>
        public async Task<HashSet<string>?> CustomerScopeAsync() => (await GetAsync())?.CustomerScope;

        /// <summary>
        /// Kiểm tra quyền ghi: trả null nếu được phép, ngược lại trả 401/403 để controller return ngay.
        /// Được phép khi là Admin hoặc có ÍT NHẤT 1 trong các cặp (module, action).
        /// </summary>
        public async Task<IActionResult?> RequireAsync(params (string Module, string Action)[] anyOf)
        {
            var ctx = await GetAsync();
            if (ctx == null) return new UnauthorizedObjectResult(new { success = false, message = "Please sign in again." });
            if (ctx.IsAdmin || anyOf.Any(p => ctx.Perms.Can(p.Module, p.Action))) return null;
            return Forbidden();
        }

        /// <summary>Chỉ Admin.</summary>
        public async Task<IActionResult?> RequireAdminAsync()
        {
            var ctx = await GetAsync();
            if (ctx == null) return new UnauthorizedObjectResult(new { success = false, message = "Please sign in again." });
            return ctx.IsAdmin ? null : Forbidden();
        }

        static IActionResult Forbidden() =>
            new ObjectResult(new { success = false, message = "You do not have permission to perform this action." }) { StatusCode = 403 };
    }
}
