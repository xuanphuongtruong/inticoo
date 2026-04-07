using InticooInspection.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/files")]
    [AllowAnonymous]
    public class FileController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedCvExtensions    = { ".pdf", ".doc", ".docx" };
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxCvSize    = 10 * 1024 * 1024;  // 10 MB
        private const long MaxImageSize =  5 * 1024 * 1024;  //  5 MB
        private const long MaxFileSize  = 20 * 1024 * 1024;  // 20 MB

        public FileController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // ── Tự tạo wwwroot + subfolder nếu chưa có ──
        private string GetUploadDir(string subFolder)
        {
            var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;
            var dir = Path.Combine(webRoot, "uploads", subFolder);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>POST api/files/upload-cv — Upload CV, trả về { success, url }</summary>
        [HttpPost("upload-cv")]
        public async Task<IActionResult> UploadCv(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file provided." });
            if (file.Length > MaxCvSize)
                return BadRequest(new { success = false, message = "File size must not exceed 10 MB." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedCvExtensions.Contains(ext))
                return BadRequest(new { success = false, message = "Only PDF, DOC or DOCX files are allowed." });

            var uniqueName = $"{Guid.NewGuid()}{ext}";
            var filePath   = Path.Combine(GetUploadDir("cv"), uniqueName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            return Ok(new { success = true, url = $"/uploads/cv/{uniqueName}" });
        }

        /// <summary>POST api/files/upload-photo — Upload ảnh, nén về ~300-400 KB, trả về { url, fileName }</summary>
        [HttpPost("upload-photo")]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });
            if (file.Length > MaxImageSize)
                return BadRequest(new { error = "Image exceeds 5 MB limit." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext))
                return BadRequest(new { error = "Only JPG, PNG, WEBP or GIF images are allowed." });

            // Nén ảnh về ~300-400 KB
            await using var inputStream = file.OpenReadStream();
            var (compressed, compressedExt) = await ImageCompressor.CompressAsync(inputStream);

            var safeName   = Path.GetFileNameWithoutExtension(file.FileName)
                                 .Replace(" ", "_").Replace("..", "");
            var uniqueName = $"{safeName}_{Guid.NewGuid():N}{compressedExt}";
            var filePath   = Path.Combine(GetUploadDir("photos"), uniqueName);

            await using (var fs = new FileStream(filePath, FileMode.Create))
                await compressed.CopyToAsync(fs);

            await compressed.DisposeAsync();

            return Ok(new { url = $"/uploads/photos/{uniqueName}", fileName = file.FileName });
        }

        /// <summary>POST api/files/upload — Upload file tham chiếu.
        /// Nếu là ảnh thì nén ~300-400 KB, trả về { url, fileName }</summary>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });
            if (file.Length > MaxFileSize)
                return BadRequest(new { error = "File exceeds 20 MB limit." });

            var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
            var safeName = Path.GetFileNameWithoutExtension(file.FileName)
                               .Replace(" ", "_").Replace("..", "");

            string uniqueName;
            string filePath;

            if (ImageCompressor.IsImageExtension(ext))
            {
                // Ảnh → nén trước khi lưu
                await using var inputStream = file.OpenReadStream();
                var (compressed, compressedExt) = await ImageCompressor.CompressAsync(inputStream);

                uniqueName = $"{safeName}_{Guid.NewGuid():N}{compressedExt}";
                filePath   = Path.Combine(GetUploadDir("references"), uniqueName);

                await using (var fs = new FileStream(filePath, FileMode.Create))
                    await compressed.CopyToAsync(fs);

                await compressed.DisposeAsync();
            }
            else
            {
                // File không phải ảnh (PDF, DOCX, ...) → lưu thẳng
                uniqueName = $"{safeName}_{Guid.NewGuid():N}{ext}";
                filePath   = Path.Combine(GetUploadDir("references"), uniqueName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
            }

            return Ok(new { url = $"/uploads/references/{uniqueName}", fileName = file.FileName });
        }
    }
}
