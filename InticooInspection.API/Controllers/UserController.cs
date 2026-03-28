using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [AllowAnonymous]
    public class UserController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET api/users
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.UserName!.Contains(search) ||
                    u.Email!.Contains(search) ||
                    u.FullName.Contains(search) ||
                    (u.InspectorId != null && u.InspectorId.Contains(search)) ||
                    (u.Mobile      != null && u.Mobile.Contains(search)));

            var total = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = new List<object>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                items.Add(new
                {
                    id          = u.Id,
                    username    = u.UserName,
                    email       = u.Email,
                    fullName    = u.FullName,
                    isActive    = u.IsActive,
                    roles       = roles,
                    createdAt   = u.CreatedAt,
                    cvUrl       = u.CvUrl,
                    inspectorId = u.InspectorId,
                    category    = u.Category,
                    address     = u.Address,
                    city        = u.City,
                    country     = u.Country,
                    mobile      = u.Mobile
                });
            }

            return Ok(new { total, page, pageSize, items });
        }

        // GET api/users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                id          = user.Id,
                username    = user.UserName,
                email       = user.Email,
                fullName    = user.FullName,
                isActive    = user.IsActive,
                roles       = roles,
                createdAt   = user.CreatedAt,
                cvUrl       = user.CvUrl,
                inspectorId = user.InspectorId,
                category    = user.Category,
                address     = user.Address,
                city        = user.City,
                country     = user.Country,
                mobile      = user.Mobile
            });
        }

        // POST api/users
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            if (await _userManager.FindByNameAsync(request.Username) != null)
                return BadRequest(new { success = false, message = "Username already exists." });

            if (!string.IsNullOrEmpty(request.Email) && await _userManager.FindByEmailAsync(request.Email) != null)
                return BadRequest(new { success = false, message = "Email already exists." });

            var user = new AppUser
            {
                UserName       = request.Username,
                Email          = request.Email,
                FullName       = request.FullName,
                IsActive       = request.IsActive,
                CvUrl          = request.CvUrl,
                InspectorId    = request.InspectorId,
                Category       = request.Category,
                Address        = request.Address,
                City           = request.City,
                Country        = request.Country,
                Mobile         = request.Mobile,
                CreatedAt      = DateTime.UtcNow,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });

            var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            foreach (var role in allRoles)
                if (request.Roles.Contains(role))
                    await _userManager.AddToRoleAsync(user, role);

            return Ok(new { success = true, id = user.Id });
        }

        // PUT api/users/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var existing = await _userManager.FindByNameAsync(request.Username);
            if (existing != null && existing.Id != id)
                return BadRequest(new { success = false, message = "Username already exists." });

            user.UserName = request.Username;
            user.Email    = request.Email;
            user.FullName = request.FullName;
            user.IsActive = request.IsActive;
            if (request.CvUrl     != null) user.CvUrl     = request.CvUrl;
            user.InspectorId = request.InspectorId;
            user.Category    = request.Category;
            user.Address     = request.Address;
            user.City        = request.City;
            user.Country     = request.Country;
            user.Mobile      = request.Mobile;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                var token    = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
                if (!pwResult.Succeeded)
                    return BadRequest(new { success = false, message = string.Join(", ", pwResult.Errors.Select(e => e.Description)) });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            foreach (var role in allRoles)
                if (request.Roles.Contains(role))
                    await _userManager.AddToRoleAsync(user, role);

            return Ok(new { success = true });
        }

        // PUT api/users/{id}/toggle-active
        [HttpPut("{id}/toggle-active")]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            return Ok(new { success = true, isActive = user.IsActive });
        }

        // DELETE api/users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (id == currentUserId)
                return BadRequest(new { success = false, message = "Cannot delete your own account." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            await _userManager.DeleteAsync(user);
            return Ok(new { success = true });
        }

        // GET api/users/nextid — sinh InspectorId tự tăng IP10001, IP10002...
        [HttpGet("nextid")]
        public async Task<IActionResult> GetNextId()
        {
            var ids = await _userManager.Users
                .Where(u => u.InspectorId != null && u.InspectorId.StartsWith("IP"))
                .Select(u => u.InspectorId!)
                .ToListAsync();

            int max = 10000;
            foreach (var id in ids)
            {
                var numPart = id.Substring(2);
                if (int.TryParse(numPart, out int n) && n > max)
                    max = n;
            }
            return Ok(new { inspectorId = $"IP{max + 1}" });
        }

        // GET api/users/dropdown — tất cả user active dùng cho dropdown Inspector
        [HttpGet("dropdown")]
        public async Task<IActionResult> GetDropdown()
        {
            var users = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.InspectorId, u.FullName })
                .ToListAsync();
            return Ok(users);
        }

        // GET api/users/all-roles
        [HttpGet("all-roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            return Ok(roles);
        }
    }

    public class CreateUserRequest
    {
        public string  Username    { get; set; } = "";
        public string  Password    { get; set; } = "";
        public string  Email       { get; set; } = "";
        public string  FullName    { get; set; } = "";
        public bool    IsActive    { get; set; } = true;
        public List<string> Roles  { get; set; } = new();
        public string? CvUrl       { get; set; }
        public string? InspectorId { get; set; }
        public string? Category    { get; set; }
        public string? Address     { get; set; }
        public string? City        { get; set; }
        public string? Country     { get; set; }
        public string? Mobile      { get; set; }
    }

    public class UpdateUserRequest
    {
        public string  Username    { get; set; } = "";
        public string  Email       { get; set; } = "";
        public string  FullName    { get; set; } = "";
        public bool    IsActive    { get; set; } = true;
        public string? NewPassword { get; set; }
        public List<string> Roles  { get; set; } = new();
        public string? CvUrl       { get; set; }
        public string? InspectorId { get; set; }
        public string? Category    { get; set; }
        public string? Address     { get; set; }
        public string? City        { get; set; }
        public string? Country     { get; set; }
        public string? Mobile      { get; set; }
    }
}
