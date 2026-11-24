# Kế hoạch: Super Admin theo dõi dung lượng, người dùng và báo cáo sử dụng (23/11/2026)

Yêu cầu: chủ cho thuê cần biết mỗi khách dùng bao nhiêu dung lượng, có bao nhiêu người thuê / người dùng, và các
báo cáo liên quan. Mọi số liệu phải lấy từ dữ liệu thật, không viết cứng.

Đã chốt với anh/chị:
- API tự đo định kỳ rồi ghi kết quả vào DB Super; Super Admin đọc kết quả đó.
- Vượt hạn mức gói thì **chặn** (không cho thêm người dùng / upload file).
- Tổng dung lượng nền tảng = dung lượng ổ đĩa thật của server (API đo), có thêm ô cài đặt để nhập dung lượng thuê ngoài.

## 1. Hiện trạng (đã kiểm tra code)

| Chỗ | Đang làm | Vấn đề |
|---|---|---|
| `ReportService.DashboardAsync(int storageCapacityTb = 10)` | Dashboard/Reports hiện "used of 10 TB" | **10 TB viết cứng** |
| `ProvisioningService.RefreshStorageAsync` | Đo kích thước DB riêng của khách (sys.master_files) khi bấm "Measure" | Bỏ sót file ảnh/tài liệu của khách nằm ở ổ đĩa server API (`wwwroot/uploads/t/{mã}`); không lưu lịch sử nên không vẽ được xu hướng |
| Storage, ngưỡng cảnh báo | "Companies above 80%" | **80% viết cứng** |
| Đếm người dùng | Lấy từ `sup.CompanyUsers` (API đồng bộ khi tạo/sửa user) | User có sẵn trước khi có đồng bộ có thể thiếu; không có số đăng nhập / hoạt động |
| Hạn mức gói | `Plan.MaxUsers`, `Plan.StorageGb` có trong DB | App chính không kiểm tra → khách dùng vượt gói thoải mái |
| Dữ liệu mẫu (`DemoSeeder`) | Dung lượng ngẫu nhiên | Chỉ là dữ liệu demo gắn `IsDemo`, giữ nguyên, có nút xóa sẵn |

## 2. Thu thập số liệu (API, chạy ngầm)

- Bảng mới trong DB Super (migration ở Portal.Core):
  - `sup.CompanyUsageSnapshots` — mỗi khách mỗi lần đo: thời điểm, dung lượng DB (data/log), dung lượng file,
    số file, tổng người dùng / đang hoạt động / bị khóa, số người đăng nhập trong 7 và 30 ngày, lần hoạt động gần nhất,
    số phiếu kiểm tra (tổng, tạo trong tháng), số CAPA mở, số nhà cung cấp, số sản phẩm.
  - `sup.PlatformDiskSnapshots` — ổ đĩa server: tổng, còn trống, dung lượng thư mục uploads.
  - `Companies` thêm cột tổng hợp lần đo gần nhất để danh sách hiển thị nhanh (giữ `StorageUsedGb` hiện có).
- `UsageWorker` trong API: chạy mỗi N giờ (cài đặt trong Settings, mặc định 6 giờ) + khi Super Admin bấm "Measure now"
  (Super Admin ghi một yêu cầu vào DB Super, worker thấy thì đo ngay — 2 app không cần gọi trực tiếp nhau).
  Với từng khách (`TenantMaintenance.AllAsync` + `CreateScope` gắn đúng tenant):
  - DB: `sys.database_files` trong chính DB khách (login của khách đủ quyền).
  - File: duyệt `uploads/t/{mã}` lấy tổng byte và số file.
  - Người dùng / hoạt động: đếm từ bảng Identity và bảng nghiệp vụ trong DB khách.
  - Đồng bộ lại `sup.CompanyUsers` khi phát hiện lệch.
- DB gốc (khách cũ không đăng ký qua Portal) đo riêng, hiển thị là "Legacy (inticoo)", không áp hạn mức.

## 3. Chặn khi vượt hạn mức (API + Client)

