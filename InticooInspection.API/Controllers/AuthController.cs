using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        public AuthController(UserManager<AppUser> userManager, IConfiguration config)
        {
            _userManager = userManager;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest(new { success = false, message = "Request body is null." });

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { success = false, message = "Username and password are required." });

            var user = await _userManager.FindByNameAsync(request.Username)
                    ?? await _userManager.FindByEmailAsync(request.Username);

            if (user == null)
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            // Check lockout TRUOC IsActive
            if (await _userManager.IsLockedOutAsync(user))
                return Unauthorized(new { success = false, message = "LOCKED" });

            if (!user.IsActive)
                return Unauthorized(new { success = false, message = "LOCKED" });

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                await _userManager.AccessFailedAsync(user);

                if (await _userManager.IsLockedOutAsync(user))
                {
                    user.IsActive = false;
                    await _userManager.UpdateAsync(user);
                    return Unauthorized(new { success = false, message = "LOCKED" });
                }

                var failCount = await _userManager.GetAccessFailedCountAsync(user);
                var remaining = Math.Max(0, 3 - failCount);
                return Unauthorized(new { success = false, message = "Invalid username or password. " + (remaining > 0 ? $"{remaining} attempt(s) remaining." : "Account will be locked on next attempt.") });
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var role  = roles.FirstOrDefault() ?? "User";
            var token = GenerateJwtToken(user, role, request.RememberMe);

            return Ok(new
            {
                success = true,
                token   = token,
                user    = new
                {
                    id       = user.Id,
                    username = user.UserName,
                    email    = user.Email,
                    fullName = user.FullName,
                    role     = role
                }
            });
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

        private string GenerateJwtToken(AppUser user, string role, bool rememberMe)
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(8);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim("username",                    user.UserName ?? ""),
                new Claim(ClaimTypes.Name,               user.UserName ?? ""),
                new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", role),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };

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
    }
}