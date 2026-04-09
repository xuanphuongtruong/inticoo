using Microsoft.AspNetCore.Identity;
namespace InticooInspection.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string  FullName            { get; set; } = "";
        public string? AvatarUrl           { get; set; }
        public string? CvUrl               { get; set; }

        // ── Inspector Details ────────────────────────────────────────
        public string? InspectorId         { get; set; }   // e.g. IP10001
        public string? ShortName           { get; set; }   // SHORT NAME
        public DateTime? DateOfBirth       { get; set; }   // DATE OF BIRTH
        public string? Gender              { get; set; }   // Male / Female / Other
        public string? Nationality         { get; set; }   // NATIONALITY
        public string? IdType              { get; set; }   // ID / Passport
        public string? IdNumber            { get; set; }   // NUMBER
        public string? Category            { get; set; }   // CATEGORY
        public int?    InspectionStartYear { get; set; }   // INSPECTION START YEAR
        public string? Language            { get; set; }   // comma-separated: English,Chinese,...
        public string? Mobile              { get; set; }   // MOBILE

        // ── Inspector Address ────────────────────────────────────────
        public string? Address             { get; set; }   // legacy (kept)
        public string? Address1            { get; set; }   // ADDRESS 1
        public string? Address2            { get; set; }   // ADDRESS 2
        public string? City                { get; set; }   // CITY
        public string? State               { get; set; }   // STATE / PROVINCE
        public string? Country             { get; set; }   // COUNTRY
        public string? PostalCode          { get; set; }   // POSTAL / ZIP

        public string? CustomerId          { get; set; }   // when role = Customer

        public DateTime  CreatedAt         { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt       { get; set; }
        public DateTime? LastLogoutAt      { get; set; }
        public bool      IsActive          { get; set; } = true;

        public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
    }
}
