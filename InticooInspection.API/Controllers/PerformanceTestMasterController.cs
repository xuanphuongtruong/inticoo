// ============================================================
//  Đặt tại: InticooInspection.API/Controllers/PerformanceTestMasterController.cs
//  ----------------------------------------------------------
//  Đã chuyển từ "1 file/master" sang "N file/master".
//  Reference giờ được quản lý qua bảng con PerformanceTestReferences.
// ============================================================

using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers;

[ApiController]
[Route("api/performance-test-master")]
public class PerformanceTestMasterController : ControllerBase
{
    private readonly AppDbContext _db;
    public PerformanceTestMasterController(AppDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────────────────────
    // GET /api/performance-test-master
    //   Trả meta + danh sách Reference (KHÔNG kèm BLOB)
    // ─────────────────────────────────────────────────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category   = null,
        [FromQuery] bool    activeOnly = true)
    {
        try
        {
            var q = _db.PerformanceTestMasters
                       .Include(x => x.References)
                       .AsQueryable();

            if (activeOnly) q = q.Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(x => x.Category == category);

            var rows = await q
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new PerformanceTestMasterDto(
                    x.Id, x.Category, x.ProtocolName,
                    x.TestProtocol, x.Procedure, x.Requirements,
                    x.SortOrder, x.IsActive,
                    x.References
                        .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
                        .Select(r => new ReferenceFileDto(
                            r.Id, r.FileName, r.ContentType, r.FileSize, r.SortOrder, r.UploadedAt))
                        .ToList()))
                .ToListAsync();

            return Ok(rows);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET /api/performance-test-master/{id}
    // ─────────────────────────────────────────────────────────────────────
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var x = await _db.PerformanceTestMasters
                         .Include(m => m.References)
                         .FirstOrDefaultAsync(m => m.Id == id);
        if (x == null) return NotFound();
        return Ok(ToDto(x));
    }

