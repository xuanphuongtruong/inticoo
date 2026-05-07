// ============================================================
//  Đặt tại: InticooInspection.Domain/Entities/PerformanceTestReference.cs
//  ----------------------------------------------------------
//  Bảng con của PerformanceTestMaster — mỗi dòng là 1 file
//  đính kèm (Reference). Cho phép NHIỀU file / 1 master.
// ============================================================

namespace InticooInspection.Domain.Entities
{
    public class PerformanceTestReference
    {
        public int Id { get; set; }

        /// <summary>FK trỏ về PerformanceTestMaster.Id</summary>
        public int PerformanceTestMasterId { get; set; }

        /// <summary>Navigation (optional)</summary>
        public PerformanceTestMaster? Master { get; set; }

        /// <summary>Tên file gốc khi upload (vd: "EN581-1_2015.pdf")</summary>
        public string FileName { get; set; } = "";

        /// <summary>Mime type (vd: "application/pdf")</summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>Nội dung file lưu dạng byte array (BLOB)</summary>
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        /// <summary>Kích thước file (bytes)</summary>
        public long FileSize { get; set; }

        /// <summary>Thứ tự hiển thị trong list file</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>Thời điểm upload</summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
