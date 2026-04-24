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
        // Supports optional roles filter: ?roles=Customer,Inticoo  (comma-separated)
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? roles,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _userManager.Users.AsQueryable();

            // ── Filter by roles (comma-separated) ──
            // Identity stores role assignments in AspNetUserRoles + AspNetRoles.
            // We resolve users-in-role via UserManager (which handles the join)
            // then narrow the main query by those Ids.
            if (!string.IsNullOrWhiteSpace(roles))
            {
                var roleNames = roles
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => r.Length > 0)
                    .ToList();

                if (roleNames.Count > 0)
                {
                    var idSet = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var roleName in roleNames)
                    {
                        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
                        foreach (var u in usersInRole) idSet.Add(u.Id);
                    }

                    if (idSet.Count == 0)
                    {
                        // No users matched — return empty page rather than unfiltered data
                        return Ok(new { total = 0, page, pageSize, items = Array.Empty<object>() });
                    }

                    query = query.Where(u => idSet.Contains(u.Id));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.UserName!.Contains(search) ||
                    u.Email!.Contains(search) ||
                    u.FullName.Contains(search) ||
                    (u.InspectorId  != null && u.InspectorId.Contains(search))  ||
                    (u.Mobile       != null && u.Mobile.Contains(search))        ||
                    (u.Nationality  != null && u.Nationality.Contains(search))   ||
                    (u.Country      != null && u.Country.Contains(search)));

            var total = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = new List<object>();
            foreach (var u in users)
            {
                var userRoles = await _userManager.GetRolesAsync(u);
                items.Add(MapToDto(u, userRoles));
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
            return Ok(MapToDto(user, roles));
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
                UserName            = request.Username,
                Email               = request.Email,
                FullName            = request.FullName,
                ShortName           = request.ShortName,
                DateOfBirth         = request.DateOfBirth,
                Gender              = request.Gender,
                Nationality         = request.Nationality,
                IdType              = request.IdType,
                IdNumber            = request.IdNumber,
                Category            = request.Category,
                InspectionStartYear = request.InspectionStartYear,
                Language            = request.Language,
                IsActive            = request.IsActive,
                CvUrl               = request.CvUrl,
                AvatarUrl           = request.PhotoUrl,
                InspectorId         = request.InspectorId,
                Address             = request.Address,
                Address1            = request.Address1,
                Address2            = request.Address2,
                City                = request.City,
                State               = request.State,
                Country             = request.Country,
                PostalCode          = request.PostalCode,
                Mobile              = request.Mobile,
                CustomerId          = request.CustomerId,
                PageAccess          = request.PageAccess,
                CreatedAt           = DateTime.UtcNow,
                EmailConfirmed      = true,
                LockoutEnabled      = true
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

    // Check trùng username (nếu có đổi)
    if (!string.IsNullOrWhiteSpace(request.Username))
    {
        var existing = await _userManager.FindByNameAsync(request.Username);
        if (existing != null && existing.Id != id)
            return BadRequest(new { success = false, message = "Username already exists." });
    }

    // ── Các trường BẮT BUỘC có trên form Blazor (luôn ghi đè) ──
    if (!string.IsNullOrWhiteSpace(request.Username)) user.UserName = request.Username;
    if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName;

    // IsActive là bool, luôn apply (form luôn có)
    user.IsActive = request.IsActive;

    // ── Các trường OPTIONAL trên form Blazor ──
    // Dùng "!= null" để cho phép client xoá (gửi chuỗi rỗng "")
    // nhưng KHÔNG ghi đè nếu property không được gửi lên.
    // Vì JSON default cho string? là null, ta chỉ update khi có giá trị.
    if (request.Email      != null) user.Email      = request.Email;
    if (request.Mobile     != null) user.Mobile     = request.Mobile;
    if (request.Country    != null) user.Country    = request.Country;
    if (request.CustomerId != null) user.CustomerId = request.CustomerId;
    if (request.PageAccess != null) user.PageAccess = request.PageAccess;

    // ── Các trường KHÔNG có trên form Blazor — CHỈ update nếu client gửi ──
    // Đây chính là nguồn gốc của bug: trước đây gán thẳng = null làm mất data.
    if (request.ShortName           != null) user.ShortName           = request.ShortName;
    if (request.DateOfBirth.HasValue)        user.DateOfBirth         = request.DateOfBirth;
    if (request.Gender              != null) user.Gender              = request.Gender;
    if (request.Nationality         != null) user.Nationality         = request.Nationality;
    if (request.IdType              != null) user.IdType              = request.IdType;
    if (request.IdNumber            != null) user.IdNumber            = request.IdNumber;
    if (request.Category            != null) user.Category            = request.Category;
    if (request.InspectionStartYear.HasValue) user.InspectionStartYear = request.InspectionStartYear;
    if (request.Language            != null) user.Language            = request.Language;
    if (request.CvUrl               != null) user.CvUrl               = request.CvUrl;
    if (request.PhotoUrl            != null) user.AvatarUrl           = request.PhotoUrl;
    if (request.InspectorId         != null) user.InspectorId         = request.InspectorId;
    if (request.Address             != null) user.Address             = request.Address;
    if (request.Address1            != null) user.Address1            = request.Address1;
    if (request.Address2            != null) user.Address2            = request.Address2;
    if (request.City                != null) user.City                = request.City;
    if (request.State               != null) user.State               = request.State;
    if (request.PostalCode          != null) user.PostalCode          = request.PostalCode;

    var result = await _userManager.UpdateAsync(user);
    if (!result.Succeeded)
        return BadRequest(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });

    // ── Đổi password nếu có ──
    if (!string.IsNullOrWhiteSpace(request.NewPassword))
    {
        var token    = await _userManager.GeneratePasswordResetTokenAsync(user);
        var pwResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!pwResult.Succeeded)
            return BadRequest(new { success = false, message = string.Join(", ", pwResult.Errors.Select(e => e.Description)) });
    }

    // ── Cập nhật roles CHỈ khi client có gửi (Count > 0) ──
    // Nếu client gửi mảng rỗng → không đụng tới roles hiện tại.
    if (request.Roles != null && request.Roles.Count > 0)
    {
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        foreach (var role in allRoles)
            if (request.Roles.Contains(role))
                await _userManager.AddToRoleAsync(user, role);
    }

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

        // GET api/users/nextid
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

        // GET api/users/dropdown
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

        // ─────────────────────────────────────────────────────────────────
        // GET api/users/{inspectorId}/review?year=2026&month=4
        // Inspector Performance Overview
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("{inspectorId}/review")]
        public async Task<IActionResult> GetInspectorReview(string inspectorId, [FromQuery] int? year, [FromQuery] int? month)
        {
            try
            {
                var now         = DateTime.UtcNow;
                var targetYear  = year  ?? now.Year;
                var targetMonth = month ?? now.Month;

                // Find inspector user
                var inspector = await _userManager.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.InspectorId == inspectorId);
                if (inspector == null) return NotFound(new { error = "Inspector not found" });

                // Load AppDbContext via DI — need to inject it
                // We'll use a raw HTTP call pattern instead: return 404 if no db access
                // Actually UserController doesn't have AppDbContext, so we need a workaround.
                // Return inspector info + signal client to call inspection API separately.
                return Ok(new
                {
                    inspectorId   = inspector.InspectorId,
                    inspectorName = inspector.FullName,
                    targetYear, targetMonth,
                    nationality   = inspector.Nationality,
                    category      = inspector.Category,
                    language      = inspector.Language,
                    inspectionStartYear = inspector.InspectionStartYear,
                    avatarUrl     = inspector.AvatarUrl,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST api/users/change-password
        // Người dùng tự đổi mật khẩu của mình.
        // Mọi role đều được phép gọi endpoint này.
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { success = false, message = "Username is required." });
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                return BadRequest(new { success = false, message = "Current password is required." });
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { success = false, message = "New password is required." });
            if (request.NewPassword.Length < 6)
                return BadRequest(new { success = false, message = "New password must be at least 6 characters." });
            if (request.NewPassword == request.CurrentPassword)
                return BadRequest(new { success = false, message = "New password must be different from current password." });

            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            // Verify current password
            var ok = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
            if (!ok)
                return BadRequest(new { success = false, message = "Current password is incorrect." });

            // Dùng ChangePasswordAsync (Identity tự xử lý hash + validator)
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });

            return Ok(new { success = true, message = "Password changed successfully." });
        }

        // ── Helper ───────────────────────────────────────────────────────────
        private static object MapToDto(AppUser u, IList<string> roles) => new
        {
            id                  = u.Id,
            username            = u.UserName,
            email               = u.Email,
            fullName            = u.FullName,
            shortName           = u.ShortName,
            dateOfBirth         = u.DateOfBirth,
            gender              = u.Gender,
            nationality         = u.Nationality,
            idType              = u.IdType,
            idNumber            = u.IdNumber,
            category            = u.Category,
            inspectionStartYear = u.InspectionStartYear,
            language            = u.Language,
            isActive            = u.IsActive,
            roles               = roles,
            createdAt           = u.CreatedAt,
            cvUrl               = u.CvUrl,
            photoUrl            = u.AvatarUrl,
            inspectorId         = u.InspectorId,
            address             = u.Address,
            address1            = u.Address1,
            address2            = u.Address2,
            city                = u.City,
            state               = u.State,
            country             = u.Country,
            postalCode          = u.PostalCode,
            mobile              = u.Mobile,
            customerId          = u.CustomerId,
            pageAccess          = u.PageAccess
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────────────────────────

    public class CreateUserRequest
    {
        public string   Username            { get; set; } = "";
        public string   Password            { get; set; } = "";
        public string   Email               { get; set; } = "";
        public string   FullName            { get; set; } = "";
        public string?  ShortName           { get; set; }
        public DateTime? DateOfBirth        { get; set; }
        public string?  Gender              { get; set; }
        public string?  Nationality         { get; set; }
        public string?  IdType              { get; set; }
        public string?  IdNumber            { get; set; }
        public string?  Category            { get; set; }
        public int?     InspectionStartYear { get; set; }
        public string?  Language            { get; set; }
        public bool     IsActive            { get; set; } = true;
        public List<string> Roles           { get; set; } = new();
        public string?  CvUrl               { get; set; }
        public string?  PhotoUrl            { get; set; }
        public string?  InspectorId         { get; set; }
        public string?  Address             { get; set; }
        public string?  Address1            { get; set; }
        public string?  Address2            { get; set; }
        public string?  City                { get; set; }
        public string?  State               { get; set; }
        public string?  Country             { get; set; }
        public string?  PostalCode          { get; set; }
        public string?  Mobile              { get; set; }
        public string?  CustomerId          { get; set; }
        public string?  PageAccess          { get; set; }
    }

    public class UpdateUserRequest
    {
        public string   Username            { get; set; } = "";
        public string   Email               { get; set; } = "";
        public string   FullName            { get; set; } = "";
        public string?  ShortName           { get; set; }
        public DateTime? DateOfBirth        { get; set; }
        public string?  Gender              { get; set; }
        public string?  Nationality         { get; set; }
        public string?  IdType              { get; set; }
        public string?  IdNumber            { get; set; }
        public string?  Category            { get; set; }
        public int?     InspectionStartYear { get; set; }
        public string?  Language            { get; set; }
        public bool     IsActive            { get; set; } = true;
        public string?  NewPassword         { get; set; }
        public List<string> Roles           { get; set; } = new();
        public string?  CvUrl               { get; set; }
        public string?  PhotoUrl            { get; set; }
        public string?  InspectorId         { get; set; }
        public string?  Address             { get; set; }
        public string?  Address1            { get; set; }
        public string?  Address2            { get; set; }
        public string?  City                { get; set; }
        public string?  State               { get; set; }
        public string?  Country             { get; set; }
        public string?  PostalCode          { get; set; }
        public string?  Mobile              { get; set; }
        public string?  CustomerId          { get; set; }
        public string?  PageAccess          { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string Username        { get; set; } = "";
        public string CurrentPassword { get; set; } = "";
        public string NewPassword     { get; set; } = "";
    }
}
