using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace InticooInspection.API.Helpers
{
    /// <summary>
    /// Compress/resize ảnh về target size (~300-400 KB).
    /// Luôn output ra JPEG để dung lượng nhỏ nhất.
    /// </summary>
    public static class ImageCompressor
    {
        // ── Giới hạn kích thước tối đa (px) ─────────────────────────
        private const int MaxWidth  = 1920;
        private const int MaxHeight = 1080;

        // ── Target dung lượng ────────────────────────────────────────
        private const long TargetBytes = 380 * 1024;   // 380 KB
        private const long MaxBytes    = 420 * 1024;   // 420 KB — ceiling

        // ── Quality range ────────────────────────────────────────────
        private const int QualityStart = 82;
        private const int QualityMin   = 30;
        private const int QualityStep  = 6;

        /// <summary>
        /// Đọc ảnh từ stream, resize nếu quá lớn, nén JPEG về ≤ 400 KB.
        /// Trả về MemoryStream chứa JPEG đã nén và extension ".jpg".
        /// </summary>
        public static async Task<(MemoryStream Stream, string Extension)> CompressAsync(Stream input)
        {
            using var image = await Image.LoadAsync(input);

            // 1. Resize nếu ảnh quá rộng/cao
            if (image.Width > MaxWidth || image.Height > MaxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxWidth, MaxHeight),
                    Mode = ResizeMode.Max   // giữ tỉ lệ, không crop
                }));
            }

            // 2. Nén JPEG với quality giảm dần cho đến khi ≤ TargetBytes
            int quality = QualityStart;
            MemoryStream? result = null;

            while (quality >= QualityMin)
            {
                var ms = new MemoryStream();
                var encoder = new JpegEncoder { Quality = quality };
                await image.SaveAsJpegAsync(ms, encoder);

                if (ms.Length <= MaxBytes || quality == QualityMin)
                {
                    result = ms;
                    break;
                }

                await ms.DisposeAsync();
                quality -= QualityStep;
            }

            // Fallback: nếu vẫn chưa có result (không bao giờ xảy ra)
            if (result == null)
            {
                result = new MemoryStream();
                await image.SaveAsJpegAsync(result, new JpegEncoder { Quality = QualityMin });
            }

            result.Position = 0;
            return (result, ".jpg");
        }

        /// <summary>
        /// Kiểm tra content-type có phải ảnh không.
        /// </summary>
        public static bool IsImage(string contentType)
        {
            var ct = contentType.ToLowerInvariant();
            return ct is "image/jpeg" or "image/jpg" or "image/png"
                      or "image/webp" or "image/gif" or "image/bmp"
                      or "image/tiff";
        }

        /// <summary>
        /// Kiểm tra extension có phải ảnh không.
        /// </summary>
        public static bool IsImageExtension(string ext)
        {
            var e = ext.ToLowerInvariant();
            return e is ".jpg" or ".jpeg" or ".png" or ".webp"
                      or ".gif" or ".bmp" or ".tiff";
        }
    }
}
