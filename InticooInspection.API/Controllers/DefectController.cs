using InticooInspection.Domain.Security;
using System.Text.Json;
using InticooInspection.API.Services;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    /// <summary>Defect Management / CAPA.</summary>
    [ApiController]
    [Route("api/defects")]
    [AllowAnonymous]
    public class DefectController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly AzureBlobService _blob;
        private readonly UserManager<AppUser> _users;
        static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public DefectController(AppDbContext db, AzureBlobService blob, UserManager<AppUser> users)
        {
            _db = db; _blob = blob; _users = users;
        }

        /// <summary>
        /// Người dùng từ JWT. Middleware JwtBearer của app không xác thực được (xem MeController),
        /// nên tự kiểm tra token giống MeController: cùng Key / Issuer / Audience.
        /// </summary>
        Task<System.Security.Claims.ClaimsPrincipal?> JwtUserAsync()
        {
            var header = Request.Headers["Authorization"].ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<System.Security.Claims.ClaimsPrincipal?>(null);
            try
            {
                var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var prms = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                    ValidIssuer = cfg["Jwt:Issuer"], ValidAudience = cfg["Jwt:Audience"],
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
                var principal = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                    .ValidateToken(header["Bearer ".Length..].Trim(), prms, out _);
                return Task.FromResult<System.Security.Claims.ClaimsPrincipal?>(principal);
            }
            catch { return Task.FromResult<System.Security.Claims.ClaimsPrincipal?>(null); }
        }

        /// <summary>Phạm vi khách hàng của người gọi (null = không giới hạn) — xem UserAccess / CustomerAccess.Scope.</summary>
        Task<HashSet<string>?> CustomerScopeAsync() =>
            HttpContext.RequestServices.GetRequiredService<UserAccess>().CustomerScopeAsync();

        /// <summary>true nếu defect/inspection thuộc khách hàng ngoài phạm vi của người gọi.</summary>
        async Task<bool> OutOfScopeAsync(string? customerId)
        {
            var scope = await CustomerScopeAsync();
            return scope != null && !scope.Contains(customerId ?? "");
        }

        // GET api/defects?status=&vendorId=&search=&year=
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? vendorId,
            [FromQuery] string? search, [FromQuery] string? severity, [FromQuery] int? year)
        {
            var q = _db.DefectCapas.AsNoTracking().AsQueryable();
            var scope = await CustomerScopeAsync();
            if (scope != null) { var ids = scope.ToList(); q = q.Where(c => c.CustomerId != null && ids.Contains(c.CustomerId)); }
            if (!string.IsNullOrWhiteSpace(status))   q = q.Where(c => c.Status == status);
            if (!string.IsNullOrWhiteSpace(vendorId)) q = q.Where(c => c.VendorId == vendorId);
            if (!string.IsNullOrWhiteSpace(severity)) q = q.Where(c => c.Severity == severity);
            if (year.HasValue)                        q = q.Where(c => c.OpenDate.Year == year);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(c => c.DefectNo.Contains(search) || c.CarNo.Contains(search)
                              || (c.JobNumber != null && c.JobNumber.Contains(search))
                              || (c.ProductName != null && c.ProductName.Contains(search))
                              || (c.ItemNumber != null && c.ItemNumber.Contains(search))
                              || (c.VendorName != null && c.VendorName.Contains(search))
                              || (c.DefectType != null && c.DefectType.Contains(search)));

            var today = DateTime.UtcNow.Date;
            var items = await q.OrderByDescending(c => c.Id).Select(c => new
            {
                c.Id, c.DefectNo, c.CarNo, c.InspectionId, c.JobNumber,
                factory = c.VendorName, c.VendorId, product = c.ProductName, c.ItemNumber,
                c.DefectCode, c.DefectType, c.Severity, c.Quantity,
                c.OpenDate, c.DueDate, responsibleName = c.ResponsiblePerson, c.Status, c.CloseDate,
                c.AutoCreated,
                signedCount = (c.FactoryRepSignedAt != null ? 1 : 0) + (c.QaSignedAt != null ? 1 : 0) + (c.CustomerSignedAt != null ? 1 : 0),
                overdue = c.Status != DefectCapaService.StatusClosed && c.DueDate != null && c.DueDate < today
            }).ToListAsync();

            return Ok(new
            {
                total      = items.Count,
                open       = items.Count(i => i.Status == DefectCapaService.StatusOpen),
                inProgress = items.Count(i => i.Status == DefectCapaService.StatusInProgress),
                closed     = items.Count(i => i.Status == DefectCapaService.StatusClosed),
                overdue    = items.Count(i => i.overdue),
                items
            });
        }

        // GET api/defects/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var c = await _db.DefectCapas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            var scope = await CustomerScopeAsync();
            if (scope != null && !scope.Contains(c.CustomerId ?? "")) return NotFound();
            var dto = ToDto(c);
            dto.ProductPhotoUrl = await ProductPhotoAsync(c.ItemNumber, c.ProductName);
            return Ok(dto);
        }

        // GET api/defects/inspection/{inspectionId} — màn "Defect Management" của 1 inspection
        [HttpGet("inspection/{inspectionId:int}")]
        public async Task<IActionResult> ByInspection(int inspectionId)
        {
            var insp = await _db.Inspections.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inspectionId);
            if (insp == null) return NotFound();
            var scope = await CustomerScopeAsync();
            if (scope != null && !scope.Contains(insp.CustomerId ?? "")) return NotFound();
            var stats = (await QualityStats.LoadAsync(_db, q => q.Where(i => i.Id == inspectionId))).FirstOrDefault();

            var capas = await _db.DefectCapas.AsNoTracking().Where(c => c.InspectionId == inspectionId)
                .OrderBy(c => c.Id).ToListAsync();

            return Ok(new
            {
                inspectionId   = insp.Id,
                insp.JobNumber,
                insp.PoNumber,
                factory        = insp.VendorName,
                insp.VendorId,
                insp.CustomerName,
                product        = insp.ProductName,
                insp.ItemNumber,
                insp.InspectionDate,
                inspector      = insp.InspectorName,
                status         = insp.Status.ToString(),
                insp.FinalResult,
                critical       = stats?.Critical ?? 0,
                major          = stats?.Major ?? 0,
                minor          = stats?.Minor ?? 0,
                sampleSize     = stats?.SampleSize ?? 0,
                productPhotoUrl = await ProductPhotoAsync(insp.ItemNumber, insp.ProductName),
                defects = capas.Select(c => new
                {
                    c.Id, c.DefectNo, c.CarNo, c.DefectCode, c.DefectType, c.Severity, c.Quantity, c.Status,
                    c.Description, photo = Photos(c.PhotosJson).FirstOrDefault(), c.DueDate
                })
            });
        }

        // GET api/defects/inspections-lookup — inspection đã hoàn tất để tạo defect thủ công
        [HttpGet("inspections-lookup")]
        public async Task<IActionResult> InspectionsLookup()
        {
            var list = await _db.Inspections.AsNoTracking()
                .Where(i => i.Status == InspectionStatus.Completed)
                .OrderByDescending(i => i.InspectionDate)
                .Select(i => new { i.Id, i.JobNumber, factory = i.VendorName, product = i.ProductName, i.ItemNumber, i.InspectionDate })
                .Take(300).ToListAsync();
            return Ok(list);
        }

        // GET api/defects/new?inspectionId= — dữ liệu khởi tạo cho defect thủ công
        [HttpGet("new")]
        public async Task<IActionResult> NewTemplate([FromQuery] int inspectionId)
        {
            var insp = await _db.Inspections.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inspectionId);
            if (insp == null) return NotFound();
            var stats = (await QualityStats.LoadAsync(_db, q => q.Where(i => i.Id == inspectionId))).FirstOrDefault();
            var c = new DefectCapa
            {
                InspectionId = insp.Id, JobNumber = insp.JobNumber, PoNumber = insp.PoNumber,
                InspectionDate = insp.InspectionDate, InspectorName = insp.InspectorName,
                VendorId = insp.VendorId, VendorName = insp.VendorName, CustomerId = insp.CustomerId,
                CustomerName = insp.CustomerName, ProductName = insp.ProductName, ItemNumber = insp.ItemNumber,
                ProductQtyChecked = stats?.SampleSize > 0 ? stats.SampleSize : null,
                Severity = "Major", AqlClassification = "Major", Priority = "Medium",
                OpenDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.Date.AddDays(DefectCapaService.DueDays("Major")),
            };
            c.TargetDate = c.DueDate;
            await DefectCapaService.AssignNumbersAsync(_db, c);
            var dto = ToDto(c);
            dto.ProductPhotoUrl = await ProductPhotoAsync(insp.ItemNumber, insp.ProductName);
            return Ok(dto);
        }

        // POST api/defects — tạo defect thủ công
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DefectCapaDto dto)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.DefectManagement, PermAction.Create)) is { } denied) return denied;
            var c = new DefectCapa { OpenDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, Status = DefectCapaService.StatusOpen };
            Apply(c, dto);
            if (c.InspectionId.HasValue)
            {
                var insp = await _db.Inspections.AsNoTracking().FirstOrDefaultAsync(i => i.Id == c.InspectionId);
                if (insp != null && await OutOfScopeAsync(insp.CustomerId)) return NotFound();
                if (insp != null)
                {
                    c.JobNumber = insp.JobNumber; c.PoNumber = insp.PoNumber; c.InspectionDate = insp.InspectionDate;
                    c.InspectorName = insp.InspectorName; c.VendorId = insp.VendorId; c.VendorName = insp.VendorName;
                    c.CustomerId = insp.CustomerId; c.CustomerName = insp.CustomerName;
                    c.ProductName = insp.ProductName; c.ItemNumber = insp.ItemNumber;
                }
            }
            c.DueDate ??= c.TargetDate ?? DateTime.UtcNow.Date.AddDays(DefectCapaService.DueDays(c.Severity));
            await DefectCapaService.AssignNumbersAsync(_db, c);
            _db.DefectCapas.Add(c);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, c.Id, c.DefectNo, c.CarNo });
        }

        // PUT api/defects/{id} — lưu từng bước wizard
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] DefectCapaDto dto)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.DefectManagement, PermAction.Edit)) is { } denied) return denied;
            var c = await _db.DefectCapas.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || await OutOfScopeAsync(c.CustomerId)) return NotFound();
            Apply(c, dto);
            if (c.TargetDate.HasValue) c.DueDate = c.TargetDate;
            // Bắt đầu xử lý → In Progress (Close phải qua endpoint riêng)
            if (c.Status == DefectCapaService.StatusOpen && !string.IsNullOrEmpty(dto.Status) && dto.Status == DefectCapaService.StatusInProgress)
                c.Status = DefectCapaService.StatusInProgress;
            c.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // POST api/defects/{id}/start
        [HttpPost("{id:int}/start")]
        public async Task<IActionResult> Start(int id)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.DefectManagement, PermAction.Edit)) is { } denied) return denied;
            var c = await _db.DefectCapas.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || await OutOfScopeAsync(c.CustomerId)) return NotFound();
            if (c.Status == DefectCapaService.StatusOpen) { c.Status = DefectCapaService.StatusInProgress; c.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
            return Ok(new { success = true, c.Status });
        }

        // POST api/defects/{id}/close — Verification & Closure
        [HttpPost("{id:int}/close")]
        public async Task<IActionResult> Close(int id, [FromBody] DefectCapaDto dto)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.DefectManagement, PermAction.Edit)) is { } denied) return denied;
            var c = await _db.DefectCapas.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || await OutOfScopeAsync(c.CustomerId)) return NotFound();
            Apply(c, dto);
            if (c.VerificationDate == null || string.IsNullOrWhiteSpace(c.VerifiedBy) || string.IsNullOrWhiteSpace(c.EffectivenessResult))
                return BadRequest(new { message = "Verification Date, Verified By and Effectiveness Result are required." });
            if (c.EffectivenessResult == "Not Effective")
                return BadRequest(new { message = "Cannot close a defect whose actions are Not Effective." });
            c.Status    = DefectCapaService.StatusClosed;
            c.CloseDate = c.VerificationDate ?? DateTime.UtcNow;
            c.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // POST api/defects/{id}/reopen
        [HttpPost("{id:int}/reopen")]
        public async Task<IActionResult> Reopen(int id)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.DefectManagement, PermAction.Edit)) is { } denied) return denied;
            var c = await _db.DefectCapas.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || await OutOfScopeAsync(c.CustomerId)) return NotFound();
            c.Status = DefectCapaService.StatusInProgress; c.CloseDate = null; c.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // DELETE api/defects/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.DefectManagement, PermAction.Delete)) is { } denied) return denied;
            // Admin hoặc tài khoản có quyền Delete của Defect Management (kiểm tra ở dòng trên)

            var c = await _db.DefectCapas.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || await OutOfScopeAsync(c.CustomerId)) return NotFound();
            var sigFiles = new[] { c.FactoryRepSignatureUrl, c.QaSignatureUrl, c.CustomerSignatureUrl }
                .Where(u => _blob.IsOwnFile(u, "signatures")).ToList();
            _db.DefectCapas.Remove(c);
            await _db.SaveChangesAsync();
            // Xoá file chữ ký của defect trên server (ảnh lỗi dùng chung với QC report nên giữ lại)
            foreach (var u in sigFiles) await _blob.DeleteAsync("signatures", Path.GetFileName(u!));
            return Ok(new { success = true });
        }

        // ── Chữ ký điện tử ───────────────────────────────────────────────
        // Quyền ký theo ô:
        //   factory  (Factory Representative) : Admin / Inspector — đại diện nhà máy ký trên điện thoại của QC
        //   qa       (QA Verification)        : Admin / Inspector
        //   customer (Customer Approval)      : tài khoản Customer của đúng khách hàng (hoặc Admin)
        static bool CanSign(System.Security.Claims.ClaimsPrincipal u, string slot) => slot switch
        {
            "factory"  => u.IsInRole("Admin") || u.IsInRole("Inspector"),
            "qa"       => u.IsInRole("Admin") || u.IsInRole("Inspector"),
            "customer" => u.IsInRole("Admin") || u.IsInRole("Customer"),
            _          => false
        };

        // POST api/defects/{id}/sign  { slot, name, signatureDataUrl }
        [HttpPost("{id:int}/sign")]
        [Authorize]
        public async Task<IActionResult> Sign(int id, [FromBody] SignRequest req)
        {
            // Controller để [AllowAnonymous] ở cấp class nên phải tự kiểm tra đăng nhập/quyền ở đây
            var user = await JwtUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in to sign." });
            var slot = (req.Slot ?? "").Trim().ToLowerInvariant();
            if (!CanSign(user, slot)) return StatusCode(403, new { message = "Your account is not allowed to sign this approval." });
            if (string.IsNullOrWhiteSpace(req.SignatureDataUrl) || !req.SignatureDataUrl.StartsWith("data:image/png;base64,"))
                return BadRequest(new { message = "Signature is required." });

            var c = await _db.DefectCapas.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || await OutOfScopeAsync(c.CustomerId)) return NotFound();
            if (slot == "customer" && !user.IsInRole("Admin"))
            {
                var scope = await CustomerScopeAsync();
                if (scope != null && !scope.Contains(c.CustomerId ?? ""))
                    return StatusCode(403, new { message = "This defect belongs to another customer." });
            }
            var already = slot switch { "factory" => c.FactoryRepSignedAt, "qa" => c.QaSignedAt, _ => c.CustomerSignedAt };
            if (already != null && !req.Resign) return Conflict(new { message = "This approval has already been signed." });
            var oldUrl = slot switch { "factory" => c.FactoryRepSignatureUrl, "qa" => c.QaSignatureUrl, _ => c.CustomerSignatureUrl };

            byte[] bytes;
            try { bytes = Convert.FromBase64String(req.SignatureDataUrl["data:image/png;base64,".Length..]); }
            catch { return BadRequest(new { message = "Invalid signature image." }); }
            if (bytes.Length > 2 * 1024 * 1024) return BadRequest(new { message = "Signature image is too large." });

            // Lưu file PNG vào thư mục server wwwroot/uploads/signatures — DB chỉ giữ đường dẫn tương đối
            await using var ms = new MemoryStream(bytes);
            var url = await _blob.UploadAsync("signatures", $"{c.DefectNo}_{slot}_{Guid.NewGuid():N}.png", ms, "image/png");

            var now  = DateTime.UtcNow;
            var who  = user.Identity?.Name ?? user.FindFirst("username")?.Value ?? "";
            // Tên người ký: nhập trong khung ký (mặc định họ tên tài khoản); để trống thì lấy họ tên tài khoản
            var name = req.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                var account = await _users.FindByNameAsync(who)
                              ?? await _users.FindByIdAsync(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
                name = !string.IsNullOrWhiteSpace(account?.FullName) ? account!.FullName.Trim() : who;
            }
            if (name.Length > 150) name = name[..150];
            var position = req.Position?.Trim();
            if (string.IsNullOrWhiteSpace(position)) position = null;
            else if (position.Length > 100) position = position[..100];
            switch (slot)
            {
                case "factory":  c.FactoryRepName = name;       c.FactoryRepPosition = position; c.FactoryRepSignatureUrl = url; c.FactoryRepSignedAt = now; c.FactoryRepSignedBy = who; break;
                case "qa":       c.QaVerifierName = name;       c.QaPosition = position;         c.QaSignatureUrl = url;         c.QaSignedAt = now;         c.QaSignedBy = who;         break;
                default:         c.CustomerApproverName = name; c.CustomerPosition = position;   c.CustomerSignatureUrl = url;   c.CustomerSignedAt = now;   c.CustomerSignedBy = who;
                                 c.CustomerApprovalDate = now; break;
            }
            c.UpdatedAt = now;
            await _db.SaveChangesAsync();

            // Ký lại: xoá file chữ ký cũ trên server
            if (_blob.IsOwnFile(oldUrl, "signatures"))
                await _blob.DeleteAsync("signatures", Path.GetFileName(oldUrl));

            return Ok(new { success = true, url, signedAt = now, signedBy = who, name, position });
        }

        // GET api/defects/signer — họ tên tài khoản đang đăng nhập (hiển thị trong khung ký)
        [HttpGet("signer")]
        public async Task<IActionResult> Signer()
        {
            var user = await JwtUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in." });
            var who = user.Identity?.Name ?? user.FindFirst("username")?.Value ?? "";
            var account = await _users.FindByNameAsync(who)
                          ?? await _users.FindByIdAsync(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
            return Ok(new { username = who, fullName = !string.IsNullOrWhiteSpace(account?.FullName) ? account!.FullName : who });
        }

        // DELETE api/defects/{id}/sign/{slot} — Admin xoá chữ ký để ký lại
        [HttpDelete("{id:int}/sign/{slot}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ClearSignature(int id, string slot)
        {
            var admin = await JwtUserAsync();
            if (admin == null || !admin.IsInRole("Admin"))
                return StatusCode(403, new { message = "Only Admin can clear a signature." });
            var c = await _db.DefectCapas.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || await OutOfScopeAsync(c.CustomerId)) return NotFound();
            // Xoá file chữ ký cũ trên server để không để lại file rác
            var oldUrl = slot.ToLowerInvariant() switch
            {
                "factory" => c.FactoryRepSignatureUrl, "qa" => c.QaSignatureUrl, "customer" => c.CustomerSignatureUrl, _ => null
            };
            if (_blob.IsOwnFile(oldUrl, "signatures"))
                await _blob.DeleteAsync("signatures", Path.GetFileName(oldUrl));
            switch (slot.ToLowerInvariant())
            {
                case "factory":  c.FactoryRepSignatureUrl = null; c.FactoryRepSignedAt = null; c.FactoryRepSignedBy = null; break;
                case "qa":       c.QaSignatureUrl = null;         c.QaSignedAt = null;         c.QaSignedBy = null;         break;
                case "customer": c.CustomerSignatureUrl = null;   c.CustomerSignedAt = null;   c.CustomerSignedBy = null;   c.CustomerApprovalDate = null; break;
                default: return BadRequest(new { message = "Unknown slot." });
            }
            c.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // POST api/defects/scan?dryRun=true — quét inspection đã hoàn tất, mở CAPA còn thiếu
        [HttpPost("scan")]
        public async Task<IActionResult> Scan([FromQuery] bool dryRun = false, [FromQuery] int? year = null)
        {
            if (await HttpContext.RequestServices.GetRequiredService<UserAccess>().RequireAsync((ModuleKeys.DefectManagement, PermAction.Create)) is { } denied) return denied;
            var ids = await _db.Inspections.AsNoTracking()
                .Where(i => i.Status == InspectionStatus.Completed && (year == null || i.InspectionDate.Year == year))
                .OrderBy(i => i.Id).Select(i => i.Id).ToListAsync();
            var created = new List<DefectCapa>();
            foreach (var id in ids)
                created.AddRange(await DefectCapaService.AutoOpenAsync(_db, id, dryRun));
            return Ok(new
            {
                dryRun,
                scanned = ids.Count,
                opened  = created.Count,
                items   = created.Select(c => new { c.DefectNo, c.CarNo, c.JobNumber, c.DefectCode, c.DefectType, c.Severity, c.Quantity })
            });
        }

        // ── helpers ─────────────────────────────────────────────────────
        async Task<string?> ProductPhotoAsync(string? itemNumber, string? productName)
        {
            var p = await _db.Products.AsNoTracking()
                .Where(x => (itemNumber != null && x.ItemNumber == itemNumber) || (productName != null && x.ProductName == productName))
                .Select(x => x.PhotoUrl).FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(p) ? null : p;
        }

        static List<string> Photos(string? json)
        {
            try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new(); } catch { return new(); }
        }
        static List<T> FromJson<T>(string? json)
        {
            try { return JsonSerializer.Deserialize<List<T>>(json ?? "[]", _json) ?? new(); } catch { return new(); }
        }

        /// <summary>Danh sách vị trí lỗi; dữ liệu cũ chỉ có 1 điểm (LocationX/Y) thì chuyển thành điểm số 1.</summary>
        static List<LocationPointDto> LocationsOf(DefectCapa c)
        {
            var list = FromJson<LocationPointDto>(c.LocationsJson);
            if (list.Count == 0 && c.LocationX.HasValue && c.LocationY.HasValue)
                list.Add(new LocationPointDto { X = c.LocationX.Value, Y = c.LocationY.Value, Label = c.LocationLabel });
            return list;
        }

        static DefectCapaDto ToDto(DefectCapa c) => new()
        {
            Id = c.Id, DefectNo = c.DefectNo, CarNo = c.CarNo, InspectionId = c.InspectionId, JobNumber = c.JobNumber,
            PoNumber = c.PoNumber, InspectionDate = c.InspectionDate, InspectorName = c.InspectorName,
            VendorId = c.VendorId, VendorName = c.VendorName, CustomerId = c.CustomerId, CustomerName = c.CustomerName,
            ProductName = c.ProductName, ItemNumber = c.ItemNumber,
            DefectCode = c.DefectCode, DefectCategory = c.DefectCategory, DefectType = c.DefectType, Severity = c.Severity,
            Quantity = c.Quantity, InspectionArea = c.InspectionArea, AqlClassification = c.AqlClassification,
            ProductQtyChecked = c.ProductQtyChecked, Description = c.Description,
            LocationImageUrl = c.LocationImageUrl, LocationX = c.LocationX, LocationY = c.LocationY, LocationLabel = c.LocationLabel,
            Locations = LocationsOf(c),
            Photos = Photos(c.PhotosJson), PhotoNotes = c.PhotoNotes, AfterPhotos = Photos(c.AfterPhotosJson),
            RootCauseType = c.RootCauseType, FinalRootCause = c.FinalRootCause,
            Why1 = c.Why1, Why2 = c.Why2, Why3 = c.Why3, Why4 = c.Why4, Why5 = c.Why5,
            ResponsibleDepartment = c.ResponsibleDepartment, ResponsiblePerson = c.ResponsiblePerson,
            CorrectiveAction = c.CorrectiveAction, TargetDate = c.TargetDate, Priority = c.Priority,
            CorrectiveActions = FromJson<CapaActionDto>(c.CorrectiveActionsJson),
            PreventiveActions = FromJson<PreventiveActionDto>(c.PreventiveActionsJson),
            CapaRemarks = c.CapaRemarks,
            VerificationDate = c.VerificationDate, VerifiedBy = c.VerifiedBy, EffectivenessResult = c.EffectivenessResult,
            VerificationComments = c.VerificationComments, References = FromJson<DefectReferenceDto>(c.ReferencesJson),
            FactoryRepName = c.FactoryRepName, QaVerifierName = c.QaVerifierName,
            CustomerApproverName = c.CustomerApproverName, CustomerApprovalDate = c.CustomerApprovalDate,
            FactoryRepPosition = c.FactoryRepPosition, QaPosition = c.QaPosition, CustomerPosition = c.CustomerPosition,
            FactoryRepSignatureUrl = c.FactoryRepSignatureUrl, FactoryRepSignedAt = c.FactoryRepSignedAt, FactoryRepSignedBy = c.FactoryRepSignedBy,
            QaSignatureUrl = c.QaSignatureUrl, QaSignedAt = c.QaSignedAt, QaSignedBy = c.QaSignedBy,
            CustomerSignatureUrl = c.CustomerSignatureUrl, CustomerSignedAt = c.CustomerSignedAt, CustomerSignedBy = c.CustomerSignedBy,
            Status = c.Status, OpenDate = c.OpenDate, DueDate = c.DueDate, CloseDate = c.CloseDate, AutoCreated = c.AutoCreated
        };

        static void Apply(DefectCapa c, DefectCapaDto d)
        {
            if (d.InspectionId.HasValue && c.InspectionId == null) c.InspectionId = d.InspectionId;
            c.DefectCode = d.DefectCode; c.DefectCategory = d.DefectCategory; c.DefectType = d.DefectType;
            c.Severity = string.IsNullOrWhiteSpace(d.Severity) ? c.Severity : d.Severity!;
            c.Quantity = d.Quantity; c.InspectionArea = d.InspectionArea; c.AqlClassification = d.AqlClassification;
            c.ProductQtyChecked = d.ProductQtyChecked; c.Description = d.Description;
            c.LocationImageUrl = d.LocationImageUrl;
            var locs = (d.Locations ?? new()).Where(l => l.X is >= 0 and <= 100 && l.Y is >= 0 and <= 100).Take(30).ToList();
            c.LocationsJson = JsonSerializer.Serialize(locs, _json);
            // Điểm đầu tiên vẫn ghi vào LocationX/Y/Label để báo cáo / dữ liệu cũ đọc được
            var first = locs.FirstOrDefault();
            c.LocationX = first?.X; c.LocationY = first?.Y;
            c.LocationLabel = locs.Any() ? string.Join(", ", locs.Select(l => l.Label).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()) : d.LocationLabel;
            c.PhotosJson = JsonSerializer.Serialize(d.Photos ?? new()); c.PhotoNotes = d.PhotoNotes;
            c.AfterPhotosJson = JsonSerializer.Serialize(d.AfterPhotos ?? new());
            c.RootCauseType = d.RootCauseType; c.FinalRootCause = d.FinalRootCause;
            c.Why1 = d.Why1; c.Why2 = d.Why2; c.Why3 = d.Why3; c.Why4 = d.Why4; c.Why5 = d.Why5;
            c.ResponsibleDepartment = d.ResponsibleDepartment; c.ResponsiblePerson = d.ResponsiblePerson;
            c.CorrectiveAction = d.CorrectiveAction; c.TargetDate = d.TargetDate; c.Priority = d.Priority;
            c.CorrectiveActionsJson = JsonSerializer.Serialize(d.CorrectiveActions ?? new(), _json);
            c.PreventiveActionsJson = JsonSerializer.Serialize(d.PreventiveActions ?? new(), _json);
            c.CapaRemarks = d.CapaRemarks;
            c.VerificationDate = d.VerificationDate; c.VerifiedBy = d.VerifiedBy; c.EffectivenessResult = d.EffectivenessResult;
            c.VerificationComments = d.VerificationComments;
            c.ReferencesJson = JsonSerializer.Serialize(d.References ?? new(), _json);
            // Ô đã ký thì khoá tên/ngày — chỉ thay đổi qua endpoint ký
            if (c.FactoryRepSignedAt == null) c.FactoryRepName = d.FactoryRepName;
            if (c.QaSignedAt == null)         c.QaVerifierName = d.QaVerifierName;
            if (c.CustomerSignedAt == null) { c.CustomerApproverName = d.CustomerApproverName; c.CustomerApprovalDate = d.CustomerApprovalDate; }
        }
    }

    public class LocationPointDto
    {
        public double  X     { get; set; }   // % theo chiều ngang ảnh
        public double  Y     { get; set; }   // % theo chiều dọc ảnh
        public string? Label { get; set; }   // Front Leg, Seat, ...
    }

    public class SignRequest
    {
        public string? Slot             { get; set; }   // factory | qa | customer
        public string? Name             { get; set; }   // họ tên người ký (mặc định = họ tên tài khoản đăng nhập, sửa được trước khi ký)
        public string? Position         { get; set; }   // chức vụ người ký
        public bool    Resign           { get; set; }   // ký lại ô đã ký (thay chữ ký + tên cũ)
        public string? SignatureDataUrl { get; set; }   // data:image/png;base64,...
    }

    public class CapaActionDto
    {
        public string?   Action         { get; set; }
        public DateTime? TargetDate     { get; set; }
        public bool      Done           { get; set; }
        public DateTime? CompletionDate { get; set; }
    }

    public class PreventiveActionDto
    {
        public string? Action    { get; set; }
        public string? Frequency { get; set; }
        public bool    Active    { get; set; }
    }

    public class DefectReferenceDto
    {
        public string? Reference { get; set; }
        public string? FileUrl   { get; set; }
        public string? FileName  { get; set; }
        public string? Remark    { get; set; }
    }

    public class DefectCapaDto
    {
        public int       Id             { get; set; }
        public string?   DefectNo       { get; set; }
        public string?   CarNo          { get; set; }
        public int?      InspectionId   { get; set; }
        public string?   JobNumber      { get; set; }
        public string?   PoNumber       { get; set; }
        public DateTime? InspectionDate { get; set; }
        public string?   InspectorName  { get; set; }
        public string?   VendorId       { get; set; }
        public string?   VendorName     { get; set; }
        public string?   CustomerId     { get; set; }
        public string?   CustomerName   { get; set; }
        public string?   ProductName    { get; set; }
        public string?   ItemNumber     { get; set; }
        public string?   ProductPhotoUrl { get; set; }

        public string?  DefectCode        { get; set; }
        public string?  DefectCategory    { get; set; }
        public string?  DefectType        { get; set; }
        public string?  Severity          { get; set; }
        public int      Quantity          { get; set; }
        public string?  InspectionArea    { get; set; }
        public string?  AqlClassification { get; set; }
        public int?     ProductQtyChecked { get; set; }
        public string?  Description       { get; set; }
        public string?  LocationImageUrl  { get; set; }
        public double?  LocationX         { get; set; }
        public double?  LocationY         { get; set; }
        public string?  LocationLabel     { get; set; }
        public List<LocationPointDto> Locations { get; set; } = new();
        public List<string> Photos        { get; set; } = new();
        public string?  PhotoNotes        { get; set; }
        public List<string> AfterPhotos   { get; set; } = new();
        public string?  RootCauseType     { get; set; }
        public string?  FinalRootCause    { get; set; }
        public string?  Why1 { get; set; }
        public string?  Why2 { get; set; }
        public string?  Why3 { get; set; }
        public string?  Why4 { get; set; }
        public string?  Why5 { get; set; }
        public string?   ResponsibleDepartment { get; set; }
        public string?   ResponsiblePerson     { get; set; }
        public string?   CorrectiveAction      { get; set; }
        public DateTime? TargetDate            { get; set; }
        public string?   Priority              { get; set; }
        public List<CapaActionDto>       CorrectiveActions { get; set; } = new();
        public List<PreventiveActionDto> PreventiveActions { get; set; } = new();
        public string?   CapaRemarks          { get; set; }
        public DateTime? VerificationDate     { get; set; }
        public string?   VerifiedBy           { get; set; }
        public string?   EffectivenessResult  { get; set; }
        public string?   VerificationComments { get; set; }
        public List<DefectReferenceDto> References { get; set; } = new();
        public string?   FactoryRepName       { get; set; }
        public string?   QaVerifierName       { get; set; }
        public string?   CustomerApproverName { get; set; }
        public DateTime? CustomerApprovalDate { get; set; }
        public string?   FactoryRepPosition   { get; set; }
        public string?   QaPosition           { get; set; }
        public string?   CustomerPosition     { get; set; }
        public string?   FactoryRepSignatureUrl { get; set; }
        public DateTime? FactoryRepSignedAt     { get; set; }
        public string?   FactoryRepSignedBy     { get; set; }
        public string?   QaSignatureUrl         { get; set; }
        public DateTime? QaSignedAt             { get; set; }
        public string?   QaSignedBy             { get; set; }
        public string?   CustomerSignatureUrl   { get; set; }
        public DateTime? CustomerSignedAt       { get; set; }
        public string?   CustomerSignedBy       { get; set; }
        public string?   Status      { get; set; }
        public DateTime  OpenDate    { get; set; }
        public DateTime? DueDate     { get; set; }
        public DateTime? CloseDate   { get; set; }
        public bool      AutoCreated { get; set; }
    }
}
