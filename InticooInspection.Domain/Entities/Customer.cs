namespace InticooInspection.Domain.Entities
{
    public class Customer
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = "";       // e.g. CP 1001
        public string CompanyName { get; set; } = "";
        public string? Category { get; set; }
        public string? ContactPerson { get; set; }
        public string? Position { get; set; }
        public string? Phone { get; set; }
        public string? OfficePhone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? TaxCode { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
