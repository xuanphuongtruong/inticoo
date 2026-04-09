// ============================================================
//  Đặt tại: InticooInspection.API/Controllers/PerformanceTestMasterController.cs
//  Dùng EF Core — không cần Dapper
// ============================================================

using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers;

[ApiController]
[Route("api/performance-test-master")]
[Authorize]
public class PerformanceTestMasterController : ControllerBase
{
    private readonly AppDbContext _db;

    public PerformanceTestMasterController(AppDbContext db)
        => _db = db;

    /// <summary>
    /// Load toàn bộ master data cho Performance Testing dropdown.
    /// Kết quả ~40 rows — client cache 1 lần khi page init.
    /// Query param productType (optional): lọc theo loại sản phẩm.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] string? productType = null)
    {
        try
        {
            var query = _db.PerformanceTestMasters
                .Where(x => x.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(productType))
                query = query.Where(x => x.ProductType == "All" || x.ProductType == productType);

            var rows = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => new PerformanceTestMasterDto(
                    x.Id,
                    x.Category,
                    x.StandardCode,
                    x.ProductType,
                    x.Market ?? "",
                    x.ProtocolName,
                    x.Requirements,
                    x.SortOrder
                ))
                .ToListAsync();

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// ── Response DTO ──────────────────────────────────────────────────────
public record PerformanceTestMasterDto(
    int     Id,
    string  Category,
    string  StandardCode,
    string  ProductType,
    string  Market,
    string  ProtocolName,
    string? Requirements,
    int     SortOrder
);
