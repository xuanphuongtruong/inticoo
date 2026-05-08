using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InticooInspection.API.Controllers
{
    /// <summary>
    /// Standalone controller for the current user's context endpoints.
    ///
    /// IMPORTANT: Class-level [AllowAnonymous] vi [Authorize] thuan tuy
    /// dang bi xung dot voi cookie scheme cua AddIdentity tren Azure
    /// (du da cau hinh JWT). De fix dut diem, controller nay tu doc
    /// va validate JWT token tu Authorization header.
    /// </summary>
    [ApiController]
    [Route("api/me")]
    [AllowAnonymous]
    public class MeController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration       _config;

        public MeController(UserManager<AppUser> userManager, IConfiguration config)
        {
            _userManager = userManager;
            _config      = config;
        }

        // GET api/me/page-access
        // Returns the current user's PageAccess CSV and roles.
        // Used by the Blazor PageAccessService to drive menu visibility.
        [HttpGet("page-access")]
        public async Task<IActionResult> GetPageAccess()
        {
            // ── 1. Read Authorization header ──
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Missing Bearer token." });

            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
                return Unauthorized(new { message = "Empty Bearer token." });

            // ── 2. Validate JWT ──
            string? userId = null;
            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = _config["Jwt:Issuer"],
                    ValidAudience            = _config["Jwt:Audience"],
                    IssuerSigningKey         = key,
                    ClockSkew                = TimeSpan.FromMinutes(2)
                };

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, validationParams, out _);

                userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? principal.FindFirst("sub")?.Value;
            }
            catch (SecurityTokenExpiredException)
            {
                return Unauthorized(new { message = "Token expired." });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = $"Invalid token: {ex.Message}" });
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "No user id in token." });

            // ── 3. Load user + roles ──
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { message = "User not found." });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                pageAccess = user.PageAccess ?? "",
                roles      = roles
            });
        }
    }
}
