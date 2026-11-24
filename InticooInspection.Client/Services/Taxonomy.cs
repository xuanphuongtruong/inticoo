namespace InticooInspection.Client.Services
{
    /// <summary>Danh mục dùng chung (Business Category / Business Type / Language). Giá trị lưu DB giữ tiếng Anh, hiển thị qua TE("Opt", ...).</summary>
    public static class Taxonomy
    {
        public static readonly string[] MainCategories =
        {
            "Furniture", "Home Décor", "Kitchenware", "Tableware & Drinkware", "Lighting",
            "Home Appliances", "Electronics", "Tools & Hardware", "Home Improvement", "Garden & Outdoor",
            "Toys & Games", "Baby & Children's Products", "Sports & Fitness", "Camping & Recreation", "Pet Products",
            "Candles & Home Fragrance", "Bags & Luggage", "Storage & Organization", "Household & Bathroom Products", "Others",
        };

        public static readonly string[] CustomerBusinessTypes =
        {
            "Manufacturer", "Importer", "Wholesaler", "Retailer", "Trading Company",
            "Brand Owner", "Distributor", "Agent", "E-commerce / Online Retailer", "Other",
        };

        public static readonly string[] VendorBusinessTypes =
        {
            "Manufacturer", "Exporter", "Supplier", "Trading Company", "Other",
        };

        public static readonly string[] Languages =
        {
            "English", "Chinese", "Hindi", "Spanish", "Arabic",
            "Bengali", "Portuguese", "Russian", "Indonesian", "Urdu",
            "French", "Japanese", "German", "Korean", "Turkish",
            "Vietnamese", "Thai", "Malay", "Filipino",
        };
    }
}
