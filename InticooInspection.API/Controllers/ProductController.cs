using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ProductController(AppDbContext db, IWebHostEnvironment env)
        {
            _db  = db;
            _env = env;
        }

        // ── GET api/products ──────────────────────────────────────────
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? category,
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 50)
        {
            var query = _db.Products
                           .Include(p => p.Customer)
                           .Include(p => p.Vendor)
                           .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.ProductName.Contains(search) ||
                    (p.ProductCode  != null && p.ProductCode.Contains(search))  ||
                    (p.Customer     != null && p.Customer.CompanyName.Contains(search)) ||
                    (p.Vendor       != null && p.Vendor.Name.Contains(search)));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto
                {
                    Id           = p.Id,
                    CustomerId   = p.Customer != null ? p.Customer.CustomerId   : "",
                    CustomerName = p.Customer != null ? p.Customer.CompanyName  : "",
                    VendorId     = p.Vendor   != null ? p.Vendor.Code           : "",
                    VendorName   = p.Vendor   != null ? p.Vendor.Name           : "",
                    Category     = p.Category    ?? "",
                    ProductType  = p.ProductType ?? "",
                    ProductName  = p.ProductName,
                    ProductCode  = p.ProductCode  ?? "",
                    ProductColor = p.ProductColor ?? "",
                    ProductSize  = p.ProductSize  ?? "",
                    PhotoUrl     = p.PhotoUrl     ?? "",
                    Remark       = p.Remark       ?? ""
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // ── GET api/products/nextcode ─────────────────────────────────
        [HttpGet("nextcode")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetNextCode()
        {
            // Lấy tất cả code dạng PI + số, tìm số lớn nhất
            var codes = await _db.Products
                .Where(p => p.ProductCode != null && p.ProductCode.StartsWith("PI"))
                .Select(p => p.ProductCode!)
                .ToListAsync();

            int max = 0;
            foreach (var code in codes)
            {
                var numPart = code.Substring(2);
                if (int.TryParse(numPart, out int n) && n > max)
                    max = n;
            }

            var nextCode = $"PI{(max + 1):D6}";
            return Ok(new { code = nextCode });
        }

        // ── GET api/products/dropdown ─────────────────────────────────
        [HttpGet("dropdown")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetDropdown()
        {
            var items = await _db.Products
                .Include(p => p.Customer)
                .Include(p => p.Vendor)
                .OrderBy(p => p.ProductCode)
                .Select(p => new
                {
                    id          = p.Id,
                    productCode = p.ProductCode ?? "",
                    productName = p.ProductName,
                    category    = p.Category ?? "",
                    customerId  = p.Customer != null ? p.Customer.CustomerId : "",
                    vendorId    = p.Vendor   != null ? p.Vendor.Code         : ""
                })
                .ToListAsync();
            return Ok(items);
        }

        // ── GET api/products/{id} ─────────────────────────────────────
        [HttpGet("{id}")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var p = await _db.Products
                             .Include(x => x.Customer)
                             .Include(x => x.Vendor)
                             .FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            return Ok(new ProductDto
            {
                Id           = p.Id,
                CustomerId   = p.Customer?.CustomerId   ?? "",
                CustomerName = p.Customer?.CompanyName  ?? "",
                VendorId     = p.Vendor?.Code           ?? "",
                VendorName   = p.Vendor?.Name           ?? "",
                Category     = p.Category    ?? "",
                ProductType  = p.ProductType ?? "",
                ProductName  = p.ProductName,
                ProductCode  = p.ProductCode  ?? "",
                ProductColor = p.ProductColor ?? "",
                ProductSize  = p.ProductSize  ?? "",
                PhotoUrl     = p.PhotoUrl     ?? "",
                Remark       = p.Remark       ?? ""
            });
        }

        // ── POST api/products ─────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ProductName))
                return BadRequest(new { message = "Product name is required." });

            // Resolve foreign keys
            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == req.CustomerId);
            var vendor = await _db.Vendors
                .FirstOrDefaultAsync(v => v.Code == req.VendorId);

            var product = new Product
            {
                CustomerId   = customer?.Id,
                VendorId     = vendor?.Id,
                Category     = req.Category,
                ProductType  = req.ProductType,
                ProductName  = req.ProductName,
                ProductCode  = req.ProductCode,
                ProductColor = req.ProductColor,
                ProductSize  = req.ProductSize,
                PhotoUrl     = req.PhotoUrl,
                Remark       = req.Remark,
                CreatedAt    = DateTime.UtcNow
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = product.Id });
        }

        // ── PUT api/products/{id} ─────────────────────────────────────
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductRequest req)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();
            if (string.IsNullOrWhiteSpace(req.ProductName))
                return BadRequest(new { message = "Product name is required." });

            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == req.CustomerId);
            var vendor = await _db.Vendors
                .FirstOrDefaultAsync(v => v.Code == req.VendorId);

            product.CustomerId   = customer?.Id;
            product.VendorId     = vendor?.Id;
            product.Category     = req.Category;
            product.ProductType  = req.ProductType;
            product.ProductName  = req.ProductName;
            product.ProductCode  = req.ProductCode;
            product.ProductColor = req.ProductColor;
            product.ProductSize  = req.ProductSize;
            product.Remark       = req.Remark;

            // Only update photo if a new one was uploaded
            if (!string.IsNullOrWhiteSpace(req.PhotoUrl))
                product.PhotoUrl = req.PhotoUrl;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ── DELETE api/products/{id} ──────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();

            // Delete photo file if exists
            if (!string.IsNullOrEmpty(product.PhotoUrl))
                DeletePhotoFile(product.PhotoUrl);

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ── POST api/products/{id}/photo ──────────────────────────────
        [HttpPost("{id}/photo")]
        public async Task<IActionResult> UploadPhoto(int id, IFormFile file)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            // Validate image
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { message = "Only image files are allowed (jpg, png, webp)." });

            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "File size must be under 5MB." });

            // Delete old photo
            if (!string.IsNullOrEmpty(product.PhotoUrl))
                DeletePhotoFile(product.PhotoUrl);

            // Save new photo
            var uploadDir = Path.Combine(_env.ContentRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadDir);
            var fileName = $"{id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            product.PhotoUrl = $"/uploads/products/{fileName}";
            await _db.SaveChangesAsync();

            return Ok(new { success = true, photoUrl = product.PhotoUrl });
        }

        // ── GET api/products/categories ───────────────────────────────
        // Legacy: lấy từ Products table (giữ tương thích)
        [HttpGet("categories")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var cats = await _db.Products
                .Where(p => p.Category != null)
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            return Ok(cats);
        }

        // ── GET api/products/productcategories ────────────────────────
        // Lấy từ bảng ProductCategories (chuẩn)
        [HttpGet("productcategories")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetProductCategories()
        {
            var cats = await _db.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return Ok(cats);
        }

        // ── Helpers ───────────────────────────────────────────────────
        private void DeletePhotoFile(string photoUrl)
        {
            try
            {
                // photoUrl = "/uploads/products/filename.jpg"
                var relative = photoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(_env.ContentRootPath, relative);
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }
            catch { /* ignore file delete errors */ }
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────
    public class ProductDto
    {
        public int    Id           { get; set; }
        public string CustomerId   { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string VendorId     { get; set; } = "";
        public string VendorName   { get; set; } = "";
        public string Category     { get; set; } = "";
        public string ProductType  { get; set; } = "";
        public string ProductName  { get; set; } = "";
        public string ProductCode  { get; set; } = "";
        public string ProductColor { get; set; } = "";
        public string ProductSize  { get; set; } = "";
        public string PhotoUrl     { get; set; } = "";
        public string Remark       { get; set; } = "";
    }

    public class ProductRequest
    {
        public string  CustomerId   { get; set; } = "";
        public string  VendorId     { get; set; } = "";
        public string? Category     { get; set; }
        public string? ProductType  { get; set; }
        public string  ProductName  { get; set; } = "";
        public string? ProductCode  { get; set; }
        public string? ProductColor { get; set; }
        public string? ProductSize  { get; set; }
        public string? PhotoUrl     { get; set; }
        public string? Remark       { get; set; }
    }
}
