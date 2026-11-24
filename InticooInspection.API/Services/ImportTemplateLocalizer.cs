using ClosedXML.Excel;

namespace InticooInspection.API.Services;

/// <summary>
/// Dịch file mẫu Excel import theo ngôn ngữ người dùng (en/vi/zh/ms).
/// - Tiêu đề cột: "{nhãn đã dịch}{*} ({MãKỹThuật})" — ImportHelper.ReadHeaderMap đọc mã trong ngoặc,
///   nên file đã dịch vẫn import đúng; file mẫu cũ (chỉ có mã) vẫn dùng được.
/// - Tiếng Anh: trả nguyên file gốc.
/// Thêm cột mới vào template: thêm nhãn vào <see cref="Labels"/> (thiếu nhãn → giữ nguyên mã kỹ thuật).
/// </summary>
public static class ImportTemplateLocalizer
{
    private static readonly string[] Langs = { "vi", "zh", "ms" };

    // Mã cột → [vi, zh, ms]
    private static readonly Dictionary<string, string[]> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CompanyName"]   = new[] { "Tên công ty", "公司名称", "Nama syarikat" },
        ["ShortName"]     = new[] { "Tên viết tắt", "简称", "Nama ringkas" },
        ["BusinessType"]  = new[] { "Loại hình kinh doanh", "业务类型", "Jenis perniagaan" },
        ["Category"]      = new[] { "Danh mục", "类别", "Kategori" },
        ["TaxCode"]       = new[] { "Mã số thuế", "税号", "Kod cukai" },
        ["BusinessRefNo"] = new[] { "Số đăng ký kinh doanh", "营业执照号", "No. pendaftaran perniagaan" },
        ["BusinessRegNo"] = new[] { "Số đăng ký kinh doanh", "营业执照号", "No. pendaftaran perniagaan" },
        ["Phone"]         = new[] { "Điện thoại", "电话", "Telefon" },
        ["PhoneNumber"]   = new[] { "Điện thoại", "电话", "Telefon" },
        ["OfficePhone"]   = new[] { "Điện thoại văn phòng", "办公电话", "Telefon pejabat" },
        ["Website"]       = new[] { "Website", "网站", "Laman web" },
        ["Address1"]      = new[] { "Địa chỉ 1", "地址 1", "Alamat 1" },
        ["Address2"]      = new[] { "Địa chỉ 2", "地址 2", "Alamat 2" },
        ["City"]          = new[] { "Thành phố", "城市", "Bandar" },
        ["State"]         = new[] { "Tỉnh / Bang", "州 / 省", "Negeri / Wilayah" },
        ["Country"]       = new[] { "Quốc gia", "国家", "Negara" },
        ["PostalCode"]    = new[] { "Mã bưu chính", "邮政编码", "Poskod" },
        ["ContactPerson"] = new[] { "Người liên hệ", "联系人", "Nama hubungan" },
        ["ContactName"]   = new[] { "Người liên hệ", "联系人", "Nama hubungan" },
        ["ContactTitle"]  = new[] { "Chức vụ người liên hệ", "联系人职位", "Jawatan hubungan" },
        ["ContactPhone"]  = new[] { "Điện thoại người liên hệ", "联系人电话", "Telefon hubungan" },
        ["ContactEmail"]  = new[] { "Email người liên hệ", "联系人邮箱", "E-mel hubungan" },
        ["Position"]      = new[] { "Chức vụ", "职位", "Jawatan" },
        ["Mobile"]        = new[] { "Di động", "手机", "Telefon bimbit" },
        ["Email"]         = new[] { "Email", "邮箱", "E-mel" },
        ["Notes"]         = new[] { "Ghi chú", "备注", "Catatan" },
        ["Remark"]        = new[] { "Ghi chú", "备注", "Catatan" },
        ["IsActive"]      = new[] { "Hoạt động (TRUE/FALSE)", "启用 (TRUE/FALSE)", "Aktif (TRUE/FALSE)" },
        ["Name"]          = new[] { "Tên nhà cung cấp", "供应商名称", "Nama pembekal" },
        ["Type"]          = new[] { "Loại", "类型", "Jenis" },
        ["Status"]        = new[] { "Trạng thái", "状态", "Status" },
        ["FactoryEvaluationNotes"] = new[] { "Ghi chú đánh giá nhà máy", "工厂评估备注", "Nota penilaian kilang" },
        ["CustomerName"]  = new[] { "Khách hàng (mã/tên)", "客户（编号/名称）", "Pelanggan (kod/nama)" },
        ["VendorName"]    = new[] { "Nhà cung cấp (mã/tên)", "供应商（编号/名称）", "Pembekal (kod/nama)" },
        ["ProductType"]   = new[] { "Loại sản phẩm", "产品类型", "Jenis produk" },
        ["ProductName"]   = new[] { "Tên sản phẩm", "产品名称", "Nama produk" },
        ["ProductCode"]   = new[] { "Mã sản phẩm", "产品编号", "Kod produk" },
        ["ItemNumber"]    = new[] { "Mã hàng", "货号", "Nombor item" },
        ["ProductColor"]  = new[] { "Màu sắc", "颜色", "Warna" },
        ["ProductSize"]   = new[] { "Kích cỡ", "尺码", "Saiz" },
        ["SizeL"]         = new[] { "Dài (L)", "长 (L)", "Panjang (L)" },
        ["SizeW"]         = new[] { "Rộng (W)", "宽 (W)", "Lebar (W)" },
        ["SizeH"]         = new[] { "Cao (H)", "高 (H)", "Tinggi (H)" },
        ["Weight"]        = new[] { "Trọng lượng (kg)", "重量 (kg)", "Berat (kg)" },
        ["PhotoUrl"]      = new[] { "Link ảnh", "图片链接", "Pautan foto" },
        ["EstablishDate"] = new[] { "Ngày tạo (yyyy-MM-dd)", "建立日期 (yyyy-MM-dd)", "Tarikh ditubuhkan (yyyy-MM-dd)" },
        ["InspectorId"]   = new[] { "Mã kiểm hàng viên", "验货员编号", "ID pemeriksa" },
        ["FullName"]      = new[] { "Họ tên", "全名", "Nama penuh" },
        ["UserName"]      = new[] { "Tên đăng nhập", "用户名", "Nama pengguna" },
        ["Password"]      = new[] { "Mật khẩu", "密码", "Kata laluan" },
        ["DateOfBirth"]   = new[] { "Ngày sinh (yyyy-MM-dd)", "出生日期 (yyyy-MM-dd)", "Tarikh lahir (yyyy-MM-dd)" },
        ["Gender"]        = new[] { "Giới tính", "性别", "Jantina" },
        ["Nationality"]   = new[] { "Quốc tịch", "国籍", "Kewarganegaraan" },
        ["IdType"]        = new[] { "Loại giấy tờ", "证件类型", "Jenis pengenalan" },
        ["IdNumber"]      = new[] { "Số giấy tờ", "证件号码", "Nombor pengenalan" },
        ["InspectionStartYear"] = new[] { "Năm bắt đầu kiểm hàng", "开始验货年份", "Tahun mula pemeriksaan" },
        ["Language"]      = new[] { "Ngôn ngữ sử dụng", "掌握语言", "Bahasa" },
        ["CustomerId"]    = new[] { "Mã khách hàng", "客户编号", "ID pelanggan" },
        ["PageAccess"]    = new[] { "Quyền truy cập trang", "页面权限", "Akses halaman" },
        ["Role"]          = new[] { "Vai trò", "角色", "Peranan" },
    };

    // Dòng tiêu đề/chú thích của template Inspector (dòng 1-2)
    private static readonly Dictionary<string, string[]> Texts = new()
    {
        ["INSPECTOR IMPORT TEMPLATE"] = new[] { "MẪU NHẬP KIỂM HÀNG VIÊN", "验货员导入模板", "TEMPLAT IMPORT PEMERIKSA" },
        ["RED header = Required  |  BLUE header = Optional  |  YELLOW row = Example (delete before import)"] = new[]
        {
            "Tiêu đề ĐỎ = Bắt buộc  |  Tiêu đề XANH = Không bắt buộc  |  Dòng VÀNG = Ví dụ (xóa trước khi nhập)",
            "红色表头 = 必填  |  蓝色表头 = 选填  |  黄色行 = 示例（导入前请删除）",
            "Tajuk MERAH = Wajib  |  Tajuk BIRU = Pilihan  |  Baris KUNING = Contoh (padam sebelum import)",
        },
    };

    public static string? Normalize(string? lang)
    {
        lang = lang?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lang)) return null;
        if (lang.Length > 2) lang = lang[..2];
        return Array.IndexOf(Langs, lang) >= 0 ? lang : null;
    }

    /// <summary>Đọc template gốc, dịch tiêu đề cột ở dòng <paramref name="headerRow"/> (và dòng tiêu đề trên đó).</summary>
    public static byte[] Localize(byte[] original, string? lang, int headerRow)
    {
        var l = Normalize(lang);
        if (l == null) return original;   // en / không hỗ trợ → file gốc
        int idx = Array.IndexOf(Langs, l);

        using var input = new MemoryStream(original);
        using var wb = new XLWorkbook(input);
        var ws = wb.Worksheets.First();

        for (int r = 1; r < headerRow; r++)
        {
            foreach (var cell in ws.Row(r).CellsUsed())
            {
                var text = cell.GetString().Trim();
                if (Texts.TryGetValue(text, out var tr)) cell.Value = tr[idx];
            }
        }

        var header = ws.Row(headerRow);
        foreach (var cell in header.CellsUsed())
        {
            var raw = cell.GetString().Trim();
            var key = raw.TrimEnd('*').Trim();
            if (!Labels.TryGetValue(key, out var labels)) continue;
            var star = raw.EndsWith('*') ? "*" : "";
            cell.Value = $"{labels[idx]}{star}\n({key})";
            cell.Style.Alignment.WrapText = true;
        }
        header.Height = Math.Max(header.Height, 32);

        using var output = new MemoryStream();
        wb.SaveAs(output);
        return output.ToArray();
    }
}
