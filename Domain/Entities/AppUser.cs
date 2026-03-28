using Microsoft.AspNetCore.Identity;

namespace InticooInspection.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
    }
}
