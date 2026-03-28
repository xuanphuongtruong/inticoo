using InticooInspection.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Controllers
{
    [ApiController]
    [Route("api/geo")]
    [AllowAnonymous]
    public class GeoController : ControllerBase
    {
        private readonly AppDbContext _db;
        public GeoController(AppDbContext db) => _db = db;

        // GET api/geo/countries
        [HttpGet("countries")]
        public async Task<IActionResult> GetCountries()
        {
            var countries = await _db.Countries
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Code, c.Name, c.Region })
                .ToListAsync();
            return Ok(countries);
        }

        // GET api/geo/cities?countryId=1
        [HttpGet("cities")]
        public async Task<IActionResult> GetCities([FromQuery] int countryId)
        {
            var cities = await _db.Cities
                .Where(c => c.CountryId == countryId && c.IsActive)
                .OrderByDescending(c => c.IsCapital)   // thủ đô lên đầu
                .ThenBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.IsCapital })
                .ToListAsync();
            return Ok(cities);
        }
    }
}
