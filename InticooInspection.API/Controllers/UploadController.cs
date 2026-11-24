using InticooInspection.API.Helpers;
using InticooInspection.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/upload")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly AzureBlobService _blobService;

        public UploadController(AzureBlobService blobService)
        {
            _blobService = blobService;
        }

        // POST api/upload/photo
        // Nhận file ảnh, nén về ~300-400 KB, upload lên Azure Blob Storage, trả về URL
        [HttpPost("photo")]
        [AllowAnonymous]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            if (!ImageCompressor.IsImage(file.ContentType))
                return BadRequest(new { error = "Only JPG, PNG or WEBP images are allowed." });

            // Nén ảnh về ~300-400 KB
            await using var inputStream = file.OpenReadStream();
            MemoryStream compressed;
            string ext;
            try
            {
                (compressed, ext) = await ImageCompressor.CompressAsync(inputStream);
            }
            catch (ImageFormatException)
            {
                return BadRequest(new { error = "Only JPG, PNG or WEBP images are allowed." });
            }

            // Tên file luôn là .jpg sau khi nén
            var baseName = Path.GetFileNameWithoutExtension(file.FileName);
            var fileName = $"{baseName}_{Guid.NewGuid():N}{ext}";

            // Upload lên Azure Blob Storage
            var url = await _blobService.UploadAsync("photos", fileName, compressed, "image/jpeg");
            await compressed.DisposeAsync();

            return Ok(new { url });
        }
    }
}
