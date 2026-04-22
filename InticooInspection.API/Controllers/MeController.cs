using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InticooInspection.API.Controllers
{
    /// <summary>
    /// Standalone controller for the current user's context endpoints.
    /// Kept SEPARATE from UserController because UserController has
    /// [AllowAnonymous] at class level, which makes the JWT middleware
    /// skip token parsing entirely — so User.Claims is empty even when
    /// the client sends a valid Bearer token.
    ///
    /// This controller has [Authorize] at class level, forcing the
    /// JWT bearer handler to run and populate User.Claims.
    /// </summary>
    [ApiController]
    [Route("api/me")]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;

        public MeController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        // GET api/me/page-access
        // Returns the current user's PageAccess CSV and roles.
        // Used by the Blazor PageAccessService to drive menu visibility.
        [HttpGet("page-access")]
        public async Task<IActionResult> GetPageAccess()
        {
            // ASP.NET maps JWT `sub` claim → ClaimTypes.NameIdentifier by default.
            // Fall back to other common identifiers to be safe.
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "No user id in token." });

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
