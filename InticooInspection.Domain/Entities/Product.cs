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

        public string?  PhotoUrl     { get; set; }   // ảnh chính — dùng trong báo cáo kiểm hàng
        public string?  PhotoUrlsJson { get; set; }  // JSON ["url1","url2",...] — tối đa 8 ảnh (gồm cả ảnh chính)
        public string?  Remark       { get; set; }

        // ── New fields ───────────────────────────────────────────────
        public bool     IsActive      { get; set; } = true;   // STATUS: Valid / Invalid
        public DateTime? EstablishDate { get; set; }           // ESTABLISH DATE

        public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt   { get; set; }   // UPDATED ON — lần cập nhật thông tin gần nhất

        // Số thùng carton / 1 bộ sản phẩm (vd 2 = mỗi SP đóng 2 thùng cần kiểm)
        public int?     CartonQuantity { get; set; }

        // Navigation
        public Customer? Customer { get; set; }
        public Vendor?   Vendor   { get; set; }
        public ICollection<ProductReference> References { get; set; } = new List<ProductReference>();
        public ICollection<ProductCarton>    Cartons    { get; set; } = new List<ProductCarton>();
    }

    /// <summary>Kích thước thùng carton của sản phẩm (CARTON SIZE MM/INCH)</summary>
    public class ProductCarton
    {
        public int      Id                { get; set; }
        public int      ProductId         { get; set; }
        public Product? Product           { get; set; }

        public int      SortOrder         { get; set; }
        public string?  CartonVariant     { get; set; }   // "Carton A"
        public string?  Description       { get; set; }   // "Dining Chair"
        public string?  PackagingQuantity { get; set; }   // "2pcs/ctn"
        public decimal? SizeL             { get; set; }
        public decimal? SizeW             { get; set; }
        public decimal? SizeH             { get; set; }
        public decimal? Weight            { get; set; }   // kg
    }
}
