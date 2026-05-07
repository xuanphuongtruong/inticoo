// ============================================================
//  Đặt tại: InticooInspection.Domain/Entities/PerformanceTestMaster.cs
//  ----------------------------------------------------------
//  Đã loại bỏ 4 trường ReferenceFile* (chỉ chứa được 1 file).
//  Reference giờ được lưu ở bảng con PerformanceTestReferences
//  thông qua collection References (1-N).
// ============================================================

namespace InticooInspection.Domain.Entities
{
    public class PerformanceTestMaster
    {
        public int Id { get; set; }

        /// <summary>Nhóm lớn: "Packaging Test" | "Product Test" | ...</summary>
        public string Category { get; set; } = "";

        /// <summary>Tên đầy đủ giao thức</summary>
        public string ProtocolName { get; set; } = "";

        /// <summary>Tên/mô tả bước kiểm tra</summary>
        public string? TestProtocol { get; set; }

        /// <summary>Quy trình thực hiện chi tiết (từng bước)</summary>
        public string? Procedure { get; set; }

        /// <summary>Yêu cầu/tiêu chí đạt</summary>
        public string? Requirements { get; set; }

        // ───── REFERENCE FILES (đính kèm) ─────
        /// <summary>Danh sách file đính kèm (0..N file).</summary>
        public ICollection<PerformanceTestReference> References { get; set; }
            = new List<PerformanceTestReference>();

        /// <summary>Thứ tự hiển thị</summary>
        public int SortOrder { get; set; } = 0;

        public bool     IsActive  { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
