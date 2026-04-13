using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                CreatedAt     = DateTime.UtcNow
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
    }
}