    // ─────────────────────────────────────────────────────────────────────
    // POST /api/performance-test-master
    // ─────────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTestMasterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var entity = FromRequest(req);
        _db.PerformanceTestMasters.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUT /api/performance-test-master/{id}
    // ─────────────────────────────────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertTestMasterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var entity = await _db.PerformanceTestMasters
                              .Include(m => m.References)
                              .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) return NotFound();
        ApplyRequest(entity, req);
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    // ─────────────────────────────────────────────────────────────────────
    // DELETE /api/performance-test-master/{id}
    //   FK đã ON DELETE CASCADE → các Reference con tự xoá theo.
    // ─────────────────────────────────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.PerformanceTestMasters.FindAsync(id);
        if (entity == null) return NotFound();
        _db.PerformanceTestMasters.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────
    // PATCH /api/performance-test-master/{id}/toggle-active
    // ─────────────────────────────────────────────────────────────────────
    [HttpPatch("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var entity = await _db.PerformanceTestMasters.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsActive  = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { id = entity.Id, isActive = entity.IsActive });
    }

    // ════════════════════════════════════════════════════════════════════
    //  REFERENCE FILES (multi)  — Upload / List / Download / Delete
    // ════════════════════════════════════════════════════════════════════

    // GET /api/performance-test-master/{id}/references
    //   Liệt kê các file (chỉ meta) thuộc 1 master.
    [HttpGet("{id:int}/references")]
    [AllowAnonymous]
    public async Task<IActionResult> ListReferences(int id)
    {
        var exists = await _db.PerformanceTestMasters.AnyAsync(m => m.Id == id);
        if (!exists) return NotFound();

        var list = await _db.PerformanceTestReferences
            .Where(r => r.PerformanceTestMasterId == id)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .Select(r => new ReferenceFileDto(
                r.Id, r.FileName, r.ContentType, r.FileSize, r.SortOrder, r.UploadedAt))
            .ToListAsync();

        return Ok(list);
    }

    // POST /api/performance-test-master/{id}/references  (multipart/form-data)
    //   Upload 1 hoặc nhiều file trong cùng 1 request (field name: "files").
    [HttpPost("{id:int}/references")]
    [RequestSizeLimit(200_000_000)] // 200 MB tổng / 1 request
    public async Task<IActionResult> UploadReferences(int id, [FromForm] List<IFormFile> files)
    {
        var master = await _db.PerformanceTestMasters.FindAsync(id);
        if (master == null) return NotFound();

        if (files == null || files.Count == 0)
            return BadRequest(new { error = "No files uploaded." });

        var nextSort = await _db.PerformanceTestReferences
            .Where(r => r.PerformanceTestMasterId == id)
            .Select(r => (int?)r.SortOrder).MaxAsync() ?? -1;

        foreach (var file in files)
        {
            if (file == null || file.Length == 0) continue;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            nextSort++;
            var entity = new PerformanceTestReference
            {
                PerformanceTestMasterId = id,
                FileName    = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                              ? "application/octet-stream"
                              : file.ContentType,
                FileData    = ms.ToArray(),
                FileSize    = file.Length,
                SortOrder   = nextSort,
                UploadedAt  = DateTime.UtcNow
            };
            _db.PerformanceTestReferences.Add(entity);
        }

        master.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Trả về toàn bộ danh sách reference hiện tại của master (đã có Id)
        var list = await _db.PerformanceTestReferences
            .Where(r => r.PerformanceTestMasterId == id)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .Select(r => new ReferenceFileDto(
                r.Id, r.FileName, r.ContentType, r.FileSize, r.SortOrder, r.UploadedAt))
            .ToListAsync();

        return Ok(list);
    }

    // GET /api/performance-test-master/references/{refId}
    //   Tải 1 file cụ thể về.
    [HttpGet("references/{refId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadReference(int refId)
    {
        var r = await _db.PerformanceTestReferences
                         .AsNoTracking()
                         .FirstOrDefaultAsync(x => x.Id == refId);
        if (r == null) return NotFound();
        if (r.FileData == null || r.FileData.Length == 0)
            return NotFound(new { error = "Empty file." });

        return File(r.FileData, r.ContentType ?? "application/octet-stream",
                    r.FileName ?? $"reference-{refId}.bin");
    }

    // DELETE /api/performance-test-master/references/{refId}
    //   Xoá 1 file đính kèm.
    [HttpDelete("references/{refId:int}")]
    public async Task<IActionResult> DeleteReference(int refId)
    {
        var r = await _db.PerformanceTestReferences.FindAsync(refId);
        if (r == null) return NotFound();

        var masterId = r.PerformanceTestMasterId;
        _db.PerformanceTestReferences.Remove(r);

        var master = await _db.PerformanceTestMasters.FindAsync(masterId);
        if (master != null) master.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Helpers ─────────────────────────────────────────────────────────
    private static PerformanceTestMasterDto ToDto(PerformanceTestMaster x) =>
        new(x.Id, x.Category, x.ProtocolName,
            x.TestProtocol, x.Procedure, x.Requirements,
            x.SortOrder, x.IsActive,
            x.References
             .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
             .Select(r => new ReferenceFileDto(
                 r.Id, r.FileName, r.ContentType, r.FileSize, r.SortOrder, r.UploadedAt))
             .ToList());

    private static PerformanceTestMaster FromRequest(UpsertTestMasterRequest r)
    {
        var e = new PerformanceTestMaster { CreatedAt = DateTime.UtcNow };
        ApplyRequest(e, r);
        return e;
    }

    private static void ApplyRequest(PerformanceTestMaster e, UpsertTestMasterRequest r)
    {
        e.Category     = r.Category.Trim();
        e.ProtocolName = r.ProtocolName.Trim();
        e.TestProtocol = r.TestProtocol?.Trim();
        e.Procedure    = r.Procedure?.Trim();
        e.Requirements = r.Requirements?.Trim();
        e.SortOrder    = r.SortOrder;
        e.IsActive     = r.IsActive;
        e.UpdatedAt    = DateTime.UtcNow;
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ReferenceFileDto(
    int      Id,
    string   FileName,
    string   ContentType,
    long     FileSize,
    int      SortOrder,
    DateTime UploadedAt
);

public record PerformanceTestMasterDto(
    int     Id,
    string  Category,
    string  ProtocolName,
    string? TestProtocol,
    string? Procedure,
    string? Requirements,
    int     SortOrder,
    bool    IsActive,
    List<ReferenceFileDto> References
);

public class UpsertTestMasterRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string  Category     { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required]
    public string  ProtocolName { get; set; } = "";
    public string? TestProtocol { get; set; }
    public string? Procedure    { get; set; }
    public string? Requirements { get; set; }
    public int     SortOrder    { get; set; } = 0;
    public bool    IsActive     { get; set; } = true;
}