- API đọc hạn mức gói đang hoạt động của công ty (cache 2 phút như trạng thái công ty hiện nay).
- **Người dùng**: tạo user / import user / mở khóa user bị từ chối khi số user đang hoạt động ≥ `Plan.MaxUsers`.
- **Dung lượng**: mọi chỗ ghi file (`AzureBlobService`, `CustomerFilesController`, `VendorController`, ảnh phiếu kiểm tra…)
  đi qua một bộ kiểm tra chung: dung lượng đo gần nhất + phần đã upload từ lúc đo ≥ `Plan.StorageGb` → từ chối.
- Lỗi trả về câu tiếng Anh cố định; Client hiển thị qua `TA(...)` và có đủ bản dịch en/vi/zh/ms.
- Client: màn quản lý người dùng và chỗ upload hiện cảnh báo trước khi chạm hạn mức ("đã dùng X/Y").
- Super Admin có công tắc tạm bỏ chặn cho từng công ty (gia hạn tạm khi khách đang chờ nâng gói).
- Cảnh báo sớm ở ngưỡng cài đặt được (mặc định 80%).

## 4. Màn hình Super Admin

- **Dashboard**: ô "Storage Usage" dùng số đo thật — đã dùng / tổng hạn mức đã bán / ổ đĩa server; bỏ 10 TB.
  Thêm ô "Total users" (người dùng của tất cả khách) và "Companies over limit".
- **Storage** (làm lại): mỗi công ty DB + file + tổng, hạn mức gói, % dùng, tăng trong 30 ngày, dự báo số ngày đến khi đầy,
  lần đo gần nhất; biểu đồ tổng dung lượng theo thời gian; thông tin ổ đĩa server; nút "Measure now"; xuất Excel/CSV.
- **Users**: thêm cột/thẻ số người dùng so với giới hạn gói theo công ty, lọc "vượt / sắp vượt".
- **Companies** (panel chi tiết): thẻ Usage — dung lượng, người dùng, hoạt động, biểu đồ 90 ngày của công ty đó.
- **Reports**: thêm tab
  1. *Storage Usage* — theo công ty, theo gói, xu hướng theo tháng.
  2. *Tenants* — số công ty thuê theo trạng thái, mới / hủy theo tháng, tỷ lệ giữ chân.
  3. *Users & Activity* — người dùng theo công ty, đăng nhập 7/30 ngày, công ty không hoạt động quá N ngày.
  4. *Plan Limits* — công ty vượt / sắp vượt dung lượng hoặc số người dùng (gợi ý nâng gói).
  5. *Usage* — số phiếu kiểm tra / CAPA theo tháng theo công ty.
  Tất cả theo bộ lọc khoảng ngày, quốc gia, gói, trạng thái; xuất Excel/CSV.
- **Settings**: chu kỳ đo, ngưỡng cảnh báo %, số ngày coi là "không hoạt động", dung lượng thuê ngoài (GB), bật/tắt chặn.
- Chuông thông báo: cộng số công ty vượt hạn mức.

## 5. Kiểm tra

- LocalDB: Super Admin + API chạy với 2 công ty thử (cấp DB thật trên LocalDB), upload file, tạo user tới giới hạn
  → bị chặn đúng; đo dung lượng khớp kích thước thư mục và DB; xuất báo cáo.
- Không chạy trên DB thật. Xóa dữ liệu thử sau khi xong.

## 6. Việc đang dở từ yêu cầu trước (làm trước khi bắt đầu)

- Màn Resources (upload bài viết/PDF/video): code xong nhưng chưa chạy thử; thư viện HtmlSanitizer 8.1.870 báo lỗ hổng
  bảo mật → nâng lên bản mới rồi mới kiểm tra.
- Đối chiếu lại từng khối 7 trang website + 9 màn Super Admin với mẫu theo góp ý của anh/chị.

## 7. Thứ tự làm

1. Hoàn tất việc dở (mục 6).
2. Bảng + migration (Portal.Core), cài đặt mới.
3. UsageWorker trong API + yêu cầu "Measure now".
4. Màn Storage / Dashboard / Users / Companies / Reports.
5. Chặn hạn mức trong API + Client (+ bản dịch 4 ngôn ngữ).
6. Kiểm tra trên LocalDB, sửa lỗi, cập nhật README/CLAUDE.md.

Không commit, không deploy khi chưa được yêu cầu. Lưu ý khi deploy: API và Client phải lên cùng lúc (endpoint mới),
API production cần `ConnectionStrings__Super` (hiện chưa đặt) thì mới đo và chặn được.
