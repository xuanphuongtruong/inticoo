using Microsoft.AspNetCore.Components.Forms;

namespace InticooInspection.Client.Shared
{
    /// <summary>
    /// Thu nhỏ ảnh ngay trên trình duyệt trước khi upload (ảnh điện thoại 4–12 MB → khoảng 1 MB),
    /// để upload nhanh khi mạng tại xưởng yếu. Server (ImageCompressor) vẫn nén lần cuối về cạnh dài 1920px,
    /// nên ở đây để 2560px + JPEG chất lượng cao của canvas — không làm giảm chất lượng ảnh cuối cùng.
    /// File không phải ảnh (PDF...) hoặc trình duyệt không đọc được (HEIC...) → trả lại file gốc.
    /// </summary>
    public static class ImageShrink
    {
        private const int  MaxEdge  = 2560;
        private const long MinBytes = 800 * 1024;   // ảnh nhỏ hơn thì gửi nguyên

        public static async Task<IBrowserFile> ShrinkAsync(IBrowserFile file)
        {
            var ct = file.ContentType?.ToLowerInvariant() ?? "";
            if (!ct.StartsWith("image/") || ct is "image/gif" or "image/svg+xml" || file.Size < MinBytes)
                return file;
            try
            {
                // Blazor tự vẽ lên canvas (đã xoay theo EXIF) và chỉ thu nhỏ, không phóng to
                var small = await file.RequestImageFileAsync("image/jpeg", MaxEdge, MaxEdge);
                return small.Size > 0 && small.Size < file.Size ? small : file;
            }
            catch
            {
                return file;
            }
        }
    }
}
