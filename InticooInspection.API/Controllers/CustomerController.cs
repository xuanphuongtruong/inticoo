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
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _db.Customers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.CompanyName.Contains(search)
                    || (c.ContactPerson != null && c.ContactPerson.Contains(search))
                    || (c.Phone        != null && c.Phone.Contains(search))
                    || (c.Email        != null && c.Email.Contains(search))
                    || (c.TaxCode      != null && c.TaxCode.Contains(search)));

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.CompanyName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id, c.CustomerId, c.CompanyName, c.Category,
                    c.ContactPerson, c.Position, c.Phone, c.OfficePhone,
                    c.Email, c.Address, c.City, c.Country,
                    c.TaxCode, c.Notes, c.IsActive, c.CreatedAt,
                    FileCount = _db.CustomerFiles.Count(f => f.CustomerId == c.Id)
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // GET api/customers/dropdown
        [HttpGet("dropdown")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
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
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
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

            // Auto-generate CustomerId: CP100001, CP100002, ...
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
                Category      = request.Category,
                ContactPerson = request.ContactPerson,
                Position      = request.Position,
                Phone         = request.Phone,
                OfficePhone   = request.OfficePhone,
                Email         = request.Email,
                Address       = request.Address,
                City          = request.City,
                Country       = request.Country,
                TaxCode       = request.TaxCode,
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
            customer.Category      = request.Category;
            customer.ContactPerson = request.ContactPerson;
            customer.Position      = request.Position;
            customer.Phone         = request.Phone;
            customer.OfficePhone   = request.OfficePhone;
            customer.Email         = request.Email;
            customer.Address       = request.Address;
            customer.City          = request.City;
            customer.Country       = request.Country;
            customer.TaxCode       = request.TaxCode;
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
        public string  CustomerId     { get; set; } = "";
        public string  CompanyName    { get; set; } = "";
        public string? Category       { get; set; }
        public string? ContactPerson  { get; set; }
        public string? Position       { get; set; }
        public string? Phone          { get; set; }
        public string? OfficePhone    { get; set; }
        public string? Email          { get; set; }
        public string? Address        { get; set; }
        public string? City           { get; set; }
        public string? Country        { get; set; }
        public string? TaxCode        { get; set; }
        public string? Notes          { get; set; }
        public bool    IsActive       { get; set; } = true;
    }
}
