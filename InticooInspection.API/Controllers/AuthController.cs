using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using InticooInspection.API.Services;
using InticooInspection.API.Services.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly TenantDirectory _tenants;

        public AuthController(UserManager<AppUser> userManager, IConfiguration config, TenantDirectory tenants)
        {
            _userManager = userManager;
            _config = config;
            _tenants = tenants;
        }

        [HttpPost("login")]
        [PublicApi]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "Request body is null." });

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { success = false, message = "Username and password are required." });

            // Mỗi công ty khách có DB riêng: tìm các DB có tài khoản này (theo email/username trong DB Super),
            // luôn kèm DB gốc. Thử mật khẩu ở từng DB; chỉ hỏi chọn công ty khi mật khẩu đúng ở nhiều nơi
            // (không lộ việc email thuộc công ty nào cho người không biết mật khẩu).
            var candidates = new List<TenantInfo?>();
            var tenants = await _tenants.FindByLoginAsync(request.Username);
            if (!string.IsNullOrWhiteSpace(request.CompanyCode))
            {
                var code = request.CompanyCode.Trim();
                candidates.AddRange(tenants.Where(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase)));
                if (code.Equals(DefaultCompanyCode, StringComparison.OrdinalIgnoreCase)) candidates.Add(null);
            }
            else
            {
                candidates.AddRange(tenants);
                candidates.Add(null);   // DB gốc
            }

            var attempts = new List<LoginAttempt>();
            foreach (var t in candidates)
            {
                var a = await TryLoginAsync(t, request);
                if (a.Outcome != LoginOutcome.NoUser) attempts.Add(a);
            }

            var ok = attempts.Where(a => a.Outcome == LoginOutcome.Success).ToList();
            if (ok.Count > 1)
                return Ok(new
                {
                    success = false, needCompany = true,
                    message = "This account exists in more than one company. Please choose your company.",
                    companies = ok.Select(a => new { code = a.Tenant?.Code ?? DefaultCompanyCode, name = a.Tenant?.Name ?? "Inticoo" })
                });
            if (ok.Count == 1)
            {
                var a = ok[0];
                if (a.Tenant != null && !TenantDirectory.CanUse(a.Tenant.Status))
                    return Unauthorized(new { success = false, message = "Your company account is not active. Please contact Inticoo support." });
                if (a.Tenant != null) await _tenants.TouchLoginAsync(a.Tenant, a.Email);
                var token = GenerateJwtToken(a.UserId!, a.Email, a.UserName, a.Role!, request.RememberMe, a.Tenant);
                return Ok(new
                {
                    success = true,
                    token   = token,
                    company = a.Tenant == null ? null : new { code = a.Tenant.Code, name = a.Tenant.Name },
                    user    = new
                    {
                        id       = a.UserId,
                        username = a.UserName,
                        email    = a.Email,
                        fullName = a.FullName,
                        role     = a.Role,
                        language = a.Language
                    }
                });
            }
            // Sai mật khẩu ở mọi DB → mới tính 1 lần sai (đúng ở DB khác thì không phạt tài khoản trùng email)
            for (var i = 0; i < attempts.Count; i++)
                if (attempts[i].Outcome == LoginOutcome.WrongPassword) attempts[i] = await RecordFailureAsync(attempts[i]);

            // Không DB nào đúng mật khẩu: tài khoản chỉ có ở 1 DB → trả đúng thông báo của DB đó (khóa / còn N lần thử)
            if (attempts.Count == 1)
                return Unauthorized(new { success = false, message = attempts[0].Message });
            if (attempts.Count > 1 && attempts.All(a => a.Outcome == LoginOutcome.Locked))
                return Unauthorized(new { success = false, message = "LOCKED" });
            return Unauthorized(new { success = false, message = "Invalid username or password." });
        }

        /// <summary>Mã công ty đại diện DB gốc khi người dùng phải chọn công ty.</summary>
        public const string DefaultCompanyCode = "inticoo";

        private enum LoginOutcome { NoUser, Locked, WrongPassword, Success }
        private sealed record LoginAttempt(TenantInfo? Tenant, LoginOutcome Outcome, string Message,
            string? UserId = null, string? UserName = null, string? Email = null, string? FullName = null, string? Role = null, string? Language = null);

        /// <summary>Kiểm tra đăng nhập trong DB của 1 công ty (scope DI riêng gắn tenant đó).</summary>
        private async Task<LoginAttempt> TryLoginAsync(TenantInfo? tenant, LoginRequest request)
        {
            await using var scope = TenantMaintenance.CreateScope(HttpContext.RequestServices, tenant);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var user = await users.FindByNameAsync(request.Username)
                    ?? await users.FindByEmailAsync(request.Username);
            if (user == null)
                return new(tenant, LoginOutcome.NoUser, "Invalid username or password.");

            // Check lockout TRUOC IsActive
            if (await users.IsLockedOutAsync(user) || !user.IsActive)
                return new(tenant, LoginOutcome.Locked, "LOCKED");

            if (!await users.CheckPasswordAsync(user, request.Password))
                return new(tenant, LoginOutcome.WrongPassword, "Invalid username or password.", user.Id);

            await users.ResetAccessFailedCountAsync(user);
            user.LastLoginAt = DateTime.UtcNow;
            // Ngôn ngữ người dùng chủ động chọn ở màn Login → lưu vào tài khoản
            var uiLang = NormalizeUiLanguage(request.Language);
            if (uiLang != null) user.UiLanguage = uiLang;
            await users.UpdateAsync(user);

            var roles = await users.GetRolesAsync(user);
            return new(tenant, LoginOutcome.Success, "", user.Id, user.UserName, user.Email, user.FullName,
                roles.FirstOrDefault() ?? "User", user.UiLanguage);
        }

        private async Task<LoginAttempt> RecordFailureAsync(LoginAttempt a)
        {
            await using var scope = TenantMaintenance.CreateScope(HttpContext.RequestServices, a.Tenant);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByIdAsync(a.UserId!);
            if (user == null) return a;
            await users.AccessFailedAsync(user);

            // Khóa tạm do Identity quản lý (Lockout) — KHÔNG tắt IsActive vĩnh viễn, tránh bị lợi dụng khóa tài khoản người khác
            if (await users.IsLockedOutAsync(user))
                return a with { Outcome = LoginOutcome.Locked, Message = "LOCKED" };

            var failCount = await users.GetAccessFailedCountAsync(user);
            var remaining = Math.Max(0, users.Options.Lockout.MaxFailedAccessAttempts - failCount);
            return a with { Message = "Invalid username or password. " + (remaining > 0 ? $"{remaining} attempt(s) remaining." : "Account will be locked on next attempt.") };
        }

        [HttpPost("logout")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.LastLogoutAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }
            }
            return Ok(new { success = true });
        }

        internal static readonly string[] UiLanguages = { "en", "vi", "zh", "ms" };

        internal static string? NormalizeUiLanguage(string? lang)
        {
            lang = lang?.Trim().ToLowerInvariant();
            return !string.IsNullOrEmpty(lang) && UiLanguages.Contains(lang) ? lang : null;
        }

        private string GenerateJwtToken(string userId, string? email, string? userName, string role, bool rememberMe, TenantInfo? tenant)
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(8);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub,   userId),
                new Claim(JwtRegisteredClaimNames.Email, email ?? ""),
                new Claim("username",                    userName ?? ""),
                new Claim(ClaimTypes.Name,               userName ?? ""),
                new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", role),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };
            // Công ty (DB riêng) của người dùng — TenantMiddleware đọc claim này để nối đúng DB
            if (tenant != null) claims.Add(new Claim(TenantMiddleware.Claim, tenant.Id.ToString()));

            var token = new JwtSecurityToken(
                issuer:             _config["Jwt:Issuer"],
                audience:           _config["Jwt:Audience"],
                claims:             claims,
                expires:            expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Username   { get; set; } = "";
        public string Password   { get; set; } = "";
        public bool   RememberMe { get; set; }
        // Chỉ gửi khi người dùng chủ động chọn ngôn ngữ ở màn Login (null = giữ ngôn ngữ đã lưu của tài khoản)
        public string? Language  { get; set; }
        // Chỉ gửi khi API trả needCompany (tài khoản có ở nhiều công ty) và người dùng đã chọn công ty
        public string? CompanyCode { get; set; }
    }
}