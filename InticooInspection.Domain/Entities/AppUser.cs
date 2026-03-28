using Microsoft.AspNetCore.Identity;
namespace InticooInspection.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string  FullName     { get; set; } = "";
        public string? AvatarUrl    { get; set; }
        public string? CvUrl        { get; set; }

        // Inspector profile fields
        public string? InspectorId  { get; set; }   // e.g. IP 1001
        public string? Category     { get; set; }
        public string? Address      { get; set; }
        public string? City         { get; set; }
        public string? Country      { get; set; }
        public string? Mobile       { get; set; }

        public DateTime  CreatedAt    { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt  { get; set; }
        public DateTime? LastLogoutAt { get; set; }
        public bool      IsActive     { get; set; } = true;

        public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
    }
}
