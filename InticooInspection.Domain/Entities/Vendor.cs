namespace InticooInspection.Domain.Entities
{
    public enum VendorType
    {
        Material,       // Nguyen vat lieu
        Accessory,      // Phu kien
        Service,        // Dich vu
        Logistics,      // Van chuyen
        Manufacturing   // Gia cong
    }

    public enum VendorStatus
    {
        Active,     // Dang hop tac
        Inactive,   // Ngung
        Suspended   // Tam khoa
    }

    public class Vendor
    {
        public int          Id             { get; set; }
        public string       Code           { get; set; } = "";
        public string       Name           { get; set; } = "";
        public string?      ShortName      { get; set; }
        public VendorType   Type           { get; set; } = VendorType.Material;
        public VendorStatus Status         { get; set; } = VendorStatus.Active;
        public string?      TaxCode        { get; set; }
        public string?      BusinessRegNo  { get; set; }
        public string?      Website        { get; set; }
        public string?      Notes          { get; set; }
        public string?      ContactName    { get; set; }
        public string?      ContactTitle   { get; set; }
        public string?      ContactPhone   { get; set; }
        public string?      ContactEmail   { get; set; }
        public string?      CompanyAddress { get; set; }
        public string?      BillingAddress { get; set; }
        public string?      City           { get; set; }
        public string?      Country        { get; set; }
        // Legacy single-attachment fields (kept for backward compatibility)
        public string?      AttachmentPath { get; set; }
        public string?      AttachmentName { get; set; }
        public DateTime     CreatedAt      { get; set; } = DateTime.UtcNow;

        // Navigation: multiple attachments
        public ICollection<VendorAttachment> Attachments { get; set; } = new List<VendorAttachment>();
    }

    /// <summary>One file attached to a Vendor.</summary>
    public class VendorAttachment
    {
        public int      Id           { get; set; }
        public int      VendorId     { get; set; }
        public Vendor   Vendor       { get; set; } = null!;
        /// <summary>Stored file name on disk (guid-based).</summary>
        public string   StoredName   { get; set; } = "";
        /// <summary>Original file name shown to user.</summary>
        public string   OriginalName { get; set; } = "";
        /// <summary>MIME type / content type.</summary>
        public string   ContentType  { get; set; } = "";
        public long     FileSize     { get; set; }
        public DateTime UploadedAt   { get; set; } = DateTime.UtcNow;
    }
}
