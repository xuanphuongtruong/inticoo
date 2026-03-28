using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/vendors")]
    public class VendorController : ControllerBase
    {
        private readonly AppDbContext _db;

        public VendorController(AppDbContext db)
        {
            _db = db;
        }

        // ─────────────────────────────────────────────────────────────
        // VENDOR CRUD
        // ─────────────────────────────────────────────────────────────

        // GET api/vendors/dropdown
        [HttpGet("dropdown")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDropdown()
        {
            var items = await _db.Vendors
                .Where(v => v.Status == VendorStatus.Active)
                .OrderBy(v => v.Code)
                .Select(v => new { v.Code, v.Name })
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? type,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _db.Vendors
                           .Include(v => v.Attachments)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v =>
                    v.Code.Contains(search) ||
                    v.Name.Contains(search) ||
                    (v.ShortName    != null && v.ShortName.Contains(search)) ||
                    (v.TaxCode      != null && v.TaxCode.Contains(search)) ||
                    (v.ContactName  != null && v.ContactName.Contains(search)) ||
                    (v.ContactPhone != null && v.ContactPhone.Contains(search)));

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<VendorType>(type, out var vType))
                query = query.Where(v => v.Type == vType);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VendorStatus>(status, out var vStatus))
                query = query.Where(v => v.Status == vStatus);

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(v => v.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => ToDto(v))
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var v = await _db.Vendors
                             .Include(x => x.Attachments)
                             .FirstOrDefaultAsync(x => x.Id == id);
            return v == null ? NotFound() : Ok(ToDto(v));
        }

        [HttpGet("next-code")]
        [AllowAnonymous]
        public async Task<IActionResult> GetNextCode()
        {
            var last = await _db.Vendors
                .Where(v => v.Code.StartsWith("VP"))
                .OrderByDescending(v => v.Code)
                .FirstOrDefaultAsync();
            int next = 100001;
            if (last != null && last.Code.Length > 2)
                if (int.TryParse(last.Code.Substring(2), out int num)) next = num + 1;
            return Ok(new { code = $"VP{next}" });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VendorRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Vendor name is required." });

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                var last = await _db.Vendors
                    .Where(v => v.Code.StartsWith("VP"))
                    .OrderByDescending(v => v.Code)
                    .FirstOrDefaultAsync();
                int next = 100001;
                if (last != null && last.Code.Length > 2)
                    if (int.TryParse(last.Code.Substring(2), out int num)) next = num + 1;
                request.Code = $"VP{next}";
            }

            if (await _db.Vendors.AnyAsync(v => v.Code == request.Code))
                return BadRequest(new { message = "Vendor code already exists." });

            var vendor = MapToVendor(new Vendor { CreatedAt = DateTime.UtcNow }, request);
            _db.Vendors.Add(vendor);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = vendor.Id, code = vendor.Code });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] VendorRequest request)
        {
            var vendor = await _db.Vendors.FindAsync(id);
            if (vendor == null) return NotFound();
            if (string.IsNullOrWhiteSpace(request.Code))  return BadRequest(new { message = "Vendor code is required." });
            if (string.IsNullOrWhiteSpace(request.Name))  return BadRequest(new { message = "Vendor name is required." });
            if (await _db.Vendors.AnyAsync(v => v.Code == request.Code && v.Id != id))
                return BadRequest(new { message = "Vendor code already exists." });

            MapToVendor(vendor, request);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var vendor = await _db.Vendors
                                  .Include(v => v.Attachments)
                                  .FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null) return NotFound();

            // Delete all physical files
            foreach (var att in vendor.Attachments)
                DeletePhysicalFile(id, att.StoredName);

            _db.Vendors.Remove(vendor);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────
        // ATTACHMENTS – multiple files per vendor
        // ─────────────────────────────────────────────────────────────

        /// <summary>Upload one or more files to a vendor.</summary>
        [HttpPost("{id}/attachments")]
        public async Task<IActionResult> UploadAttachments(int id, [FromForm] List<IFormFile> files)
        {
            var vendor = await _db.Vendors
                                  .Include(v => v.Attachments)
                                  .FirstOrDefaultAsync(v => v.Id == id);
            if (vendor == null) return NotFound();
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files uploaded." });

            var uploads = GetUploadDir(id);
            var added = new List<object>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var storedName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath   = Path.Combine(uploads, storedName);
                await using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                var att = new VendorAttachment
                {
                    VendorId     = id,
                    StoredName   = storedName,
                    OriginalName = file.FileName,
                    ContentType  = file.ContentType,
                    FileSize     = file.Length,
                    UploadedAt   = DateTime.UtcNow
                };
                vendor.Attachments.Add(att);
                await _db.SaveChangesAsync();         // get att.Id

                added.Add(new
                {
                    id           = att.Id,
                    originalName = att.OriginalName,
                    contentType  = att.ContentType,
                    fileSize     = att.FileSize,
                    uploadedAt   = att.UploadedAt,
                    downloadUrl  = $"api/vendors/{id}/attachments/{att.Id}/download"
                });
            }

            return Ok(new { success = true, files = added });
        }

        /// <summary>List all attachments for a vendor.</summary>
        [HttpGet("{id}/attachments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAttachments(int id)
        {
            var atts = await _db.Set<VendorAttachment>()
                                .Where(a => a.VendorId == id)
                                .OrderBy(a => a.UploadedAt)
                                .ToListAsync();

            var result = atts.Select(a => new
            {
                id           = a.Id,
                originalName = a.OriginalName,
                contentType  = a.ContentType,
                fileSize     = a.FileSize,
                uploadedAt   = a.UploadedAt,
                downloadUrl  = $"api/vendors/{id}/attachments/{a.Id}/download"
            });
            return Ok(result);
        }

        /// <summary>Download a single attachment.</summary>
        [HttpGet("{id}/attachments/{attId}/download")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadAttachment(int id, int attId)
        {
            var att = await _db.Set<VendorAttachment>()
                               .FirstOrDefaultAsync(a => a.Id == attId && a.VendorId == id);
            if (att == null) return NotFound();

            var filePath = Path.Combine(GetUploadDir(id), att.StoredName);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, att.ContentType, att.OriginalName);
        }

        /// <summary>Delete a single attachment.</summary>
        [HttpDelete("{id}/attachments/{attId}")]
        public async Task<IActionResult> DeleteAttachment(int id, int attId)
        {
            var att = await _db.Set<VendorAttachment>()
                               .FirstOrDefaultAsync(a => a.Id == attId && a.VendorId == id);
            if (att == null) return NotFound();

            DeletePhysicalFile(id, att.StoredName);
            _db.Set<VendorAttachment>().Remove(att);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────
        // Legacy single-file endpoints (kept for backward compatibility)
        // ─────────────────────────────────────────────────────────────

        [HttpPost("{id}/upload")]
        public async Task<IActionResult> Upload(int id, IFormFile file)
        {
            var vendor = await _db.Vendors.FindAsync(id);
            if (vendor == null) return NotFound();
            if (file == null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });

            var uploads   = GetUploadDir(id);
            var fileName  = $"{id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath  = Path.Combine(uploads, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            vendor.AttachmentPath = $"api/vendors/{id}/download";
            vendor.AttachmentName = file.FileName;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, path = vendor.AttachmentPath, name = vendor.AttachmentName });
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(int id)
        {
            var vendor = await _db.Vendors.FindAsync(id);
            if (vendor == null || string.IsNullOrEmpty(vendor.AttachmentName)) return NotFound();

            var uploads = GetUploadDir(id);
            var files   = Directory.GetFiles(uploads, $"{id}_*");
            if (files.Length == 0) return NotFound();

            var filePath    = files[0];
            var ext         = Path.GetExtension(filePath).ToLower();
            var contentType = ext switch
            {
                ".pdf"             => "application/pdf",
                ".jpg" or ".jpeg"  => "image/jpeg",
                ".png"             => "image/png",
                _                  => "application/octet-stream"
            };
            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, contentType, vendor.AttachmentName);
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private string GetUploadDir(int vendorId)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "vendors", vendorId.ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void DeletePhysicalFile(int vendorId, string storedName)
        {
            var path = Path.Combine(GetUploadDir(vendorId), storedName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        private static VendorDto ToDto(Vendor v) => new()
        {
            Id             = v.Id,
            Code           = v.Code,
            Name           = v.Name,
            ShortName      = v.ShortName,
            Type           = (int)v.Type,
            Status         = (int)v.Status,
            TaxCode        = v.TaxCode,
            BusinessRegNo  = v.BusinessRegNo,
            Website        = v.Website,
            Notes          = v.Notes,
            ContactName    = v.ContactName,
            ContactTitle   = v.ContactTitle,
            ContactPhone   = v.ContactPhone,
            ContactEmail   = v.ContactEmail,
            CompanyAddress = v.CompanyAddress,
            BillingAddress = v.BillingAddress,
            City           = v.City,
            Country        = v.Country,
            AttachmentPath = v.AttachmentPath,
            AttachmentName = v.AttachmentName,
            CreatedAt      = v.CreatedAt,
            Attachments    = v.Attachments.Select(a => new AttachmentDto
            {
                Id           = a.Id,
                OriginalName = a.OriginalName,
                ContentType  = a.ContentType,
                FileSize     = a.FileSize,
                UploadedAt   = a.UploadedAt,
                DownloadUrl  = $"api/vendors/{v.Id}/attachments/{a.Id}/download"
            }).ToList()
        };

        private static Vendor MapToVendor(Vendor v, VendorRequest r)
        {
            v.Code           = r.Code;
            v.Name           = r.Name;
            v.ShortName      = r.ShortName;
            v.Type           = (VendorType)r.Type;
            v.Status         = (VendorStatus)r.Status;
            v.TaxCode        = r.TaxCode;
            v.BusinessRegNo  = r.BusinessRegNo;
            v.Website        = r.Website;
            v.Notes          = r.Notes;
            v.ContactName    = r.ContactName;
            v.ContactTitle   = r.ContactTitle;
            v.ContactPhone   = r.ContactPhone;
            v.ContactEmail   = r.ContactEmail;
            v.CompanyAddress = r.CompanyAddress;
            v.BillingAddress = r.BillingAddress;
            v.City           = r.City;
            v.Country        = r.Country;
            return v;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // DTOs / Request models
    // ─────────────────────────────────────────────────────────────────

    public class VendorRequest
    {
        public string  Code           { get; set; } = "";
        public string  Name           { get; set; } = "";
        public string? ShortName      { get; set; }
        public int     Type           { get; set; } = 0;
        public int     Status         { get; set; } = 0;
        public string? TaxCode        { get; set; }
        public string? BusinessRegNo  { get; set; }
        public string? Website        { get; set; }
        public string? Notes          { get; set; }
        public string? ContactName    { get; set; }
        public string? ContactTitle   { get; set; }
        public string? ContactPhone   { get; set; }
        public string? ContactEmail   { get; set; }
        public string? CompanyAddress { get; set; }
        public string? BillingAddress { get; set; }
        public string? City           { get; set; }
        public string? Country        { get; set; }
    }

    public class AttachmentDto
    {
        public int      Id           { get; set; }
        public string   OriginalName { get; set; } = "";
        public string   ContentType  { get; set; } = "";
        public long     FileSize     { get; set; }
        public DateTime UploadedAt   { get; set; }
        public string   DownloadUrl  { get; set; } = "";
    }

    public class VendorDto
    {
        public int              Id             { get; set; }
        public string           Code           { get; set; } = "";
        public string           Name           { get; set; } = "";
        public string?          ShortName      { get; set; }
        public int              Type           { get; set; }
        public int              Status         { get; set; }
        public string?          TaxCode        { get; set; }
        public string?          BusinessRegNo  { get; set; }
        public string?          Website        { get; set; }
        public string?          Notes          { get; set; }
        public string?          ContactName    { get; set; }
        public string?          ContactTitle   { get; set; }
        public string?          ContactPhone   { get; set; }
        public string?          ContactEmail   { get; set; }
        public string?          CompanyAddress { get; set; }
        public string?          BillingAddress { get; set; }
        public string?          City           { get; set; }
        public string?          Country        { get; set; }
        public string?          AttachmentPath { get; set; }
        public string?          AttachmentName { get; set; }
        public DateTime         CreatedAt      { get; set; }
        public List<AttachmentDto> Attachments { get; set; } = new();
    }
}
