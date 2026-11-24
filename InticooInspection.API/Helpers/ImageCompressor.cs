using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace InticooInspection.API.Helpers
{
    /// <summary>
    /// Giảm dung lượng ảnh nhưng giữ chất lượng đủ xem chi tiết lỗi khi phóng to.
    /// Luôn output ra JPEG.
    /// </summary>
    public static class ImageCompressor
    {
        // ── Cạnh dài tối đa (px) — áp cho cả ảnh ngang lẫn ảnh dọc ──
        // (trước đây 1920×1080 làm ảnh dọc 3000×4000 chỉ còn 810×1080)
        private const int MaxEdge = 1920;

        // ── Quality: bắt đầu 82 (khó thấy khác bản gốc), chỉ hạ khi ảnh quá nặng, sàn 70 ──
        // (trước đây hạ tới 30 để ép ≤ 420 KB → ảnh nhiều chi tiết bị vỡ khối, nhoè chữ)
        private const int  QualityStart = 82;
        private const int  QualityMin   = 70;
        private const int  QualityStep  = 6;
        private const long SoftMaxBytes = 700 * 1024;   // vượt ngưỡng này mới hạ quality

        // ── Chỉ bật decoder JPEG/PNG/WebP, đọc 1 khung hình ──
        // ImageSharp 3.1.x còn lỗ hổng chưa vá ở TIFF/ICC (bản vá 4.1.2 cần license key khi build Release).
        // Nhận dạng định dạng theo nội dung file, nên file TIFF/BMP/GIF đổi đuôi .jpg cũng bị từ chối
        // (ném UnknownImageFormatException — lớp con của ImageFormatException).
        private static readonly DecoderOptions Decoder = new()
        {
            Configuration = new Configuration(
                new JpegConfigurationModule(),
                new PngConfigurationModule(),
                new WebpConfigurationModule()),
            MaxFrames = 1,
        };

        /// <summary>
        /// Đọc ảnh, xoay theo EXIF, thu nhỏ cạnh dài về ≤ 1920px, bỏ metadata, nén JPEG.
        /// Trả về MemoryStream chứa JPEG đã nén và extension ".jpg".
        /// Ném ImageFormatException nếu nội dung không phải JPEG/PNG/WebP hợp lệ.
        /// </summary>
        public static async Task<(MemoryStream Stream, string Extension)> CompressAsync(Stream input)
        {
            using var image = await Image.LoadAsync(Decoder, input);

            // 1. Xoay ảnh đúng chiều theo EXIF trước khi bỏ metadata
            image.Mutate(x => x.AutoOrient());

            // 2. Thu nhỏ nếu cạnh dài vượt MaxEdge (giữ tỉ lệ, không crop)
            if (image.Width > MaxEdge || image.Height > MaxEdge)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size    = new Size(MaxEdge, MaxEdge),
                    Mode    = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3,
                }));
            }

            // 3. Bỏ EXIF (GPS, thumbnail nhúng, thông tin máy), IPTC, XMP. Giữ ICC để màu không bị lệch.
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile  = null;

            // 4. Nén JPEG 4:2:0; chỉ hạ quality khi ảnh vẫn quá nặng, không xuống dưới QualityMin
            MemoryStream? result = null;
            for (int quality = QualityStart; quality >= QualityMin; quality -= QualityStep)
            {
                var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms, new JpegEncoder
                {
                    Quality   = quality,
                    ColorType = JpegEncodingColor.YCbCrRatio420,
                });

                if (ms.Length <= SoftMaxBytes || quality - QualityStep < QualityMin)
                {
                    result = ms;
                    break;
                }
                await ms.DisposeAsync();
            }

            result!.Position = 0;
            return (result, ".jpg");
        }

        /// <summary>
        /// Kiểm tra content-type có phải ảnh không.
        /// </summary>
        public static bool IsImage(string contentType)
        {
            var ct = contentType.ToLowerInvariant();
            return ct is "image/jpeg" or "image/jpg" or "image/png"
                      or "image/webp";
        }

        /// <summary>
        /// Kiểm tra extension có phải ảnh không.
        /// </summary>
        public static bool IsImageExtension(string ext)
        {
            var e = ext.ToLowerInvariant();
            return e is ".jpg" or ".jpeg" or ".png" or ".webp";
        }
    }
}
