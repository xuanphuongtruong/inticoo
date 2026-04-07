namespace InticooInspection.Domain.Entities
{
    public class ProductReference
    {
        public int     Id          { get; set; }
        public int     ProductId   { get; set; }
        public int     SortOrder   { get; set; }        // row number (1-based)
        public string  Name        { get; set; } = "";  // Reference name
        public string? FileUrl     { get; set; }        // stored file path
        public string? FileName    { get; set; }        // original filename

        // Navigation
        public Product? Product    { get; set; }
    }
}
