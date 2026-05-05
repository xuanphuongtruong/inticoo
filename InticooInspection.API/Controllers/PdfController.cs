using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InticooInspection.API.Services;

namespace InticooInspection.API.Controllers;

/// <summary>
/// API endpoint xuất PDF cho Inspection Report.
/// Frontend Blazor gọi: GET /api/pdf/inspection/{id}
///
/// IMPORTANT: Hiện tại đang [AllowAnonymous] để debug. Sau khi confirm hoạt động,
/// đổi thành [Authorize] hoặc [Authorize(Roles = "Admin,Inspector,...")] tuỳ
/// policy của project.
/// </summary>
[ApiController]
[Route("api/pdf")]
[AllowAnonymous]   // ⚠ TẠM THỜI để debug — đổi sau khi confirm Puppeteer hoạt động
public class PdfController : ControllerBase
{
    private readonly IPdfService _pdfService;
    private readonly ILogger<PdfController> _logger;

    public PdfController(IPdfService pdfService, ILogger<PdfController> logger)
    {
        _pdfService = pdfService;
        _logger     = logger;
    }

    [HttpGet("inspection/{id:int}")]
    public async Task<IActionResult> GenerateInspectionPdf(int id, CancellationToken ct)
    {
        try
        {
            // Forward auth token để Puppeteer load lại trang Blazor có data đầy đủ.
            // Token đến từ header Authorization của request hiện tại (frontend gửi).
            var authHeader = Request.Headers.Authorization.ToString();
            var authToken  = authHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();

            _logger.LogInformation(
                "PDF request for inspection {Id}. Auth token forwarded: {HasToken} (length={Len})",
                id, !string.IsNullOrEmpty(authToken), authToken?.Length ?? 0);

            var pdfBytes = await _pdfService.GenerateInspectionReportPdfAsync(id, authToken, ct);

            // Trả về binary PDF. Frontend đọc bytes và dùng JS Blob để trigger
            // download với filename mong muốn.
            return File(pdfBytes, "application/pdf", $"inspection-{id}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for inspection {Id}", id);
            return StatusCode(500, new { error = "PDF generation failed", detail = ex.Message });
        }
    }
}
