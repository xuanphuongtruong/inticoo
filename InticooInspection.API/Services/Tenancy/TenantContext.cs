namespace InticooInspection.API.Services.Tenancy
{
    /// <summary>Một công ty khách đã được Portal cấp database riêng.</summary>
    public sealed record TenantInfo(Guid Id, string Code, string Name, string Status, string ConnectionString);

    /// <summary>
    /// Công ty (database) của request / scope hiện tại. Mặc định = DB gốc (<c>ConnectionStrings:DefaultConnection</c>),
    /// tức khách đang dùng từ trước khi có Portal. Middleware đặt tenant theo claim <c>inticoo_company</c> của JWT;
    /// worker / tác vụ nền tự đặt trước khi lấy <c>AppDbContext</c>.
    /// </summary>
    public sealed class TenantContext
    {
        private readonly string _defaultCs;
        private bool _frozen;

        public TenantContext(IConfiguration cfg)
        {
            _defaultCs = cfg.GetConnectionString("DefaultConnection") ?? "";
            ConnectionString = _defaultCs;
        }

        public TenantInfo? Tenant { get; private set; }
        public bool IsDefault => Tenant == null;
        public string ConnectionString { get; private set; }

        /// <summary>Khóa phân biệt tenant (cache, log). Rỗng = DB gốc.</summary>
        public string Key => Tenant?.Code ?? "";

        /// <summary>URL gốc của file upload: "/uploads" (DB gốc, giữ đường dẫn cũ) hoặc "/uploads/t/{code}".</summary>
        public string UploadsUrl => Tenant == null ? "/uploads" : $"/uploads/t/{Tenant.Code}";

        /// <summary>Thư mục vật lý tương ứng với <see cref="UploadsUrl"/>.</summary>
        public string UploadsDir(string webRoot) =>
            Tenant == null ? Path.Combine(webRoot, "uploads") : Path.Combine(webRoot, "uploads", "t", Tenant.Code);

        /// <summary>Đặt tenant. Phải gọi TRƯỚC khi AppDbContext của scope được tạo (nếu không sẽ nối nhầm DB).</summary>
        public void Set(TenantInfo? tenant)
        {
            if (_frozen && tenant?.Id != Tenant?.Id)
                throw new InvalidOperationException("TenantContext đã được dùng để tạo AppDbContext — không đổi tenant giữa chừng được.");
            Tenant = tenant;
            ConnectionString = tenant?.ConnectionString ?? _defaultCs;
        }

        /// <summary>Gọi khi AppDbContext đọc chuỗi kết nối — từ đây không cho đổi tenant nữa.</summary>
        internal string Freeze() { _frozen = true; return ConnectionString; }
    }
}
