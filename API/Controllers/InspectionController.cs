using InticooInspection.Application.DTOs;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/inspections")]
    [Authorize]
    public class InspectionController : ControllerBase
    {
        private readonly AppDbContext _db;

        public InspectionController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/inspections
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var inspections = await _db.Inspections
                .Include(i => i.CreatedBy)
                .Include(i => i.Steps)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => MapToDto(i))
                .ToListAsync();

            return Ok(inspections);
        }

        // GET /api/inspections/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var inspection = await _db.Inspections
                .Include(i => i.CreatedBy)
                .Include(i => i.Steps.OrderBy(s => s.Order))
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inspection == null) return NotFound();
            return Ok(MapToDto(inspection));
        }

        // POST /api/inspections
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInspectionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var inspection = new Inspection
            {
                Title       = dto.Title,
                Description = dto.Description,
                CreatedById = userId!,
                Status      = InspectionStatus.Pending,
                Steps       = dto.Steps.Select(s => new InspectionStep
                {
                    Order       = s.Order,
                    Title       = s.Title,
                    Description = s.Description,
                    Status      = StepStatus.Pending
                }).ToList()
            };

            _db.Inspections.Add(inspection);
            await _db.SaveChangesAsync();
            return Ok(new { id = inspection.Id });
        }

        // PUT /api/inspections/{id}/steps
        [HttpPut("{id}/steps")]
        public async Task<IActionResult> UpdateStep(int id, [FromBody] UpdateStepDto dto)
        {
            var step = await _db.InspectionSteps
                .Include(s => s.Inspection)
                .FirstOrDefaultAsync(s => s.Id == dto.StepId && s.InspectionId == id);

            if (step == null) return NotFound();

            step.Status      = Enum.Parse<StepStatus>(dto.Status);
            step.Note        = dto.Note;
            step.CompletedAt = DateTime.UtcNow;

            // Cập nhật trạng thái Inspection
            var inspection = step.Inspection!;
            var allSteps = await _db.InspectionSteps.Where(s => s.InspectionId == id).ToListAsync();

            if (allSteps.All(s => s.Status != StepStatus.Pending))
            {
                inspection.Status      = allSteps.Any(s => s.Status == StepStatus.Fail)
                                         ? InspectionStatus.Completed
                                         : InspectionStatus.Completed;
                inspection.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                inspection.Status = InspectionStatus.InProgress;
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        // GET /api/inspections/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var total      = await _db.Inspections.CountAsync();
            var pending    = await _db.Inspections.CountAsync(i => i.Status == InspectionStatus.Pending);
            var inProgress = await _db.Inspections.CountAsync(i => i.Status == InspectionStatus.InProgress);
            var completed  = await _db.Inspections.CountAsync(i => i.Status == InspectionStatus.Completed);

            var recent = await _db.Inspections
                .Include(i => i.CreatedBy)
                .Include(i => i.Steps)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .Select(i => MapToDto(i))
                .ToListAsync();

            return Ok(new DashboardDto
            {
                TotalInspections      = total,
                PendingInspections    = pending,
                InProgressInspections = inProgress,
                CompletedInspections  = completed,
                CompletionRate        = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0,
                RecentInspections     = recent
            });
        }

        // ─── Helper ─────────────────────────────────────────────────────────────
        private static InspectionDto MapToDto(Inspection i) => new()
        {
            Id              = i.Id,
            Title           = i.Title,
            Description     = i.Description,
            Status          = i.Status.ToString(),
            CreatedAt       = i.CreatedAt,
            CompletedAt     = i.CompletedAt,
            CreatedByName   = i.CreatedBy?.FullName ?? "",
            TotalSteps      = i.Steps.Count,
            CompletedSteps  = i.Steps.Count(s => s.Status != StepStatus.Pending),
            Steps           = i.Steps.OrderBy(s => s.Order).Select(s => new InspectionStepDto
            {
                Id          = s.Id,
                Order       = s.Order,
                Title       = s.Title,
                Description = s.Description,
                Status      = s.Status.ToString(),
                Note        = s.Note,
                CompletedAt = s.CompletedAt
            }).ToList()
        };
    }
}
