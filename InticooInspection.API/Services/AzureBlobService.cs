using Microsoft.AspNetCore.Hosting;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Lưu file vào ĐĨA LOCAL của server (wwwroot/uploads/{folder}) và trả về URL tương đối
    /// dạng "/uploads/{folder}/{fileName}". Client sẽ tự ghép ApiBaseUrl (inticooapi.thuphuc.com).
    ///
    /// Trước đây service này đẩy lên Azure Blob Storage; sau khi bỏ Azure đã chuyển sang lưu
    /// local. Giữ nguyên TÊN CLASS và CHỮ KÝ phương thức để các controller đang dùng
    /// (FileController, UploadController, ProductController) không phải sửa.
    /// </summary>
    public class AzureBlobService
    {
        private readonly string _uploadsRoot;

        public AzureBlobService(IWebHostEnvironment env)
        {
            var wwwroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            _uploadsRoot = Path.Combine(wwwroot, "uploads");
            Directory.CreateDirectory(_uploadsRoot);
        }

        /// <summary>
        /// Lưu <paramref name="stream"/> vào wwwroot/uploads/{folder}/{fileName}.
        /// Trả về URL tương đối "/uploads/{folder}/{fileName}".
        /// </summary>
        public async Task<string> UploadAsync(string folder, string fileName, Stream stream, string contentType)
        {
            var folderPath = Path.Combine(_uploadsRoot, folder);
            Directory.CreateDirectory(folderPath);

            var fullPath = Path.Combine(folderPath, fileName);
            if (stream.CanSeek) stream.Position = 0;

            await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                await stream.CopyToAsync(fs);

            // URL tương đối — đồng nhất với cách CustomerFilesController đang dùng
            return $"/uploads/{folder}/{fileName}";
        }

        /// <summary>Xóa file wwwroot/uploads/{folder}/{fileName} nếu tồn tại.</summary>
        public Task DeleteAsync(string folder, string fileName)
        {
            var fullPath = Path.Combine(_uploadsRoot, folder, fileName);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            return Task.CompletedTask;
        }
    }
}
