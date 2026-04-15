namespace InticooInspection.Domain.Entities
{
    public class VendorFactoryEvalFile
    {
        public int      Id           { get; set; }
        public int      VendorId     { get; set; }
        public string   StoredName   { get; set; } = "";
        public string   OriginalName { get; set; } = "";
        public string   ContentType  { get; set; } = "";
        public long     FileSize     { get; set; }
        public DateTime UploadedAt   { get; set; }

        public Vendor Vendor { get; set; } = null!;
    }
}