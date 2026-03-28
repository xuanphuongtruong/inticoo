using InticooInspection.Application.DTOs;
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

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new AuthResponseDto { Success = false, Message = "Username and password are required." });

            // Tìm user theo username hoặc email
            var user = await _userManager.FindByNameAsync(dto.Username)
                    ?? await _userManager.FindByEmailAsync(dto.Username);

            if (user == null || !user.IsActive)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid username or password." });

            // Kiểm tra tài khoản bị khóa
            if (await _userManager.IsLockedOutAsync(user))
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Account is locked. Please contact your administrator." });

            // Verify password
            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                await _userManager.AccessFailedAsync(user); // tăng failed count
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid username or password." });
            }

            // Reset failed count khi login thành công
            await _userManager.ResetAccessFailedCountAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var token = GenerateJwtToken(user, roles, dto.RememberMe);

            return Ok(new AuthResponseDto
            {
                Success = true,
                Token = token,
                User = new UserInfoDto
                {
                    Id       = user.Id,
                    Username = user.UserName ?? "",
                    Email    = user.Email ?? "",
                    FullName = user.FullName,
                    Role     = roles.FirstOrDefault() ?? "User"
                }
            });
        }

        // GET /api/auth/me — lấy thông tin user hiện tại từ token
        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId ?? "");
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new UserInfoDto
            {
                Id       = user.Id,
                Username = user.UserName ?? "",
                Email    = user.Email ?? "",
                FullName = user.FullName,
                Role     = roles.FirstOrDefault() ?? "User"
            });
        }

        // ─── Helper ─────────────────────────────────────────────────────────────

        private string GenerateJwtToken(AppUser user, IList<string> roles, bool rememberMe)
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(8);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name,           user.UserName ?? ""),
                new(ClaimTypes.Email,          user.Email ?? ""),
                new("fullName",                user.FullName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

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
}
