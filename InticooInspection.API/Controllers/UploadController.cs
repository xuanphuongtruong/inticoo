using InticooInspection.API.Helpers;
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
        // Nhận file ảnh, nén về ~300-400 KB, lưu vào wwwroot/uploads/photos/, trả về URL tương đối
        [HttpPost("photo")]
        [AllowAnonymous]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { error = "Only image files are allowed." });

            // Nén ảnh về ~300-400 KB
            await using var inputStream = file.OpenReadStream();
            var (compressed, ext) = await ImageCompressor.CompressAsync(inputStream);

            // Tên file luôn là .jpg sau khi nén
            var baseName = Path.GetFileNameWithoutExtension(file.FileName);
            var fileName = $"{baseName}_{Guid.NewGuid():N}{ext}";

            var uploadDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "photos");
            Directory.CreateDirectory(uploadDir);
            var filePath = Path.Combine(uploadDir, fileName);

            await using (var fs = new FileStream(filePath, FileMode.Create))
                await compressed.CopyToAsync(fs);

            await compressed.DisposeAsync();

            var relativeUrl = $"uploads/photos/{fileName}";
            return Ok(new { url = relativeUrl });
        }
    }
}
