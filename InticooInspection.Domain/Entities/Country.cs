namespace InticooInspection.Domain.Entities
{
    public class Country
    {
        public int    Id       { get; set; }
        public string Code     { get; set; } = "";   // ISO 3166-1 alpha-2
        public string Name     { get; set; } = "";
        public string Region   { get; set; } = "";   // Asia, Europe, Africa, Americas, Oceania
        public bool   IsActive { get; set; } = true;

        public ICollection<City> Cities { get; set; } = new List<City>();
    }

    public class City
    {
        public int     Id        { get; set; }
        public int     CountryId { get; set; }
        public string  Name      { get; set; } = "";
        public bool    IsCapital { get; set; } = false;
        public bool    IsActive  { get; set; } = true;

        public Country? Country { get; set; }
    }
}
