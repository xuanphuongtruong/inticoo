# InticooInspection.SuperAdmin — khu quản trị nền tảng Inticoo

Làm theo thiết kế `Super Admin.docx` (9 hình: Dashboard, Companies, Subscriptions, Payments, Invoices, Users, Modules,
Reports, Settings) + các màn bổ sung Languages, Storage, Audit Log, Messages, Account. Các màn nằm ở gốc
(`/` = Dashboard, `/companies`, `/login`...), toàn bộ yêu cầu đăng nhập super admin.

```bash
dotnet run --project InticooInspection.SuperAdmin --launch-profile http   # http://localhost:5210
```

Cấu hình bí mật (không nằm trong git) ở `appsettings.Secrets.json` cạnh project/dll hoặc biến môi trường
(`ConnectionStrings__Super`, `Security__TenantSecretKey`, ...). **Hai app phải dùng cùng `Security:TenantSecretKey`**
(và cùng khóa với API chính) — mất khóa là mất chuỗi kết nối DB của khách.

**Chạy thử không đụng DB thật**: `.claude/launch.json` có cấu hình `*-localdb` (LocalDB `InticooInspectionSupper_Dev`,
`Provisioning:Mode=Skip`, `Seed:Demo=true`). Tham số dòng lệnh đè file secret; log khởi động in dòng `DB Super: ...`
để chắc chắn đang nối đúng DB. Chạy Super Admin trước (tạo schema), rồi Website.

`SuperAdmin:Email/FullName/Password` chỉ dùng tạo super admin đầu tiên khi bảng còn trống.

* App này là "chủ" DB Super: khởi động sẽ `Migrate` + seed (module, gói, super admin đầu tiên) và chạy `OverdueWorker`.
* Ô khoảng ngày ở header: Payments/Invoices/Reports/Audit Log lọc ngay tại trang; trang khác mở Reports với khoảng đã chọn.
* Cờ quốc gia: `wwwroot/img/flags/{mã ISO2}.svg` (vẽ đơn giản hóa), mã lạ hiện `xx.svg`.
* Link sang website công khai (gửi link thanh toán...) dùng `Portal:PublicUrl`.
* Chế độ bảo trì (Settings › General) chặn website công khai (trả 503, cache 20 giây), không chặn app này.
* Không viết cứng mã gói/module trong code: màu gói = `Plan.Color` (Settings › Subscription Plans), icon/màu module =
  `PlatformModule.Icon/Color` (Modules › Configure, tên icon lấy từ `Icons.Names`); "Default Plan" và form thêm công ty lấy
  các gói đang Active; link Support ở menu = Settings › General › Support Email. Danh sách trạng thái/phương thức lấy từ
  hằng `*Status.All` / `PaymentMethods.All` trong `Entities.cs`.
* KPI "this month / last month" (Invoices, Payments) dùng `AdminUi.MonthStarts()`; MRR tháng trước = thuê bao đã chạy
  trước đầu tháng và lúc đó chưa hủy (kể cả gói hủy trong tháng này).

## Dung lượng, người dùng, hạn mức gói (23/11/2026)

* **Ai đo**: API (`InticooInspection.API/Services/Usage/UsageWorker.cs`) đo định kỳ (`usage.intervalHours`, mặc định 6 giờ)
  hoặc ngay khi bấm **Measure now** (Storage / Companies › Usage): kích thước DB riêng (data + log), dung lượng + số file
  trong `wwwroot/uploads/t/{mã}` của API, người dùng (tổng / đang hoạt động / khóa / đăng nhập 7-30 ngày), số phiếu kiểm tra,
  CLC, CAPA mở, nhà cung cấp, sản phẩm; kèm ổ đĩa server. Ghi vào `sup.CompanyUsageSnapshots` + `sup.PlatformDiskSnapshots`
  và cột tổng hợp trên `sup.Companies`. DB gốc (khách cũ) hiện là dòng "Legacy (main database)".
* **Không có số viết cứng**: dung lượng nền tảng = ổ đĩa server đo được + `usage.externalCapacityGb`; hạn mức = gói
  (`Plan.StorageGb`, `Plan.MaxUsers`); ngưỡng cảnh báo / số ngày không hoạt động trong Settings › General › Usage & Limits.
* **Chặn khi vượt** (`usage.enforce`): API từ chối tạo / kích hoạt / import người dùng khi số người đang hoạt động đạt
  `MaxUsers`, và từ chối ghi file khi dung lượng đo gần nhất + phần upload từ lúc đo vượt `StorageGb` → 403
  `{code:"plan_limit"}` (Client dịch qua `TA()`). Companies › Usage có ô **tạm bỏ chặn đến ngày** cho khách đang chờ nâng gói.
  Thay đổi có hiệu lực sau tối đa 2 phút (cache).
* **Màn hình**: Dashboard (ô Storage Usage), Storage (theo công ty: DB, file, tăng 30 ngày, dự báo đầy, người dùng, ổ đĩa,
  biểu đồ 90 ngày, xuất Excel), Companies › Usage, Reports (tab Tenants, Users & Activity, Storage Usage, Plan Limits,
  App Usage), chuông cộng số công ty vượt hạn mức.
* **Triển khai**: API phải có `ConnectionStrings__Super` (+ `Security__TenantSecretKey`) thì mới đo / chặn. Nếu có 2 instance
  API (Azure + VPS), chỉ instance giữ file upload của khách được đo: đặt `Usage:WorkerEnabled=false` ở instance còn lại.
  Hai instance cùng bật cũng không đo trùng (khóa qua `usage.lastRunAt`) nhưng số file sẽ sai nếu instance kia không có file.
* **Chạy thử**: cấu hình `inticoo-api-localdb` + `inticoo-superadmin-localdb` + `inticoo-website-localdb` (LocalDB, cấp DB
  khách tiền tố `InticooDev_`). API nay nạp tham số dòng lệnh sau cùng và log `DB DefaultConnection / DB Super` khi khởi động.
