using InticooInspection.API.Services;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly AzureBlobService _blobService;

        public ProductController(AppDbContext db, AzureBlobService blobService)
        {
            _db          = db;
            _blobService = blobService;
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
            try
            {
                var query = _db.Products
                               .Include(p => p.Customer)
                               .Include(p => p.Vendor)
                               .Include(p => p.References.OrderBy(r => r.SortOrder))
                               .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(p =>
                        p.ProductName.Contains(search) ||
                        (p.ProductCode  != null && p.ProductCode.Contains(search))  ||
                        (p.ItemNumber   != null && p.ItemNumber.Contains(search))   ||
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
                        ItemNumber   = p.ItemNumber   ?? "",
                        ProductColor = p.ProductColor ?? "",
                        ProductSize  = p.ProductSize  ?? "",
                        SizeL        = p.SizeL,
                        SizeW        = p.SizeW,
                        SizeH        = p.SizeH,
                        Weight       = p.Weight,
                        PhotoUrl     = p.PhotoUrl     ?? "",
                        Remark       = p.Remark       ?? "",
                        IsActive      = p.IsActive,
                        EstablishDate = p.EstablishDate,
                        CreatedAt    = p.CreatedAt,
                        References   = p.References.OrderBy(r => r.SortOrder).Select(r => new ProductReferenceDto
                        {
                            Id        = r.Id,
                            SortOrder = r.SortOrder,
                            Name      = r.Name,
                            FileUrl   = r.FileUrl,
                            FileName  = r.FileName
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { total, page, pageSize, items });
            }
            catch (Exception ex)
            {
                // Log toàn bộ chain lỗi (InnerException có thông tin DB cụ thể)
                var errors = new List<string>();
                var e = ex;
                while (e != null) { errors.Add($"{e.GetType().Name}: {e.Message}"); e = e.InnerException; }
                return StatusCode(500, new
                {
                    error = "Failed to load products.",
                    details = errors,
                    hint = "Most likely cause: missing DB migration for ProductCategories/ProductReferences tables, or missing columns EstablishDate/IsActive. Run: dotnet ef database update"
                });
            }
        }

        // ── GET api/products/nextcode ─────────────────────────────────
        [HttpGet("nextcode")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetNextCode()
        {
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
                    productCode = p.ProductCode  ?? "",
                    productName = p.ProductName,
                    itemNumber  = p.ItemNumber   ?? "",
                    productType = p.ProductType  ?? "",
                    category    = p.Category     ?? "",
                    sizeL       = p.SizeL,
                    sizeW       = p.SizeW,
                    sizeH       = p.SizeH,
                    weight      = p.Weight,
                    photoUrl    = p.PhotoUrl     ?? "",
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
                             .Include(x => x.References.OrderBy(r => r.SortOrder))
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
                ItemNumber   = p.ItemNumber   ?? "",
                ProductColor = p.ProductColor ?? "",
                ProductSize  = p.ProductSize  ?? "",
                SizeL        = p.SizeL,
                SizeW        = p.SizeW,
                SizeH        = p.SizeH,
                Weight       = p.Weight,
                PhotoUrl     = p.PhotoUrl     ?? "",
                Remark       = p.Remark       ?? "",
                IsActive      = p.IsActive,
                EstablishDate = p.EstablishDate,
                CreatedAt    = p.CreatedAt,
                References   = p.References.OrderBy(r => r.SortOrder).Select(r => new ProductReferenceDto
                {
                    Id        = r.Id,
                    SortOrder = r.SortOrder,
                    Name      = r.Name,
                    FileUrl   = r.FileUrl,
                    FileName  = r.FileName
                }).ToList()
            });
        }

        // ── POST api/products ─────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ProductName))
                return BadRequest(new { message = "Product name is required." });

            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == req.CustomerId);
            var vendor = await _db.Vendors
                .FirstOrDefaultAsync(v => v.Code == req.VendorId);

            var product = new Product
            {
                CustomerId    = customer?.Id,
                VendorId      = vendor?.Id,
                Category      = req.Category,
                ProductType   = req.ProductType,
                ProductName   = req.ProductName,
                ProductCode   = req.ProductCode,
                ItemNumber    = req.ItemNumber,
                ProductColor  = req.ProductColor,
                ProductSize   = req.ProductSize,
                SizeL         = req.SizeL,
                SizeW         = req.SizeW,
                SizeH         = req.SizeH,
                Weight        = req.Weight,
                PhotoUrl      = req.PhotoUrl,
                Remark        = req.Remark,
                IsActive      = req.IsActive,
                EstablishDate = req.EstablishDate,
                CreatedAt     = DateTime.UtcNow
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            var refs = req.References
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) || !string.IsNullOrWhiteSpace(r.FileUrl))
                .Select((r, i) => new ProductReference
                {
                    ProductId = product.Id,
                    SortOrder = r.SortOrder > 0 ? r.SortOrder : i + 1,
                    Name      = r.Name,
                    FileUrl   = r.FileUrl,
                    FileName  = r.FileName
                }).ToList();

            if (refs.Any())
            {
                _db.ProductReferences.AddRange(refs);
                await _db.SaveChangesAsync();
            }

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

            product.CustomerId    = customer?.Id;
            product.VendorId      = vendor?.Id;
            product.Category      = req.Category;
            product.ProductType   = req.ProductType;
            product.ProductName   = req.ProductName;
            product.ProductCode   = req.ProductCode;
            product.ItemNumber    = req.ItemNumber;
            product.ProductColor  = req.ProductColor;
            product.ProductSize   = req.ProductSize;
            product.SizeL         = req.SizeL;
            product.SizeW         = req.SizeW;
            product.SizeH         = req.SizeH;
            product.Weight        = req.Weight;
            product.Remark        = req.Remark;
            product.IsActive      = req.IsActive;
            product.EstablishDate = req.EstablishDate;

            if (!string.IsNullOrWhiteSpace(req.PhotoUrl))
                product.PhotoUrl = req.PhotoUrl;

            var oldRefs = await _db.ProductReferences
                .Where(r => r.ProductId == id)
                .ToListAsync();
            _db.ProductReferences.RemoveRange(oldRefs);

            var newRefs = req.References
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) || !string.IsNullOrWhiteSpace(r.FileUrl))
                .Select((r, i) => new ProductReference
                {
                    ProductId = id,
                    SortOrder = r.SortOrder > 0 ? r.SortOrder : i + 1,
                    Name      = r.Name,
                    FileUrl   = r.FileUrl,
                    FileName  = r.FileName
                }).ToList();

            if (newRefs.Any())
                _db.ProductReferences.AddRange(newRefs);

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ── DELETE api/products/{id} ──────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ── POST api/products/{id}/photo ──────────────────────────────
        // ✅ Đã sửa: upload lên Azure Blob Storage thay vì lưu local
        [HttpPost("{id}/photo")]
        public async Task<IActionResult> UploadPhoto(int id, IFormFile file)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { message = "Only image files are allowed (jpg, png, webp)." });

            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "File size must be under 5MB." });

            // ✅ Upload lên Azure Blob Storage
            var fileName = $"{id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            await using var stream = file.OpenReadStream();
            var url = await _blobService.UploadAsync("products", fileName, stream, file.ContentType);

            product.PhotoUrl = url;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, photoUrl = url });
        }

        // ── GET api/products/categories ───────────────────────────────
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
        [HttpGet("productcategories")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetProductCategories()
        {
            try
            {
                var cats = await _db.ProductCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync();
                return Ok(cats);
            }
            catch (Exception ex)
            {
                var errors = new List<string>();
                var e = ex;
                while (e != null) { errors.Add($"{e.GetType().Name}: {e.Message}"); e = e.InnerException; }
                return StatusCode(500, new
                {
                    error = "Failed to load product categories.",
                    details = errors,
                    hint = "Table 'ProductCategories' may not exist. Run: dotnet ef database update"
                });
            }
        }

        // ── POST api/products/{id}/references/upload ──────────────────
        // ✅ Đã sửa: upload lên Azure Blob Storage thay vì lưu local
        [HttpPost("{id}/references/upload")]
        public async Task<IActionResult> UploadReferenceFile(int id, IFormFile file)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            if (file.Length > 20 * 1024 * 1024)
                return BadRequest(new { message = "File size must be under 20MB." });

            // ✅ Upload lên Azure Blob Storage
            var ext      = Path.GetExtension(file.FileName);
            var fileName = $"{id}_{Guid.NewGuid()}{ext}";
            await using var stream = file.OpenReadStream();
            var url = await _blobService.UploadAsync("references", fileName, stream, file.ContentType);

            return Ok(new
            {
                success  = true,
                fileUrl  = url,
                fileName = file.FileName
            });
        }
    
        // ─────────────────────────────────────────────────────────────────
        // GET api/products/template
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("template")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public IActionResult DownloadImportTemplate([FromServices] IWebHostEnvironment env)
            => ImportHelper.ServeTemplate(env, "Products_Import_Template.xlsx");

        // ─────────────────────────────────────────────────────────────────
        // POST api/products/import
        // ⭐ Resolve CustomerName / VendorName → FK ID theo 3 cách:
        //    1. Code/CustomerId  2. Full name  3. ShortName
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Import([FromForm] IFormFile file)
        {
            var validateError = ImportHelper.ValidateFile(file);
            if (validateError != null) return validateError;

            var rows = new List<ProductImportRow>();
            var errors = new List<ImportRowError>();

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var sheet = workbook.Worksheets.FirstOrDefault(
                    s => string.Equals(s.Name, "Data", StringComparison.OrdinalIgnoreCase));
                if (sheet == null)
                    return BadRequest(new { success = false, message = "Sheet 'Data' not found." });

                var headerMap = ImportHelper.ReadHeaderMap(sheet, headerRow: 1);
                string[] required = { "CustomerName", "VendorName", "ProductName" };
                var missing = required.Where(h => !headerMap.ContainsKey(h) && !headerMap.ContainsKey(h + "*")).ToList();
                if (missing.Count > 0)
                    return BadRequest(new { success = false, message = $"Missing required columns: {string.Join(", ", missing)}" });

                int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
                for (int r = 2; r <= lastRow; r++)
                {
                    var row = ReadProductRow(sheet, r, headerMap);
                    if (row.IsEmpty()) continue;
                    row.RowNumber = r;
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Failed to read Excel: {ex.Message}" });
            }

            if (rows.Count == 0)
                return BadRequest(new { success = false, message = "No data rows found." });

            // Pre-load Customer/Vendor để resolve nhanh (tránh N+1)
            var customers = await _db.Customers
                .Select(c => new { c.Id, c.CustomerId, c.CompanyName, c.ShortName }).ToListAsync();
            var vendors = await _db.Vendors
                .Select(v => new { v.Id, v.Code, v.Name, v.ShortName }).ToListAsync();

            var custByCode  = customers.Where(c => !string.IsNullOrEmpty(c.CustomerId))
                .GroupBy(c => c.CustomerId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var custByName  = customers.GroupBy(c => c.CompanyName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var custByShort = customers.Where(c => !string.IsNullOrEmpty(c.ShortName))
                .GroupBy(c => c.ShortName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var vendByCode  = vendors.Where(v => !string.IsNullOrEmpty(v.Code))
                .GroupBy(v => v.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var vendByName  = vendors.GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var vendByShort = vendors.Where(v => !string.IsNullOrEmpty(v.ShortName))
                .GroupBy(v => v.ShortName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int created = 0;
            foreach (var row in rows)
            {
                var rowErrs = new List<string>();
                if (string.IsNullOrWhiteSpace(row.ProductName))  rowErrs.Add("ProductName is required.");
                if (string.IsNullOrWhiteSpace(row.CustomerName)) rowErrs.Add("CustomerName is required.");
                if (string.IsNullOrWhiteSpace(row.VendorName))   rowErrs.Add("VendorName is required.");

                int? customerFkId = null;
                if (!string.IsNullOrWhiteSpace(row.CustomerName))
                {
                    var key = row.CustomerName.Trim();
                    if (custByCode.TryGetValue(key, out var c1))      customerFkId = c1.Id;
                    else if (custByName.TryGetValue(key, out var c2)) customerFkId = c2.Id;
                    else if (custByShort.TryGetValue(key, out var c3)) customerFkId = c3.Id;
                    else rowErrs.Add($"Customer '{key}' not found (tried CustomerId / CompanyName / ShortName).");
                }

                int? vendorFkId = null;
                if (!string.IsNullOrWhiteSpace(row.VendorName))
                {
                    var key = row.VendorName.Trim();
                    if (vendByCode.TryGetValue(key, out var v1))      vendorFkId = v1.Id;
                    else if (vendByName.TryGetValue(key, out var v2)) vendorFkId = v2.Id;
                    else if (vendByShort.TryGetValue(key, out var v3)) vendorFkId = v3.Id;
                    else rowErrs.Add($"Vendor '{key}' not found (tried Code / Name / ShortName).");
                }

                if (rowErrs.Count > 0)
                {
                    errors.Add(new ImportRowError { Row = row.RowNumber, Key = row.ProductName, Errors = rowErrs });
                    continue;
                }

                try
                {
                    var product = new Product
                    {
                        CustomerId    = customerFkId,
                        VendorId      = vendorFkId,
                        Category      = row.Category,
                        ProductType   = row.ProductType,
                        ProductName   = row.ProductName!,
                        ProductCode   = row.ProductCode,
                        ItemNumber    = row.ItemNumber,
                        ProductColor  = row.ProductColor,
                        ProductSize   = row.ProductSize,
                        SizeL         = row.SizeL,
                        SizeW         = row.SizeW,
                        SizeH         = row.SizeH,
                        Weight        = row.Weight,
                        PhotoUrl      = row.PhotoUrl,
                        Remark        = row.Remark,
                        IsActive      = row.IsActive ?? true,
                        EstablishDate = row.EstablishDate,
                        CreatedAt     = DateTime.UtcNow,
                    };
                    _db.Products.Add(product);
                    await _db.SaveChangesAsync();
                    created++;
                }
                catch (Exception ex)
                {
                    errors.Add(new ImportRowError { Row = row.RowNumber, Key = row.ProductName,
                        Errors = new List<string> { ex.InnerException?.Message ?? ex.Message } });
                }
            }

            return Ok(new { success = errors.Count == 0, totalRows = rows.Count, created, failed = errors.Count, errors });
        }

        private static ProductImportRow ReadProductRow(IXLWorksheet sheet, int rowNum, Dictionary<string, int> map)
            => new ProductImportRow
            {
                CustomerName  = ImportHelper.GetStr(sheet, rowNum, map, "CustomerName"),
                VendorName    = ImportHelper.GetStr(sheet, rowNum, map, "VendorName"),
                Category      = ImportHelper.GetStr(sheet, rowNum, map, "Category"),
                ProductType   = ImportHelper.GetStr(sheet, rowNum, map, "ProductType"),
                ProductName   = ImportHelper.GetStr(sheet, rowNum, map, "ProductName"),
                ProductCode   = ImportHelper.GetStr(sheet, rowNum, map, "ProductCode"),
                ItemNumber    = ImportHelper.GetStr(sheet, rowNum, map, "ItemNumber"),
                ProductColor  = ImportHelper.GetStr(sheet, rowNum, map, "ProductColor"),
                ProductSize   = ImportHelper.GetStr(sheet, rowNum, map, "ProductSize"),
                SizeL         = ImportHelper.GetDecimal(sheet, rowNum, map, "SizeL"),
                SizeW         = ImportHelper.GetDecimal(sheet, rowNum, map, "SizeW"),
                SizeH         = ImportHelper.GetDecimal(sheet, rowNum, map, "SizeH"),
                Weight        = ImportHelper.GetDecimal(sheet, rowNum, map, "Weight"),
                PhotoUrl      = ImportHelper.GetStr(sheet, rowNum, map, "PhotoUrl"),
                Remark        = ImportHelper.GetStr(sheet, rowNum, map, "Remark"),
                IsActive      = ImportHelper.GetBool(sheet, rowNum, map, "IsActive"),
                EstablishDate = ImportHelper.GetDate(sheet, rowNum, map, "EstablishDate"),
            };

        }

    // ── DTOs ─────────────────────────────────────────────────────────
    public class ProductDto
    {
        public int     Id           { get; set; }
        public string  CustomerId   { get; set; } = "";
        public string  CustomerName { get; set; } = "";
        public string  VendorId     { get; set; } = "";
        public string  VendorName   { get; set; } = "";
        public string  Category     { get; set; } = "";
        public string  ProductType  { get; set; } = "";
        public string  ProductName  { get; set; } = "";
        public string  ProductCode  { get; set; } = "";
        public string  ItemNumber   { get; set; } = "";
        public string  ProductColor { get; set; } = "";
        public string  ProductSize  { get; set; } = "";
        public decimal? SizeL       { get; set; }
        public decimal? SizeW       { get; set; }
        public decimal? SizeH       { get; set; }
        public decimal? Weight      { get; set; }
        public string  PhotoUrl     { get; set; } = "";
        public string  Remark       { get; set; } = "";
        public bool    IsActive      { get; set; } = true;
        public DateTime? EstablishDate { get; set; }
        public DateTime CreatedAt    { get; set; }
        public List<ProductReferenceDto> References { get; set; } = new();
    }

    public class ProductRequest
    {
        public string   CustomerId    { get; set; } = "";
        public string   VendorId      { get; set; } = "";
        public string?  Category      { get; set; }
        public string?  ProductType   { get; set; }
        public string   ProductName   { get; set; } = "";
        public string?  ProductCode   { get; set; }
        public string?  ItemNumber    { get; set; }
        public string?  ProductColor  { get; set; }
        public string?  ProductSize   { get; set; }
        public decimal? SizeL         { get; set; }
        public decimal? SizeW         { get; set; }
        public decimal? SizeH         { get; set; }
        public decimal? Weight        { get; set; }
        public string?  PhotoUrl      { get; set; }
        public string?  Remark        { get; set; }
        public bool     IsActive      { get; set; } = true;
        public DateTime? EstablishDate { get; set; }
        public List<ProductReferenceRequest> References { get; set; } = new();
    }

    public class ProductReferenceDto
    {
        public int     Id        { get; set; }
        public int     SortOrder { get; set; }
        public string  Name      { get; set; } = "";
        public string? FileUrl   { get; set; }
        public string? FileName  { get; set; }
    }

    public class ProductReferenceRequest
    {
        public int     SortOrder { get; set; }
        public string  Name      { get; set; } = "";
        public string? FileUrl   { get; set; }
        public string? FileName  { get; set; }
    }

    public class ProductImportRow
    {
        public int       RowNumber     { get; set; }
        public string?   CustomerName  { get; set; }
        public string?   VendorName    { get; set; }
        public string?   Category      { get; set; }
        public string?   ProductType   { get; set; }
        public string?   ProductName   { get; set; }
        public string?   ProductCode   { get; set; }
        public string?   ItemNumber    { get; set; }
        public string?   ProductColor  { get; set; }
        public string?   ProductSize   { get; set; }
        public decimal?  SizeL         { get; set; }
        public decimal?  SizeW         { get; set; }
        public decimal?  SizeH         { get; set; }
        public decimal?  Weight        { get; set; }
        public string?   PhotoUrl      { get; set; }
        public string?   Remark        { get; set; }
        public bool?     IsActive      { get; set; }
        public DateTime? EstablishDate { get; set; }
        public bool IsEmpty() =>
            string.IsNullOrWhiteSpace(ProductName) && string.IsNullOrWhiteSpace(CustomerName) &&
            string.IsNullOrWhiteSpace(VendorName) && string.IsNullOrWhiteSpace(ProductCode) &&
            string.IsNullOrWhiteSpace(ItemNumber);
    }

}
