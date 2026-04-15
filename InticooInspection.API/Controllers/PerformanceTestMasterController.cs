// ============================================================
//  Đặt tại: InticooInspection.API/Controllers/PerformanceTestMasterController.cs
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

    // GET /api/performance-test-master
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category   = null,
        [FromQuery] bool    activeOnly = true)
    {
        try
        {
            var q = _db.PerformanceTestMasters.AsQueryable();
            if (activeOnly)        q = q.Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(x => x.Category == category);

            var rows = await q
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new PerformanceTestMasterDto(
                    x.Id, x.Category, x.ProtocolName,
                    x.TestProtocol, x.Procedure, x.Requirements,
                    x.SortOrder, x.IsActive))
                .ToListAsync();

            return Ok(rows);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // GET /api/performance-test-master/{id}
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var x = await _db.PerformanceTestMasters.FindAsync(id);
        if (x == null) return NotFound();
        return Ok(ToDto(x));
    }

    // POST /api/performance-test-master
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTestMasterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var entity = FromRequest(req);
        _db.PerformanceTestMasters.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    // PUT /api/performance-test-master/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertTestMasterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var entity = await _db.PerformanceTestMasters.FindAsync(id);
        if (entity == null) return NotFound();
        ApplyRequest(entity, req);
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    // DELETE /api/performance-test-master/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.PerformanceTestMasters.FindAsync(id);
        if (entity == null) return NotFound();
        _db.PerformanceTestMasters.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PATCH /api/performance-test-master/{id}/toggle-active
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

    // ── Helpers ──
    private static PerformanceTestMasterDto ToDto(PerformanceTestMaster x) =>
        new(x.Id, x.Category, x.ProtocolName,
            x.TestProtocol, x.Procedure, x.Requirements,
            x.SortOrder, x.IsActive);

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
public record PerformanceTestMasterDto(
    int     Id,
    string  Category,
    string  ProtocolName,
    string? TestProtocol,
    string? Procedure,
    string? Requirements,
    int     SortOrder,
    bool    IsActive
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
