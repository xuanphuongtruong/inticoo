using InticooInspection.API.Helpers;
using InticooInspection.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/files")]
    [AllowAnonymous]
    public class FileController : ControllerBase
    {
        private readonly AzureBlobService _blobService;

        private static readonly string[] AllowedCvExtensions    = { ".pdf", ".doc", ".docx" };
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxCvSize    = 10 * 1024 * 1024;  // 10 MB
        private const long MaxImageSize =  5 * 1024 * 1024;  //  5 MB
        private const long MaxFileSize  = 20 * 1024 * 1024;  // 20 MB

        public FileController(AzureBlobService blobService)
        {
            _blobService = blobService;
        }

        /// <summary>POST api/files/upload-cv — Upload CV lên Azure Blob Storage, trả về { success, url }</summary>
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

            // Upload lên Azure Blob Storage
            await using var stream = file.OpenReadStream();
            var url = await _blobService.UploadAsync("cv", uniqueName, stream, file.ContentType);

            return Ok(new { success = true, url });
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

            // Upload lên Azure Blob Storage
            var url = await _blobService.UploadAsync("photos", uniqueName, compressed, "image/jpeg");
            await compressed.DisposeAsync();

            return Ok(new { url, fileName = file.FileName });
        }

        /// <summary>POST api/files/upload — Upload file tham chiếu lên Azure Blob Storage.
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
            string url;

            if (ImageCompressor.IsImageExtension(ext))
            {
                // Ảnh → nén trước khi upload
                await using var inputStream = file.OpenReadStream();
                var (compressed, compressedExt) = await ImageCompressor.CompressAsync(inputStream);

                uniqueName = $"{safeName}_{Guid.NewGuid():N}{compressedExt}";

                // Upload lên Azure Blob Storage
                url = await _blobService.UploadAsync("references", uniqueName, compressed, "image/jpeg");
                await compressed.DisposeAsync();
            }
            else
            {
                // File không phải ảnh (PDF, DOCX, ...) → upload thẳng
                uniqueName = $"{safeName}_{Guid.NewGuid():N}{ext}";

                await using var stream = file.OpenReadStream();
                url = await _blobService.UploadAsync("references", uniqueName, stream, file.ContentType);
            }

            return Ok(new { url, fileName = file.FileName });
        }
    }
}
