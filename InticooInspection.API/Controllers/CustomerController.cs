using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomerController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CustomerController(AppDbContext db) => _db = db;

        // GET api/customers
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _db.Customers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c =>
                    c.CompanyName.Contains(search) ||
                    (c.ShortName     != null && c.ShortName.Contains(search))     ||
                    (c.ContactPerson != null && c.ContactPerson.Contains(search)) ||
                    (c.Mobile        != null && c.Mobile.Contains(search))        ||
                    (c.Phone         != null && c.Phone.Contains(search))         ||
                    (c.Email         != null && c.Email.Contains(search))         ||
                    (c.TaxCode       != null && c.TaxCode.Contains(search))       ||
                    (c.Country       != null && c.Country.Contains(search))       ||
                    (c.Category      != null && c.Category.Contains(search))      ||
                    (c.CustomerId    != null && c.CustomerId.Contains(search)));

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.CompanyName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,           c.CustomerId,   c.CompanyName,  c.ShortName,
                    c.BusinessType, c.Category,     c.TaxCode,      c.BusinessRefNo,
                    c.Phone,        c.Website,
                    c.Address1,     c.Address2,     c.City,         c.State,
                    c.Country,      c.PostalCode,
                    c.ContactPerson,c.Position,     c.Mobile,       c.Email,
                    c.OfficePhone,  c.Notes,        c.IsActive,     c.CreatedAt,
                    c.ReceiveInspectionReport,
                    c.ReportEmailType,
                    c.AlternateReportEmail,
                    FileCount = _db.CustomerFiles.Count(f => f.CustomerId == c.Id)
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // GET api/customers/dropdown
        [HttpGet("dropdown")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDropdown()
        {
            var items = await _db.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.CompanyName)
                .Select(c => new { c.CustomerId, c.CompanyName })
                .ToListAsync();
            return Ok(items);
        }

        // GET api/customers/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var c = await _db.Customers.FindAsync(id);
            return c == null ? NotFound() : Ok(c);
        }

        // POST api/customers
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CustomerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CompanyName))
                return BadRequest(new { message = "Company name is required." });

            // Validate alternate email khi user chọn "Alternate"
            if (request.ReceiveInspectionReport
                && string.Equals(request.ReportEmailType, "Alternate", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(request.AlternateReportEmail))
            {
                return BadRequest(new { message = "Alternate email is required when 'Use Alternate Email' is selected." });
            }

            var lastCustomer = await _db.Customers
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastCustomer != null && lastCustomer.CustomerId.StartsWith("CP"))
            {
                var numPart = lastCustomer.CustomerId.Substring(2);
                if (int.TryParse(numPart, out int parsed))
                    nextNumber = parsed + 1;
            }

            var customer = new Customer
            {
                CustomerId    = $"CP{nextNumber:D6}",
                CompanyName   = request.CompanyName,
                ShortName     = request.ShortName,
                BusinessType  = request.BusinessType,
                Category      = request.Category,
                TaxCode       = request.TaxCode,
                BusinessRefNo = request.BusinessRefNo,
                Phone         = request.Phone,
                Website       = request.Website,
                Address1      = request.Address1,
                Address2      = request.Address2,
                City          = request.City,
                State         = request.State,
                Country       = request.Country,
                PostalCode    = request.PostalCode,
                ContactPerson = request.ContactPerson,
                Position      = request.Position,
                Mobile        = request.Mobile,
                Email         = request.Email,
                OfficePhone   = request.OfficePhone,
                Notes         = request.Notes,
                IsActive      = request.IsActive,
                CreatedAt     = DateTime.UtcNow,

                // Inspection Report Preferences
                ReceiveInspectionReport = request.ReceiveInspectionReport,
                ReportEmailType         = string.IsNullOrWhiteSpace(request.ReportEmailType)
                                              ? "Registered"
                                              : request.ReportEmailType,
                AlternateReportEmail    = request.AlternateReportEmail
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = customer.Id });
        }

        // PUT api/customers/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CustomerRequest request)
        {
            var customer = await _db.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.CompanyName))
                return BadRequest(new { message = "Company name is required." });

            // Validate alternate email
            if (request.ReceiveInspectionReport
                && string.Equals(request.ReportEmailType, "Alternate", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(request.AlternateReportEmail))
            {
                return BadRequest(new { message = "Alternate email is required when 'Use Alternate Email' is selected." });
            }

            customer.CustomerId    = request.CustomerId;
            customer.CompanyName   = request.CompanyName;
            customer.ShortName     = request.ShortName;
            customer.BusinessType  = request.BusinessType;
            customer.Category      = request.Category;
            customer.TaxCode       = request.TaxCode;
            customer.BusinessRefNo = request.BusinessRefNo;
            customer.Phone         = request.Phone;
            customer.Website       = request.Website;
            customer.Address1      = request.Address1;
            customer.Address2      = request.Address2;
            customer.City          = request.City;
            customer.State         = request.State;
            customer.Country       = request.Country;
            customer.PostalCode    = request.PostalCode;
            customer.ContactPerson = request.ContactPerson;
            customer.Position      = request.Position;
            customer.Mobile        = request.Mobile;
            customer.Email         = request.Email;
            customer.OfficePhone   = request.OfficePhone;
            customer.Notes         = request.Notes;
            customer.IsActive      = request.IsActive;

            // Inspection Report Preferences
            customer.ReceiveInspectionReport = request.ReceiveInspectionReport;
            customer.ReportEmailType         = string.IsNullOrWhiteSpace(request.ReportEmailType)
                                                   ? "Registered"
                                                   : request.ReportEmailType;
            customer.AlternateReportEmail    = request.AlternateReportEmail;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // DELETE api/customers/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _db.Customers.FindAsync(id);
            if (customer == null) return NotFound();
            _db.Customers.Remove(customer);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    
        // ─────────────────────────────────────────────────────────────────
        // GET api/customers/template
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("template")]
        [AllowAnonymous]
        public IActionResult DownloadImportTemplate([FromServices] IWebHostEnvironment env)
            => ImportHelper.ServeTemplate(env, "Customers_Import_Template.xlsx");

        // ─────────────────────────────────────────────────────────────────
        // POST api/customers/import
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Import([FromForm] IFormFile file)
        {
            var validateError = ImportHelper.ValidateFile(file);
            if (validateError != null) return validateError;

            var rows = new List<CustomerImportRow>();
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
                if (!headerMap.ContainsKey("CompanyName") && !headerMap.ContainsKey("CompanyName*"))
                    return BadRequest(new { success = false, message = "Missing required column: CompanyName" });

                int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
                for (int r = 2; r <= lastRow; r++)
                {
                    var row = ReadCustomerRow(sheet, r, headerMap);
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

            var lastC = await _db.Customers.OrderByDescending(c => c.Id).FirstOrDefaultAsync();
            int nextNum = 1;
            if (lastC != null && !string.IsNullOrEmpty(lastC.CustomerId) && lastC.CustomerId.StartsWith("CP"))
                if (int.TryParse(lastC.CustomerId.Substring(2), out int n)) nextNum = n + 1;

            int created = 0;
            foreach (var row in rows)
            {
                var rowErrs = new List<string>();
                if (string.IsNullOrWhiteSpace(row.CompanyName)) rowErrs.Add("CompanyName is required.");
                if (!string.IsNullOrEmpty(row.Email) && !ImportHelper.IsValidEmail(row.Email))
                    rowErrs.Add("Email format is invalid.");

                if (rowErrs.Count > 0)
                {
                    errors.Add(new ImportRowError { Row = row.RowNumber, Key = row.CompanyName, Errors = rowErrs });
                    continue;
                }

                try
                {
                    var customer = new Customer
                    {
                        CustomerId    = $"CP{nextNum:D6}",
                        CompanyName   = row.CompanyName!,
                        ShortName     = row.ShortName,
                        BusinessType  = row.BusinessType,
                        Category      = row.Category,
                        TaxCode       = row.TaxCode,
                        BusinessRefNo = row.BusinessRefNo,
                        Phone         = row.Phone,
                        Website       = row.Website,
                        Address1      = row.Address1,
                        Address2      = row.Address2,
                        City          = row.City,
                        State         = row.State,
                        Country       = row.Country,
                        PostalCode    = row.PostalCode,
                        ContactPerson = row.ContactPerson,
                        Position      = row.Position,
                        Mobile        = row.Mobile,
                        Email         = row.Email,
                        OfficePhone   = row.OfficePhone,
                        Notes         = row.Notes,
                        IsActive      = row.IsActive ?? true,
                        CreatedAt     = DateTime.UtcNow,
                        ReceiveInspectionReport = true,
                        ReportEmailType = "Registered",
                    };
                    _db.Customers.Add(customer);
                    await _db.SaveChangesAsync();
                    nextNum++;
                    created++;
                }
                catch (Exception ex)
                {
                    errors.Add(new ImportRowError { Row = row.RowNumber, Key = row.CompanyName,
                        Errors = new List<string> { ex.InnerException?.Message ?? ex.Message } });
                }
            }

            return Ok(new { success = errors.Count == 0, totalRows = rows.Count, created, failed = errors.Count, errors });
        }

        private static CustomerImportRow ReadCustomerRow(IXLWorksheet sheet, int rowNum, Dictionary<string, int> map)
            => new CustomerImportRow
            {
                CompanyName   = ImportHelper.GetStr(sheet, rowNum, map, "CompanyName"),
                ShortName     = ImportHelper.GetStr(sheet, rowNum, map, "ShortName"),
                BusinessType  = ImportHelper.GetStr(sheet, rowNum, map, "BusinessType"),
                Category      = ImportHelper.GetStr(sheet, rowNum, map, "Category"),
                TaxCode       = ImportHelper.GetStr(sheet, rowNum, map, "TaxCode"),
                BusinessRefNo = ImportHelper.GetStr(sheet, rowNum, map, "BusinessRefNo"),
                Phone         = ImportHelper.GetStr(sheet, rowNum, map, "Phone"),
                Website       = ImportHelper.GetStr(sheet, rowNum, map, "Website"),
                Address1      = ImportHelper.GetStr(sheet, rowNum, map, "Address1"),
                Address2      = ImportHelper.GetStr(sheet, rowNum, map, "Address2"),
                City          = ImportHelper.GetStr(sheet, rowNum, map, "City"),
                State         = ImportHelper.GetStr(sheet, rowNum, map, "State"),
                Country       = ImportHelper.GetStr(sheet, rowNum, map, "Country"),
                PostalCode    = ImportHelper.GetStr(sheet, rowNum, map, "PostalCode"),
                ContactPerson = ImportHelper.GetStr(sheet, rowNum, map, "ContactPerson"),
                Position      = ImportHelper.GetStr(sheet, rowNum, map, "Position"),
                Mobile        = ImportHelper.GetStr(sheet, rowNum, map, "Mobile"),
                Email         = ImportHelper.GetStr(sheet, rowNum, map, "Email"),
                OfficePhone   = ImportHelper.GetStr(sheet, rowNum, map, "OfficePhone"),
                Notes         = ImportHelper.GetStr(sheet, rowNum, map, "Notes"),
                IsActive      = ImportHelper.GetBool(sheet, rowNum, map, "IsActive"),
            };

        }

    public class CustomerRequest
    {
        public string  CustomerId    { get; set; } = "";
        public string  CompanyName   { get; set; } = "";
        public string? ShortName     { get; set; }
        public string? BusinessType  { get; set; }
        public string? Category      { get; set; }
        public string? TaxCode       { get; set; }
        public string? BusinessRefNo { get; set; }
        public string? Phone         { get; set; }
        public string? Website       { get; set; }
        public string? Address1      { get; set; }
        public string? Address2      { get; set; }
        public string? City          { get; set; }
        public string? State         { get; set; }
        public string? Country       { get; set; }
        public string? PostalCode    { get; set; }
        public string? ContactPerson { get; set; }
        public string? Position      { get; set; }
        public string? Mobile        { get; set; }
        public string? Email         { get; set; }
        public string? OfficePhone   { get; set; }
        public string? Notes         { get; set; }
        public bool    IsActive      { get; set; } = true;

        // ── Inspection Report Preferences ────────────────────────────
        public bool    ReceiveInspectionReport { get; set; } = true;
        public string? ReportEmailType         { get; set; } = "Registered";
        public string? AlternateReportEmail    { get; set; }
    }

    public class CustomerImportRow
    {
        public int     RowNumber     { get; set; }
        public string? CompanyName   { get; set; }
        public string? ShortName     { get; set; }
        public string? BusinessType  { get; set; }
        public string? Category      { get; set; }
        public string? TaxCode       { get; set; }
        public string? BusinessRefNo { get; set; }
        public string? Phone         { get; set; }
        public string? Website       { get; set; }
        public string? Address1      { get; set; }
        public string? Address2      { get; set; }
        public string? City          { get; set; }
        public string? State         { get; set; }
        public string? Country       { get; set; }
        public string? PostalCode    { get; set; }
        public string? ContactPerson { get; set; }
        public string? Position      { get; set; }
        public string? Mobile        { get; set; }
        public string? Email         { get; set; }
        public string? OfficePhone   { get; set; }
        public string? Notes         { get; set; }
        public bool?   IsActive      { get; set; }
        public bool IsEmpty() =>
            string.IsNullOrWhiteSpace(CompanyName) && string.IsNullOrWhiteSpace(Email) &&
            string.IsNullOrWhiteSpace(TaxCode) && string.IsNullOrWhiteSpace(Phone);
    }

}
