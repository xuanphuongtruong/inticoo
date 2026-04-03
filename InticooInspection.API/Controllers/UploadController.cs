using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/upload")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // POST api/upload/photo
        // Nhận file ảnh, lưu vào wwwroot/uploads/photos/, trả về URL tương đối
        [HttpPost("photo")]
        [AllowAnonymous] // QC inspector có thể chưa login khi upload
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            // Chỉ chấp nhận ảnh
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { error = "Only image files are allowed." });

            // Tạo tên file unique
            var ext      = Path.GetExtension(file.FileName).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid():N}{ext}";

            // Đường dẫn lưu file
            var uploadDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "photos");
            Directory.CreateDirectory(uploadDir);
            var filePath = Path.Combine(uploadDir, fileName);

            // Lưu file
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Trả về URL tương đối — client sẽ gắn API BaseAddress vào trước
            var relativeUrl = $"uploads/photos/{fileName}";
            return Ok(new { url = relativeUrl });
        }
    }
}
