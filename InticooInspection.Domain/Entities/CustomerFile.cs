namespace InticooInspection.Domain.Entities
{
    public class CustomerFile
    {
        public int      Id         { get; set; }
        public int      CustomerId { get; set; }
        public Customer Customer   { get; set; } = null!;
        public string   FileName   { get; set; } = "";
        public string   FilePath   { get; set; } = "";   // relative path on disk
        public string   Url        { get; set; } = "";   // public URL
        public long     FileSize   { get; set; }
        public string   ContentType { get; set; } = "";
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
