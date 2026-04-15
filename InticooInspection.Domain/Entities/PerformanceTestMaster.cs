// ============================================================
//  Đặt tại: InticooInspection.Domain/Entities/PerformanceTestMaster.cs
// ============================================================

namespace InticooInspection.Domain.Entities
{
    public class PerformanceTestMaster
    {
        public int      Id            { get; set; }

        /// <summary>Nhóm lớn: "Packaging Test" | "Product test" | "Moisture content test" | "Adhessive test"</summary>
        public string   Category      { get; set; } = "";

        /// <summary>Tên đầy đủ giao thức: "EN581-1 & EN581-2:2015 (Outdoor Seating for Europe market)"</summary>
        public string   ProtocolName  { get; set; } = "";

        /// <summary>Mã tiêu chuẩn ngắn: "ISTA 1A" | "EN581-1 & EN581-2:2015"</summary>
        public string   StandardCode  { get; set; } = "";

        /// <summary>Loại sản phẩm: "All" | "Seating" | "Table" | "Bed" | "Wood"</summary>
        public string   ProductType   { get; set; } = "All";

        /// <summary>Thị trường: "All" | "Europe" | "US"</summary>
        public string   Market        { get; set; } = "All";

        /// <summary>Tên/mô tả bước kiểm tra: "1. General Safety Requirements." | "2. Seat and back static load test..."</summary>
        public string?  TestProtocol  { get; set; }

        /// <summary>Quy trình thực hiện chi tiết (từng bước)</summary>
        public string?  Procedure     { get; set; }

        /// <summary>Yêu cầu/tiêu chí đạt</summary>
        public string?  Requirements  { get; set; }

        /// <summary>Thứ tự hiển thị</summary>
        public int      SortOrder     { get; set; } = 0;

        public bool     IsActive      { get; set; } = true;
        public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
    }
}
