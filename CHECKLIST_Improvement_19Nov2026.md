# Checklist cải tiến InticooInspection (yêu cầu khách 19/11/2026, ảnh chụp Zalo)

Ký hiệu: ✅ đã sửa, build đạt | 🔍 chưa xem trên thiết bị thật | ⚠️ cần lưu ý

`dotnet build InticooInspection.sln` đạt, `i18n.py check` ✓ OK. Giao diện xem bằng mock `docs/printcheck/mockproxy.py`
(đã thêm `api/defects` giả) ở 1366px và 375px — không đụng API/DB thật.

1. ✅ **Zoom ảnh sản phẩm không chạy** (popup "Product Photo 1/1" ở màn QC): `mobile.css` đặt `img { max-width:100% }`
   cho mọi ảnh → ảnh không phóng to được. `.qc-lb-body img` thêm `max-width:none; flex-shrink:0; margin:auto`
   (margin:auto để khi phóng to vẫn cuộn được sang trái). `InspectionQC.razor`
2. ✅ **Inspection Schedule mở chậm**: sau khi tải danh sách, màn gọi `api/inspections/{id}` (bản đầy đủ, nặng) cho tối đa
   50 job để lấy References — trong khi danh sách đã trả đúng ProductReferences y hệt. Bỏ hẳn bước này; tải CLC và
   danh sách inspector song song với danh sách job; trang 1000 job/lần thay 200. `InspectionScheduleSummary.razor`
   🔍 Chưa đo tốc độ trên server thật
3. ✅ **4 trang Directory cùng kiểu**: tiêu đề bảng nền vàng (#fffde7) như Inspector Profile ở Vendors / Customers / Products
   (trước bị `desktop-list.css` đè thành xanh nhạt); cột Vendor ID bỏ khung badge, chữ thường như các trang khác.
   ⚠️ Cột Status mỗi trang vẫn khác nhau (Inspector/Products dạng pill, Vendors/Customers chữ thường) — khách chưa nói
4. ✅ **Defect Management theo mẫu mới** (desktop): tiêu đề lớn + câu giới thiệu (quy tắc CAPA chuyển vào tooltip),
   4 thẻ thống kê màu có icon (bấm để lọc; thẻ Open ghi số quá hạn), ô tìm có icon + nút lọc (bấm = xoá bộ lọc),
   bảng icon xem/sửa/xoá, phân trang "Show 10 per page / 1 – 10 of 12 records".
   ⚠️ Bỏ cột Approval + View riêng theo mẫu: số chữ ký "✍ n/3" hiện dưới CAPA Status; nút bút chì = Start (job Open) / Continue / Details.
   Laptop 1366px bảng vẫn rộng hơn khung ~70px → cột Action ghim bên phải khi cuộn ngang. Giao diện điện thoại giữ nguyên.
5. ✅ **"Still not show +"** (khối ảnh QC trên điện thoại): ô trống có dấu "+" trên chữ Add Photo, và luôn có thêm 1 ô "+"
   ở cuối lưới (ảnh chọn từ ô này điền vào ô trống đầu tiên, hết ô trống thì thêm ô mới). `QxPhotos` trong `InspectionQC.razor`
6. ✅ **Báo cáo trên điện thoại bị rối**: tái hiện được — ô ảnh cao theo ảnh nên ảnh đang tải/lỗi/ô trống co thành vạch mỏng,
   cộng `min-height:400px` + lề dưới 80px mỗi phần tạo khoảng trắng lớn. Màn ≤860px: khung ảnh cố định 4:3 có nền + biểu tượng,
   ô không có ảnh thì ẩn; ≤600px bỏ min-height/lề footer. Chỉ áp `screen`, in/PDF không đổi. `InspectionReport.razor`
   🔍 Chưa rõ vì sao ảnh trên máy khách không hiện (mạng 4G chậm hay URL ảnh lỗi) — nên mở lại báo cáo đó sau khi deploy

## Deploy
Chỉ đổi Client (Blazor) — không cần deploy API.

## Bổ sung 19/11 (chiều)
7. ✅ **Defect Closure Report in ra 8 trang thay vì 4** (khách in trên Mac). Hai nguyên nhân:
   - `@media (max-width:820px)` (bố cục điện thoại) cũng áp khi in vì khổ giấy ~190mm ≈ 718px → các khối 2 cột
     xếp 1 cột, mỗi trang cao 300–390mm. Đổi thành `@media screen and (max-width:820px)`.
   - Trang in đặt `min-height:296mm`, lề 0 → Safari (in Letter + header/footer) tràn sang trang trắng.
     Giờ lề 10mm, mỗi trang 270mm; Safari (class `html.dcr-safari`) 236mm; thu gọn ảnh/khoảng cách khi in.
   Đo bằng Playwright WebKit + Chromium (mock `api/defects/{id}`): PDF A4 4 trang, Letter giả lập Safari 4 trang.
   🔍 Chưa in thử trên máy Mac thật. Gói: `D:\Deploy\Inticoo\InticooClient19112026_2.7z`
