namespace InticooInspection.Domain.Entities
{
    public class Product
    {
        public int      Id           { get; set; }
        public int?     CustomerId   { get; set; }
        public int?     VendorId     { get; set; }
        public string?  Category     { get; set; }
        public string?  ProductType  { get; set; }
        public string   ProductName  { get; set; } = "";
        public string?  ProductCode  { get; set; }
        public string?  ItemNumber   { get; set; }
        public string?  ProductColor { get; set; }
        public string?  ProductSize  { get; set; }

        // Dimensions & weight
        public decimal? SizeL        { get; set; }   // mm
        public decimal? SizeW        { get; set; }   // mm
        public decimal? SizeH        { get; set; }   // mm
        public decimal? Weight       { get; set; }   // kg

        public string?  PhotoUrl     { get; set; }
        public string?  Remark       { get; set; }
        public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer? Customer { get; set; }
        public Vendor?   Vendor   { get; set; }
        public ICollection<ProductReference> References { get; set; } = new List<ProductReference>();
    }
}
