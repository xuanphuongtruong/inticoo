namespace InticooInspection.Domain.Entities
{
    public enum VendorType
    {
        Material,
        Accessory,
        Service,
        Logistics,
        Manufacturing
    }

    public enum VendorStatus
    {
        Active,
        Inactive,
        Suspended
    }

    public class Vendor
    {
        public int          Id             { get; set; }
        public string       Code           { get; set; } = "";
        public string       Name           { get; set; } = "";
        public string?      ShortName      { get; set; }
        public VendorType   Type           { get; set; } = VendorType.Material;
        public VendorStatus Status         { get; set; } = VendorStatus.Active;

        // ── Vendor Profile ────────────────────────────────────────────
        public string?      Category       { get; set; }             // PRODUCT CATEGORY
        public string?      TaxCode        { get; set; }
        public string?      BusinessRegNo  { get; set; }
        public string?      Phone          { get; set; }             // PHONE (company main phone)
        public string?      Website        { get; set; }
        public string?      Notes          { get; set; }

        // ── Vendor Address ────────────────────────────────────────────
        public string?      Address1       { get; set; }             // ADDRESS 1
        public string?      Address2       { get; set; }             // ADDRESS 2
        public string?      City           { get; set; }
        public string?      State          { get; set; }             // STATE / PROVINCE
        public string?      Country        { get; set; }
        public string?      PostalCode     { get; set; }             // POSTAL / ZIP

        // ── Contact Information ───────────────────────────────────────
        public string?      ContactName    { get; set; }
        public string?      ContactTitle   { get; set; }
        public string?      ContactPhone   { get; set; }             // MOBILE
        public string?      ContactEmail   { get; set; }

        // ── Legacy fields (kept for backward compatibility) ───────────
        public string?      CompanyAddress { get; set; }
        public string?      BillingAddress { get; set; }
        public string?      AttachmentPath { get; set; }
        public string?      AttachmentName { get; set; }

        public DateTime     CreatedAt      { get; set; } = DateTime.UtcNow;

        public ICollection<VendorAttachment> Attachments { get; set; } = new List<VendorAttachment>();
        public string? FactoryEvaluationNotes { get; set; }
        public List<VendorFactoryEvalFile> FactoryEvalFiles { get; set; } = new();
    }

    public class VendorAttachment
    {
        public int      Id           { get; set; }
        public int      VendorId     { get; set; }
        public Vendor   Vendor       { get; set; } = null!;
        public string   StoredName   { get; set; } = "";
        public string   OriginalName { get; set; } = "";
        public string   ContentType  { get; set; } = "";
        public long     FileSize     { get; set; }
        public DateTime UploadedAt   { get; set; } = DateTime.UtcNow;
    }
}
