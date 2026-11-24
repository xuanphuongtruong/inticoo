using System.Text.Json;
using InticooInspection.API.Services;
using InticooInspection.Domain.Entities;
using InticooInspection.Domain.Security;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    /// <summary>
    /// Container Loading Check (CLC): job request + kiểm container 7 điểm + xác nhận xếp hàng + seal + chữ ký.
    ///   GET    api/clc                 danh sách
    ///   GET    api/clc/{id}            chi tiết đầy đủ
    ///   POST   api/clc                 tạo job
    ///   PUT    api/clc/{id}            lưu toàn bộ (job + kiểm + ảnh); Status = Draft|Pending|InProgress|Submitted
    ///   POST   api/clc/{id}/sign       ký (slot inspector | reviewer)
    ///   DELETE api/clc/{id}            xoá (chỉ Admin, chỉ job chưa kiểm)
    /// </summary>
    [ApiController]
    [Route("api/clc")]
    [AllowAnonymous]   // tự kiểm tra đăng nhập/quyền bằng UserAccess (giống DefectController)
    public class ClcController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly AzureBlobService _blob;
        private readonly UserAccess _access;

        public ClcController(AppDbContext db, AzureBlobService blob, UserAccess access)
        {
            _db = db; _blob = blob; _access = access;
        }

        static readonly string[] Statuses = { ClcStatus.Draft, ClcStatus.Pending, ClcStatus.InProgress, ClcStatus.Submitted };
        static readonly string[] Results  = { ClcResult.Pass, ClcResult.Fail, ClcResult.NA };

        // Quyền ghi: tạo/sửa job request HOẶC thực hiện kiểm (Summary Inspections)
        static readonly (string, string)[] WritePerms =
        {
            (ModuleKeys.ContainerLoadingCheck, PermAction.Create), (ModuleKeys.ContainerLoadingCheck, PermAction.Edit),
        };

        // ─────────────────────────────────────────────────────────────
        // LIST
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var ctx = await _access.GetAsync();
            if (ctx == null) return Unauthorized(new { message = "Please sign in again." });

            var q = _db.ContainerChecks.AsNoTracking().AsQueryable();
            if (ctx.CustomerScope != null)
                q = q.Where(c => c.CustomerCode != null && ctx.CustomerScope.Contains(c.CustomerCode));
            // QC/kho (chỉ có quyền thực hiện, không có quyền tạo job): chỉ thấy job đã giao cho mình
            if (ExecOnly(ctx))
            {
                var uid = ctx.User.Id;
                q = q.Where(c => c.InspectorId == uid && c.Status != ClcStatus.Draft);
            }
            // Số liệu tổng hợp (không bị ảnh hưởng bởi bộ lọc trạng thái/tìm kiếm)
            // New = chưa tới hạn (hoặc chưa có ngày); Delay = quá hạn → hai nhóm không giao nhau (giống Summary Inspections)
            var today = DateTime.Today;
            var stat = await q.GroupBy(c => 1).Select(g => new
            {
                all        = g.Count(),
                inProgress = g.Count(c => c.Status == "InProgress"),
                pending    = g.Count(c => (c.Status == "Draft" || c.Status == "Pending") && (c.PlannedLoadingDate == null || c.PlannedLoadingDate >= today)),
                submitted  = g.Count(c => c.Status == "Submitted"),
                delay      = g.Count(c => (c.Status == "Draft" || c.Status == "Pending") && c.PlannedLoadingDate != null && c.PlannedLoadingDate < today),
                pass       = g.Count(c => c.OverallResult == "PASS"),
                fail       = g.Count(c => c.OverallResult == "FAIL"),
            }).FirstOrDefaultAsync();
            if (status == "New")
                q = q.Where(c => (c.Status == "Draft" || c.Status == "Pending") && (c.PlannedLoadingDate == null || c.PlannedLoadingDate >= today));
            else if (status == "Delay")
                q = q.Where(c => (c.Status == "Draft" || c.Status == "Pending") && c.PlannedLoadingDate != null && c.PlannedLoadingDate < today);
            else if (!string.IsNullOrWhiteSpace(status) && Statuses.Contains(status)) q = q.Where(c => c.Status == status);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(c => c.JobNumber.Contains(s) || c.ContainerNo.Contains(s) || c.PoNumber.Contains(s)
                              || c.CustomerName.Contains(s) || c.VendorName.Contains(s)
                              || (c.InspectorName != null && c.InspectorName.Contains(s)));
            }
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
            var total = await q.CountAsync();
            var hideCust = ExecOnly(ctx);
            var items = await q.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.Id, c.JobNumber, c.Status, CustomerName = hideCust ? "" : c.CustomerName, c.VendorName, c.ContainerNo, c.ContainerType, c.PoNumber,
                    c.PlannedLoadingDate, c.InspectorName, c.InspectionDate, c.OverallResult, c.CreatedAt,
                    TotalCartons = c.Products.Sum(p => p.PlannedCartons),
                    TotalQty     = c.Products.Sum(p => p.PlannedQty),
                }).ToListAsync();
            return Ok(new { total, page, pageSize, items, stats = stat ?? new { all = 0, inProgress = 0, pending = 0, submitted = 0, delay = 0, pass = 0, fail = 0 } });
        }

        // ─────────────────────────────────────────────────────────────
        // GET ONE
        // ─────────────────────────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var ctx = await _access.GetAsync();
            if (ctx == null) return Unauthorized(new { message = "Please sign in again." });
            var c = await LoadAsync(id, track: false);
            if (c == null || OutOfScope(ctx, c)) return NotFound();
            var dto = ToDto(c);
            if (ExecOnly(ctx)) HideCustomer(dto);   // Inspector không được xem thông tin khách hàng
            return Ok(dto);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClcDto dto)
        {
            if (await _access.RequireAsync((ModuleKeys.ContainerLoadingCheck, PermAction.Create)) is { } denied) return denied;
            var ctx = (await _access.GetAsync())!;
            var err = ValidateJob(dto);
            if (err != null) return BadRequest(new { message = err });

            if (dto.Status == ClcStatus.Pending && string.IsNullOrWhiteSpace(dto.InspectorId))
                return BadRequest(new { message = "Please assign an inspector before sending the job request." });

            var c = new ContainerCheck { CreatedById = ctx.User.Id, CreatedAt = DateTime.UtcNow };
            c.JobNumber = await NextJobNumberAsync();
            Apply(c, dto, ctx, job: true, exec: false, allowStatus: new[] { ClcStatus.Draft, ClcStatus.Pending });
            _db.ContainerChecks.Add(c);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = c.Id, jobNumber = c.JobNumber });
        }

        // ─────────────────────────────────────────────────────────────
        // UPDATE (toàn bộ)
        // ─────────────────────────────────────────────────────────────
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClcDto dto)
        {
            if (await _access.RequireAsync(WritePerms) is { } denied) return denied;
            var ctx = (await _access.GetAsync())!;
            var c = await LoadAsync(id, track: true);
            if (c == null || OutOfScope(ctx, c)) return NotFound();
            if (c.Status == ClcStatus.Submitted && !ctx.IsAdmin)
                return BadRequest(new { message = "This job was already submitted and can no longer be edited." });

            // Phân vai: người tạo job request (admin/quản lý) chỉ sửa phần thông tin job khi chưa kiểm;
            // QC/kho chỉ ghi phần kiểm khi job đã giao cho mình.
            bool job  = CanJob(ctx)  && (ctx.IsAdmin || c.Status is ClcStatus.Draft or ClcStatus.Pending);
            bool exec = CanExec(ctx) && (ctx.IsAdmin ? c.Status != ClcStatus.Draft || CanJob(ctx) : c.Status is ClcStatus.Pending or ClcStatus.InProgress);
            if (!job && !exec)
                return BadRequest(new { message = "You cannot edit this job at its current status." });

            var err = ValidateJob(dto, requireCustomer: job);
            if (err != null) return BadRequest(new { message = err });

            string[] allow = job && exec ? Statuses
                           : job ? new[] { ClcStatus.Draft, ClcStatus.Pending }
                           : new[] { ClcStatus.InProgress, ClcStatus.Submitted };
            var wantStatus = allow.Contains(dto.Status) ? dto.Status : c.Status;
            if (wantStatus == ClcStatus.Pending && string.IsNullOrWhiteSpace(job ? dto.InspectorId : c.InspectorId))
                return BadRequest(new { message = "Please assign an inspector before sending the job request." });
            if (wantStatus == ClcStatus.Submitted)
            {
                var missing = SubmitProblems(dto, c);
                if (missing.Count > 0)
                    return BadRequest(new { message = "Cannot submit: " + string.Join("; ", missing) });
            }

            Apply(c, dto, ctx, job, exec, allowStatus: allow);
            c.UpdatedAt = DateTime.UtcNow;
            if (c.Status == ClcStatus.Submitted && c.SubmittedAt == null) c.SubmittedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = c.Id, status = c.Status, overallResult = c.OverallResult });
        }

        // ─────────────────────────────────────────────────────────────
        // SIGN
        // ─────────────────────────────────────────────────────────────
        [HttpPost("{id:int}/sign")]
        public async Task<IActionResult> Sign(int id, [FromBody] ClcSignRequest req)
        {
            if (await _access.RequireAsync(WritePerms) is { } denied) return denied;
            var ctx = (await _access.GetAsync())!;
            var slot = (req.Slot ?? "").Trim().ToLowerInvariant();
            if (slot != "inspector" && slot != "reviewer") return BadRequest(new { message = "Invalid signature slot." });
            if (string.IsNullOrWhiteSpace(req.SignatureDataUrl) || !req.SignatureDataUrl.StartsWith("data:image/png;base64,"))
                return BadRequest(new { message = "Signature is required." });

            var c = await _db.ContainerChecks.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || OutOfScope(ctx, c)) return NotFound();
            // Inspector ký khi thực hiện kiểm; Reviewer là người quản lý/admin duyệt
            if (slot == "inspector" ? !(CanExec(ctx) && (ctx.IsAdmin || c.Status is ClcStatus.Pending or ClcStatus.InProgress)) : !CanJob(ctx))
                return StatusCode(403, new { message = "You do not have permission to sign this slot." });

            byte[] bytes;
            try { bytes = Convert.FromBase64String(req.SignatureDataUrl["data:image/png;base64,".Length..]); }
            catch { return BadRequest(new { message = "Invalid signature image." }); }
            if (bytes.Length > 2 * 1024 * 1024) return BadRequest(new { message = "Signature image is too large." });

            await using var ms = new MemoryStream(bytes);
            var url = await _blob.UploadAsync("signatures", $"{c.JobNumber}_{slot}_{Guid.NewGuid():N}.png", ms, "image/png");

            var name = req.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = string.IsNullOrWhiteSpace(ctx.User.FullName) ? ctx.User.UserName ?? "" : ctx.User.FullName.Trim();
            if (name.Length > 150) name = name[..150];
            var position = string.IsNullOrWhiteSpace(req.Position) ? null : req.Position.Trim();
            if (position != null && position.Length > 100) position = position[..100];

            string? oldUrl;
            var now = DateTime.UtcNow;
            if (slot == "inspector")
            {
                oldUrl = c.InspectorSignatureUrl;
                c.InspectorSignatureUrl = url; c.InspectorSignName = name; c.InspectorSignPosition = position; c.InspectorSignedAt = now;
            }
            else
            {
                oldUrl = c.ReviewerSignatureUrl;
                c.ReviewerSignatureUrl = url; c.ReviewerSignName = name; c.ReviewerSignPosition = position; c.ReviewerSignedAt = now;
            }
            c.UpdatedAt = now;
            await _db.SaveChangesAsync();

            if (_blob.IsOwnFile(oldUrl, "signatures"))
                await _blob.DeleteAsync("signatures", Path.GetFileName(oldUrl));

            return Ok(new { success = true, url, name, position, signedAt = now });
        }

        // ─────────────────────────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (await _access.RequireAdminAsync() is { } denied) return denied;
            var c = await _db.ContainerChecks.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            if (c.Status is ClcStatus.InProgress or ClcStatus.Submitted)
                return BadRequest(new { message = "Only jobs that have not been inspected can be deleted." });
            _db.ContainerChecks.Remove(c);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ═════════════════════════════════════════════════════════════
        // helpers
        // ═════════════════════════════════════════════════════════════
        static bool OutOfScope(AccessContext ctx, ContainerCheck c) =>
            (ctx.CustomerScope != null && !(c.CustomerCode != null && ctx.CustomerScope.Contains(c.CustomerCode)))
            || (ExecOnly(ctx) && (c.InspectorId != ctx.User.Id || c.Status == ClcStatus.Draft));

        /// <summary>Admin / quản lý: tạo, sửa và giao job request (module Container Loading Check, cờ Create).</summary>
        static bool CanJob(AccessContext ctx) =>
            ctx.IsAdmin || ctx.Can(ModuleKeys.ContainerLoadingCheck, PermAction.Create);
        /// <summary>QC / kho: thực hiện kiểm (module Container Loading Check, cờ Edit).</summary>
        static bool CanExec(AccessContext ctx) =>
            ctx.IsAdmin || ctx.Can(ModuleKeys.ContainerLoadingCheck, PermAction.Edit);
        static bool ExecOnly(AccessContext ctx) => !CanJob(ctx) && CanExec(ctx);

        static void HideCustomer(ClcDto d)
        {
            d.CustomerCode = null; d.CustomerName = ""; d.CustomerContact = null;
            d.CustomerPhone = null; d.CustomerEmail = null; d.CustomerAddress = null;
        }

        Task<ContainerCheck?> LoadAsync(int id, bool track)
        {
            var q = _db.ContainerChecks.Include(c => c.Points).Include(c => c.Products).Include(c => c.Photos).AsQueryable();
            if (!track) q = q.AsNoTracking();
            return q.FirstOrDefaultAsync(c => c.Id == id);
        }

        async Task<string> NextJobNumberAsync()
        {
            var prefix = $"CLC-{DateTime.UtcNow:yyyyMMdd}-";
            var count = await _db.ContainerChecks.CountAsync(c => c.JobNumber.StartsWith(prefix));
            var n = count + 1;
            // tránh trùng khi đã xoá bớt job trong ngày
            while (await _db.ContainerChecks.AnyAsync(c => c.JobNumber == $"{prefix}{n:D3}")) n++;
            return $"{prefix}{n:D3}";
        }

        static string? ValidateJob(ClcDto d, bool requireCustomer = true)
        {
            // Inspector không nhận thông tin khách hàng từ API nên không bắt buộc khi họ lưu phần kiểm
            if (requireCustomer && string.IsNullOrWhiteSpace(d.CustomerName)) return "Customer is required.";
            if (string.IsNullOrWhiteSpace(d.VendorName)) return "Supplier / Factory is required.";
            if (string.IsNullOrWhiteSpace(d.ContainerNo)) return "Container No. is required.";
            if (string.IsNullOrWhiteSpace(d.ContainerType)) return "Container Type is required.";
            if (string.IsNullOrWhiteSpace(d.PoNumber)) return "PO Number is required.";
            if (d.PlannedLoadingDate == null) return "Planned Loading Date is required.";
            foreach (var p in d.Products ?? new())
                if (string.IsNullOrWhiteSpace(p.ProductCode)) return "Every product line needs a product code.";
            return null;
        }

        /// <summary>Điều kiện để gửi duyệt: đủ 7 điểm, có seal, có chữ ký inspector.</summary>
        static List<string> SubmitProblems(ClcDto d, ContainerCheck existing)
        {
            var m = new List<string>();
            var graded = (d.Points ?? new()).Where(p => Results.Contains(p.Result ?? "")).Select(p => p.PointNo).Distinct().Count();
            if (graded < 7) m.Add($"all 7 container points must be graded ({graded}/7)");
            if (string.IsNullOrWhiteSpace(d.SealNumber)) m.Add("seal number is required");
            if (string.IsNullOrWhiteSpace(d.SealPhotoUrl)) m.Add("seal photo is required");
            if (string.IsNullOrWhiteSpace(existing.InspectorSignatureUrl)) m.Add("inspector signature is required");
            return m;
        }

        static string? Res(string? v) => Results.Contains(v ?? "") ? v : null;
        static string? Trim(string? v, int max) { v = v?.Trim(); if (string.IsNullOrEmpty(v)) return null; return v.Length > max ? v[..max] : v; }

        void Apply(ContainerCheck c, ClcDto d, AccessContext ctx, bool job, bool exec, string[] allowStatus)
        {
            if (job) ApplyJob(c, d);
            if (exec) ApplyExec(c, d);
            else if (job) { /* chỉ job request: giữ nguyên phần kiểm đã có */ }
            ApplyStatusAndResults(c, d, job, exec, allowStatus);
        }

        void ApplyJob(ContainerCheck c, ClcDto d)
        {
            c.CustomerCode = Trim(d.CustomerCode, 50); c.CustomerName = Trim(d.CustomerName, 200) ?? "";
            c.CustomerContact = Trim(d.CustomerContact, 150); c.CustomerPhone = Trim(d.CustomerPhone, 50);
            c.CustomerEmail = Trim(d.CustomerEmail, 200); c.CustomerAddress = Trim(d.CustomerAddress, 500);
            c.VendorCode = Trim(d.VendorCode, 50); c.VendorName = Trim(d.VendorName, 200) ?? "";
            c.VendorContact = Trim(d.VendorContact, 150); c.VendorPhone = Trim(d.VendorPhone, 50);
            c.VendorEmail = Trim(d.VendorEmail, 200); c.VendorAddress = Trim(d.VendorAddress, 500);

            c.ContainerNo = Trim(d.ContainerNo, 30)?.ToUpperInvariant() ?? ""; c.ContainerType = Trim(d.ContainerType, 20) ?? "";
            c.PoNumber = Trim(d.PoNumber, 100) ?? ""; c.ShipmentReference = Trim(d.ShipmentReference, 100);
            c.Destination = Trim(d.Destination, 200); c.PlannedLoadingDate = d.PlannedLoadingDate?.Date;
            c.PlannedStartTime = Trim(d.PlannedStartTime, 10); c.PlannedEndTime = Trim(d.PlannedEndTime, 10);
            c.LoadingMethod = Trim(d.LoadingMethod, 60); c.TruckNo = Trim(d.TruckNo, 50);
            c.LoadingLocation = Trim(d.LoadingLocation, 60); c.LoadingAddress = Trim(d.LoadingAddress, 500);
            c.CargoDescription = Trim(d.CargoDescription, 500);

            c.InspectorId = Trim(d.InspectorId, 450); c.InspectorName = Trim(d.InspectorName, 150);
            c.InspectionDate = d.InspectionDate?.Date;
            c.InspectionStartTime = Trim(d.InspectionStartTime, 10); c.InspectionEndTime = Trim(d.InspectionEndTime, 10);
            c.JobRemarks = Trim(d.JobRemarks, 500);

        }

        /// <summary>Phần QC/kho ghi khi thực hiện kiểm: 7 điểm, additional checks, seal, loading, ảnh.</summary>
        void ApplyExec(ContainerCheck c, ClcDto d)
        {
            // Additional checks + seal
            c.CheckClean = d.CheckClean; c.CheckNoMoisture = d.CheckNoMoisture; c.CheckNoOdor = d.CheckNoOdor;
            c.CheckSuitable = d.CheckSuitable; c.CheckDoorSealable = d.CheckDoorSealable;
            c.AdditionalRemarks = Trim(d.AdditionalRemarks, 500);
            c.SealNumber = Trim(d.SealNumber, 60); c.SealCondition = Trim(d.SealCondition, 20);
            c.SealAppliedBy = Trim(d.SealAppliedBy, 30); c.SealPhotoUrl = Trim(d.SealPhotoUrl, 500); c.SealRemarks = Trim(d.SealRemarks, 500);

            // Loading verification
            c.LoadingDate = d.LoadingDate?.Date; c.LoadingStartTime = Trim(d.LoadingStartTime, 10); c.LoadingEndTime = Trim(d.LoadingEndTime, 10);
            c.LoadingMethodActual = Trim(d.LoadingMethodActual, 60);
            c.CartonCondition = Res(d.CartonCondition); c.MarkingLabel = Res(d.MarkingLabel);
            c.PackagingProtection = Res(d.PackagingProtection); c.PalletCondition = Res(d.PalletCondition);
            c.LoadingRemarks = Trim(d.LoadingRemarks, 500); c.FinalRemarks = Trim(d.FinalRemarks, 500);
            c.OnHold = d.OnHold;

            // 7 points (thay toàn bộ, tối đa 7 dòng, mỗi PointNo 1 dòng)
            c.Points.Clear();
            foreach (var p in (d.Points ?? new()).Where(p => p.PointNo is >= 1 and <= 7).GroupBy(p => p.PointNo).Select(g => g.Last()))
                c.Points.Add(new ContainerCheckPoint
                {
                    PointNo = p.PointNo, Result = Res(p.Result), Findings = Trim(p.Findings, 300), Remarks = Trim(p.Remarks, 500),
                    PhotosJson = JsonSerializer.Serialize((p.Photos ?? new()).Where(u => !string.IsNullOrWhiteSpace(u)).Take(12).ToList()),
                });

            // Photos (thay toàn bộ)
            c.Photos.Clear();
            foreach (var ph in (d.Photos ?? new()).Where(x => !string.IsNullOrWhiteSpace(x.Url)))
            {
                var cat = ph.Category == "Additional" ? "Additional" : "Process";
                if (cat == "Process" && ph.Slot is < 1 or > 6) continue;
                c.Photos.Add(new ContainerCheckPhoto { Category = cat, Slot = cat == "Process" ? ph.Slot : 0, Url = ph.Url.Trim(), Caption = Trim(ph.Caption, 200) });
            }
        }

        /// <summary>Dòng sản phẩm: job request ghi phần kế hoạch, QC/kho ghi phần thực tế (khớp theo thứ tự dòng).</summary>
        static void ApplyProducts(ContainerCheck c, ClcDto d, bool job, bool exec)
        {
            var old = c.Products.OrderBy(p => p.SortOrder).ToList();
            var src = d.Products ?? new();
            int n = job ? src.Count : old.Count;
            var list = new List<ContainerCheckProduct>();
            for (int i = 0; i < n; i++)
            {
                var o = i < old.Count ? old[i] : null;
                var p = i < src.Count ? src[i] : null;
                var row = new ContainerCheckProduct { SortOrder = i };
                if (job && p != null)
                {
                    row.ProductCode = Trim(p.ProductCode, 100) ?? ""; row.Description = Trim(p.Description, 300);
                    row.PlannedCartons = Math.Max(0, p.PlannedCartons); row.PlannedQty = Math.Max(0, p.PlannedQty);
                    row.Unit = Trim(p.Unit, 20) ?? "pcs";
                }
                else if (o != null)
                {
                    row.ProductCode = o.ProductCode; row.Description = o.Description;
                    row.PlannedCartons = o.PlannedCartons; row.PlannedQty = o.PlannedQty; row.Unit = o.Unit;
                }
                if (exec && p != null)
                {
                    row.ActualCartons = p.ActualCartons is < 0 ? 0 : p.ActualCartons; row.ActualQty = p.ActualQty is < 0 ? 0 : p.ActualQty;
                    row.Result = Res(p.Result);
                }
                else if (o != null) { row.ActualCartons = o.ActualCartons; row.ActualQty = o.ActualQty; row.Result = o.Result; }
                list.Add(row);
            }
            c.Products.Clear();
            foreach (var r in list) c.Products.Add(r);
        }

        void ApplyStatusAndResults(ContainerCheck c, ClcDto d, bool job, bool exec, string[] allowStatus)
        {
            ApplyProducts(c, d, job, exec);

            // Status
            if (Statuses.Contains(d.Status) && allowStatus.Contains(d.Status)) c.Status = d.Status;

            // Kết quả tổng hợp
            c.SevenPointResult = GroupResult(c.Points.Select(p => p.Result), expected: 7);
            var adds = new[] { c.CheckClean, c.CheckNoMoisture, c.CheckNoOdor, c.CheckSuitable, c.CheckDoorSealable };
            c.AdditionalResult = adds.Any(a => a == false) ? ClcResult.Fail : adds.All(a => a == true) ? ClcResult.Pass : null;
            c.LoadingResult = GroupResult(c.Products.Select(p => p.Result).Concat(new[] { c.CartonCondition, c.MarkingLabel, c.PackagingProtection, c.PalletCondition }), expected: 0);
            c.SealResult = c.SealCondition == "Damaged" ? ClcResult.Fail : c.SealCondition == "Intact" ? ClcResult.Pass : null;
            var parts = new[] { c.SevenPointResult, c.AdditionalResult, c.LoadingResult, c.SealResult };
            c.OverallResult = parts.Any(p => p == ClcResult.Fail) ? "FAIL"
                            : c.OnHold ? "HOLD"
                            : parts.All(p => p != null) ? "PASS" : null;

            // Chữ ký chỉ do endpoint /sign ghi; lưu kèm khi client gửi URL cũ giữ nguyên — không ghi đè tại đây.
        }

        /// <summary>Fail nếu có Fail; Pass nếu đã chấm đủ (expected>0: cần đủ số mục) và có ít nhất 1 Pass; N/A nếu toàn N/A; null nếu chưa chấm đủ.</summary>
        static string? GroupResult(IEnumerable<string?> results, int expected)
        {
            var list = results.ToList();
            var graded = list.Where(r => r != null).ToList();
            if (graded.Any(r => r == ClcResult.Fail)) return ClcResult.Fail;
            if (graded.Count == 0) return null;
            if (expected > 0 && graded.Count < expected) return null;
            if (graded.All(r => r == ClcResult.NA)) return ClcResult.NA;
            return ClcResult.Pass;
        }

        static ClcDto ToDto(ContainerCheck c) => new()
        {
            Id = c.Id, JobNumber = c.JobNumber, Status = c.Status,
            CustomerCode = c.CustomerCode, CustomerName = c.CustomerName, CustomerContact = c.CustomerContact,
            CustomerPhone = c.CustomerPhone, CustomerEmail = c.CustomerEmail, CustomerAddress = c.CustomerAddress,
            VendorCode = c.VendorCode, VendorName = c.VendorName, VendorContact = c.VendorContact,
            VendorPhone = c.VendorPhone, VendorEmail = c.VendorEmail, VendorAddress = c.VendorAddress,
            ContainerNo = c.ContainerNo, ContainerType = c.ContainerType, PoNumber = c.PoNumber,
            ShipmentReference = c.ShipmentReference, Destination = c.Destination, PlannedLoadingDate = c.PlannedLoadingDate,
            PlannedStartTime = c.PlannedStartTime, PlannedEndTime = c.PlannedEndTime, LoadingMethod = c.LoadingMethod,
            TruckNo = c.TruckNo, LoadingLocation = c.LoadingLocation, LoadingAddress = c.LoadingAddress, CargoDescription = c.CargoDescription,
            InspectorId = c.InspectorId, InspectorName = c.InspectorName, InspectionDate = c.InspectionDate,
            InspectionStartTime = c.InspectionStartTime, InspectionEndTime = c.InspectionEndTime, JobRemarks = c.JobRemarks,
            SevenPointResult = c.SevenPointResult, AdditionalResult = c.AdditionalResult, LoadingResult = c.LoadingResult,
            SealResult = c.SealResult, OverallResult = c.OverallResult, OnHold = c.OnHold,
            CheckClean = c.CheckClean, CheckNoMoisture = c.CheckNoMoisture, CheckNoOdor = c.CheckNoOdor,
            CheckSuitable = c.CheckSuitable, CheckDoorSealable = c.CheckDoorSealable, AdditionalRemarks = c.AdditionalRemarks,
            SealNumber = c.SealNumber, SealCondition = c.SealCondition, SealAppliedBy = c.SealAppliedBy,
            SealPhotoUrl = c.SealPhotoUrl, SealRemarks = c.SealRemarks,
            LoadingDate = c.LoadingDate, LoadingStartTime = c.LoadingStartTime, LoadingEndTime = c.LoadingEndTime,
            LoadingMethodActual = c.LoadingMethodActual, CartonCondition = c.CartonCondition, MarkingLabel = c.MarkingLabel,
            PackagingProtection = c.PackagingProtection, PalletCondition = c.PalletCondition,
            LoadingRemarks = c.LoadingRemarks, FinalRemarks = c.FinalRemarks,
            InspectorSignatureUrl = c.InspectorSignatureUrl, InspectorSignName = c.InspectorSignName,
            InspectorSignPosition = c.InspectorSignPosition, InspectorSignedAt = c.InspectorSignedAt,
            ReviewerSignatureUrl = c.ReviewerSignatureUrl, ReviewerSignName = c.ReviewerSignName,
            ReviewerSignPosition = c.ReviewerSignPosition, ReviewerSignedAt = c.ReviewerSignedAt,
            CreatedAt = c.CreatedAt, SubmittedAt = c.SubmittedAt,
            Products = c.Products.OrderBy(p => p.SortOrder).Select(p => new ClcProductDto
            {
                ProductCode = p.ProductCode, Description = p.Description, PlannedCartons = p.PlannedCartons, PlannedQty = p.PlannedQty,
                Unit = p.Unit, ActualCartons = p.ActualCartons, ActualQty = p.ActualQty, Result = p.Result
            }).ToList(),
            Points = c.Points.OrderBy(p => p.PointNo).Select(p => new ClcPointDto
            {
                PointNo = p.PointNo, Result = p.Result, Findings = p.Findings, Remarks = p.Remarks,
                Photos = string.IsNullOrEmpty(p.PhotosJson) ? new() : (JsonSerializer.Deserialize<List<string>>(p.PhotosJson) ?? new())
            }).ToList(),
            Photos = c.Photos.OrderBy(p => p.Category).ThenBy(p => p.Slot).ThenBy(p => p.Id).Select(p => new ClcPhotoDto
            {
                Category = p.Category, Slot = p.Slot, Url = p.Url, Caption = p.Caption
            }).ToList(),
        };

        // ═════════════════════════════════════════════════════════════
        // DTO
        // ═════════════════════════════════════════════════════════════
        public class ClcDto
        {
            public int Id { get; set; }
            public string JobNumber { get; set; } = "";
            public string Status { get; set; } = ClcStatus.Draft;

            public string? CustomerCode { get; set; }  public string CustomerName { get; set; } = "";
            public string? CustomerContact { get; set; } public string? CustomerPhone { get; set; }
            public string? CustomerEmail { get; set; }  public string? CustomerAddress { get; set; }
            public string? VendorCode { get; set; }    public string VendorName { get; set; } = "";
            public string? VendorContact { get; set; }  public string? VendorPhone { get; set; }
            public string? VendorEmail { get; set; }    public string? VendorAddress { get; set; }

            public string ContainerNo { get; set; } = ""; public string ContainerType { get; set; } = "";
            public string PoNumber { get; set; } = "";    public string? ShipmentReference { get; set; }
            public string? Destination { get; set; }      public DateTime? PlannedLoadingDate { get; set; }
            public string? PlannedStartTime { get; set; } public string? PlannedEndTime { get; set; }
            public string? LoadingMethod { get; set; }    public string? TruckNo { get; set; }
            public string? LoadingLocation { get; set; }  public string? LoadingAddress { get; set; }
            public string? CargoDescription { get; set; }

            public string? InspectorId { get; set; }      public string? InspectorName { get; set; }
            public DateTime? InspectionDate { get; set; }
            public string? InspectionStartTime { get; set; } public string? InspectionEndTime { get; set; }
            public string? JobRemarks { get; set; }

            public string? SevenPointResult { get; set; } public string? AdditionalResult { get; set; }
            public string? LoadingResult { get; set; }    public string? SealResult { get; set; }
            public string? OverallResult { get; set; }
            public bool OnHold { get; set; }

            public bool? CheckClean { get; set; }         public bool? CheckNoMoisture { get; set; }
            public bool? CheckNoOdor { get; set; }        public bool? CheckSuitable { get; set; }
            public bool? CheckDoorSealable { get; set; }  public string? AdditionalRemarks { get; set; }

            public string? SealNumber { get; set; }       public string? SealCondition { get; set; }
            public string? SealAppliedBy { get; set; }    public string? SealPhotoUrl { get; set; }
            public string? SealRemarks { get; set; }

            public DateTime? LoadingDate { get; set; }
            public string? LoadingStartTime { get; set; } public string? LoadingEndTime { get; set; }
            public string? LoadingMethodActual { get; set; }
            public string? CartonCondition { get; set; }  public string? MarkingLabel { get; set; }
            public string? PackagingProtection { get; set; } public string? PalletCondition { get; set; }
            public string? LoadingRemarks { get; set; }   public string? FinalRemarks { get; set; }

            public string? InspectorSignatureUrl { get; set; } public string? InspectorSignName { get; set; }
            public string? InspectorSignPosition { get; set; } public DateTime? InspectorSignedAt { get; set; }
            public string? ReviewerSignatureUrl { get; set; }  public string? ReviewerSignName { get; set; }
            public string? ReviewerSignPosition { get; set; }  public DateTime? ReviewerSignedAt { get; set; }

            public DateTime? CreatedAt { get; set; }      public DateTime? SubmittedAt { get; set; }

            public List<ClcProductDto> Products { get; set; } = new();
            public List<ClcPointDto>   Points   { get; set; } = new();
            public List<ClcPhotoDto>   Photos   { get; set; } = new();
        }

        public class ClcProductDto
        {
            public string ProductCode { get; set; } = ""; public string? Description { get; set; }
            public int PlannedCartons { get; set; }       public int PlannedQty { get; set; }
            public string? Unit { get; set; } = "pcs";
            public int? ActualCartons { get; set; }       public int? ActualQty { get; set; }
            public string? Result { get; set; }
        }

        public class ClcPointDto
        {
            public int PointNo { get; set; }  public string? Result { get; set; }
            public string? Findings { get; set; } public string? Remarks { get; set; }
            public List<string> Photos { get; set; } = new();
        }

        public class ClcPhotoDto
        {
            public string Category { get; set; } = "Process"; public int Slot { get; set; }
            public string Url { get; set; } = "";             public string? Caption { get; set; }
        }

        public class ClcSignRequest
        {
            public string? Slot { get; set; }  public string? Name { get; set; }  public string? Position { get; set; }
            public string? SignatureDataUrl { get; set; }
        }
    }
}
