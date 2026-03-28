using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/customers/{customerId}/files")]
    public class CustomerFilesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public CustomerFilesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db  = db;
            _env = env;
        }

        // GET api/customers/{customerId}/files
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetFiles(int customerId)
        {
            var files = await _db.CustomerFiles
                .Where(f => f.CustomerId == customerId)
                .OrderByDescending(f => f.UploadedAt)
                .Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.Url,
                    f.FileSize,
                    f.ContentType,
                    f.UploadedAt
                })
                .ToListAsync();

            return Ok(files);
        }

        // POST api/customers/{customerId}/files
        [HttpPost]
        public async Task<IActionResult> UploadFiles(int customerId, [FromForm] List<IFormFile> files)
        {
            var customer = await _db.Customers.FindAsync(customerId);
            if (customer == null) return NotFound();

            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files provided." });

            // Allowed types
            var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp" };
            var allowedMimeTypes  = new[] { "application/pdf", "image/png", "image/jpeg", "image/gif", "image/webp" };

            // Upload folder: wwwroot/uploads/customers/{customerId}/
            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadFolder = Path.Combine(wwwroot, "uploads", "customers", customerId.ToString());
            Directory.CreateDirectory(uploadFolder);

            var savedFiles = new List<object>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext)) continue;

                // Unique file name to avoid collisions
                var uniqueName = $"{Guid.NewGuid():N}{ext}";
                var fullPath   = Path.Combine(uploadFolder, uniqueName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await file.CopyToAsync(stream);

                var relativeUrl = $"/uploads/customers/{customerId}/{uniqueName}";
                var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

                var entity = new CustomerFile
                {
                    CustomerId  = customerId,
                    FileName    = file.FileName,
                    FilePath    = fullPath,
                    Url         = absoluteUrl,
                    FileSize    = file.Length,
                    ContentType = file.ContentType,
                    UploadedAt  = DateTime.UtcNow
                };

                _db.CustomerFiles.Add(entity);
                await _db.SaveChangesAsync();

                savedFiles.Add(new { entity.Id, entity.FileName, entity.Url, entity.FileSize });
            }

            return Ok(new { success = true, files = savedFiles });
        }

        // DELETE api/customers/{customerId}/files/{fileId}
        [HttpDelete("{fileId}")]
        public async Task<IActionResult> DeleteFile(int customerId, int fileId)
        {
            var file = await _db.CustomerFiles
                .FirstOrDefaultAsync(f => f.Id == fileId && f.CustomerId == customerId);

            if (file == null) return NotFound();

            // Remove physical file
            if (System.IO.File.Exists(file.FilePath))
                System.IO.File.Delete(file.FilePath);

            _db.CustomerFiles.Remove(file);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
