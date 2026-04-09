// ============================================================
//  Đặt tại: InticooInspection.Domain/Entities/PerformanceTestMaster.cs
// ============================================================

namespace InticooInspection.Domain.Entities
{
    public class PerformanceTestMaster
    {
        public int     Id           { get; set; }
        public string  Category     { get; set; } = "";
        public string  StandardCode { get; set; } = "";
        public string? StandardName { get; set; }
        public string  ProductType  { get; set; } = "All";
        public string? Market       { get; set; }
        public string  ProtocolName { get; set; } = "";
        public string? Requirements { get; set; }
        public int     SortOrder    { get; set; } = 0;
        public bool    IsActive     { get; set; } = true;
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    }
}
