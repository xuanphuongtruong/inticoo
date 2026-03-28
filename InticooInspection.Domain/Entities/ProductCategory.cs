namespace InticooInspection.Domain.Entities
{
    public class ProductCategory
    {
        public int      Id        { get; set; }
        public string   Name      { get; set; } = "";
        public bool     IsActive  { get; set; } = true;
        public int      SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
