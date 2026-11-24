# InticooInspection.Website — website công khai bán app Inticoo

Làm theo thiết kế `Website.docx` (7 hình): Home, Features, Pricing, Resources, About, Contact và trang đăng ký 6 bước
(`/register` → `/register/complete/{id}`), PayPal quay về `/pay/paypal/return|cancel`. Super Admin là app riêng
(`InticooInspection.SuperAdmin`); phần dùng chung (DB, thanh toán, cấp DB cho khách) ở `InticooInspection.Portal.Core`.

```bash
dotnet run --project InticooInspection.Website --launch-profile http      # http://localhost:5200
```

Cấu hình bí mật (không nằm trong git) ở `appsettings.Secrets.json` cạnh project/dll hoặc biến môi trường
(`ConnectionStrings__Super`, `Security__TenantSecretKey`, ...). **Hai app phải dùng cùng `Security:TenantSecretKey`**
(và cùng khóa với API chính) — mất khóa là mất chuỗi kết nối DB của khách.

**Chạy thử không đụng DB thật**: `.claude/launch.json` có cấu hình `*-localdb` (LocalDB `InticooInspectionSupper_Dev`,
`Provisioning:Mode=Skip`, `Seed:Demo=true`). Tham số dòng lệnh đè file secret; log khởi động in dòng `DB Super: ...`
để chắc chắn đang nối đúng DB. Chạy Super Admin trước (tạo schema), rồi Website.

Tùy chọn (`appsettings.json`): `Portal:AppUrl` (nút Login/Sign In → app Inticoo chính), `Payments:Mode`
(`Test` = cổng thẻ giả lập, thẻ `4242 4242 4242 4242`), `PayPal:*`, `Provisioning:*` (website cũng cấp DB cho khách
ngay khi thanh toán thẻ/PayPal thành công nên cần quyền tạo DB/login như Super Admin).

## Ảnh và giao diện

* Ảnh chụp ở `wwwroot/img/photos/` được **cắt từ chính hình thiết kế** của khách (độ phân giải thấp). Có ảnh gốc nét hơn
  thì chép đè **cùng tên file** là xong (hero-dashboard.png, home-*.jpg, ind-*.jpg, about-*.jpg, contact-hero.jpg,
  res-*.jpg, learn-1..4.jpg, feat-*.jpg).
* Cờ ngôn ngữ: `wwwroot/img/flags/*.svg`. Icon: SVG nội tuyến (`Icons.Svg`), không dùng CDN (CSP `script-src 'self'`).
* Giá, số user, dung lượng, module của gói lấy từ DB (Super Admin › Settings › Subscription Plans), không viết cứng.
  Bảng so sánh ở Pricing suy ra từ module của gói (API Integration = Professional = có Dedicated Account Manager).
* Nút tài liệu ở Resources (Download PDF, View Guide...) đang mở form liên hệ chủ đề "Request a Resource" vì chưa có file
  PDF/bài viết thật từ khách.

## Dịch

Khóa dịch = câu tiếng Anh trong `L["..."]`/`L.F("...")`. Chuỗi trong mảng tuple của .cshtml (vd. `("doc", "Inspection Scheduling", ...)`)
được `tools/extract_keys.py` tự bắt nếu bắt đầu bằng chữ hoa. Thêm bản dịch vào `tools/tr_a.py`/`tr_b.py`/`tr_c.py`
rồi `python tools/build_translations.py` (chạy trong thư mục project, phải ra "đã dịch N/N"). File dịch nạp lúc khởi động →
sửa xong phải chạy lại app.

## Triển khai

`dotnet publish -c Release` → IIS (ASP.NET Core Hosting Bundle). Hiện production vẫn chạy bản gộp cũ
(`InticooInspection.Portal`, IIS port 19116 trên 14.225.8.231) — khi chuyển sang bản tách: deploy Website vào site cũ,
tạo site IIS mới cho Super Admin, chép `appsettings.Secrets.json` cho cả hai.
