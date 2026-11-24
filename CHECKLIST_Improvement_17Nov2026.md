# Checklist cải tiến InticooInspection (yêu cầu khách 17/11/2026)

Nguồn: `15 Dec 2.docx` + `New Request on Inspection Process. Better Design.docx` (toàn ảnh chụp + chú thích).
Ký hiệu: ✅ đã sửa, build đạt | 🔍 chưa xem trên thiết bị thật | ⚠️ cần lưu ý

`dotnet build InticooInspection.sln` đạt, `i18n.py check` ✓ OK. Giao diện đã xem trên trình duyệt với API giả
(mock cùng origin, không đụng DB/API thật) ở 1280px và 375px.

## File "15 Dec 2"
- ✅ Menu trái: bấm Product Inspection không còn sáng kèm Inspection Reports (CLC cũng vậy). Bỏ cơ chế "sáng theo cặp";
  `/inspections/new` + `/inspections/{id}/edit` → Job Requests; `/inspections`, chi tiết, báo cáo → Summary (`MainLayout.razor` `ItemActive`)
- ✅ In báo cáo trên MacBook (Safari) bị tràn trang (trang 2 chỉ có bảng kết luận + footer, 33 trang thay vì 26).
  Nguyên nhân khả dĩ: Safari bỏ qua `@page size: A4`, in theo khổ mặc định của máy (thường Letter, thấp hơn A4 ~18mm) + header/footer của Safari.
  Thu gọn khi in: ảnh bìa 85→52mm, ô ảnh 68→56mm, bảng (result/spec/kv/perf/aql/carton/qty) padding 4px 8px, chữ 11px.
  Đo bằng Playwright **WebKit** (engine Safari) + Chromium với báo cáo giả đầy ảnh (28 phần): mỗi phần cao tối đa 228.9mm,
  vừa A4 (277mm), Letter lề 12.7mm (254mm) và Letter + header/footer Safari (244mm); PDF A4 và Letter đều 28 trang.
  🔍 Chưa in thử trên máy Mac thật (script: scratchpad `printcheck.py` + mock `mockproxy.py`, id 2000)
- ✅ KPI Inspector Schedule trở lại bố cục cũ khách thích: desktop 5 thẻ đều nhau; điện thoại Total Jobs 1 hàng + PPI/DPI/PSI/CLC 1 hàng, nhỏ lại vừa khung.
  Số liệu (đã chốt 17/11): job **chưa hoàn thành** của năm hiện tại (New/Delay), không tính Completed/Cancel/Draft;
  PPI/DPI/PSI/CLC đếm cùng cách nên cộng lại bằng Total. Lưới tháng "TOTAL JOB" bên dưới vẫn là tổng mọi job trong năm
- ✅ Product Inspection theo CLC: 2 nút **Save as Draft** / **Assign & Send to Inspector** (màn tạo + màn sửa job nháp).
  Trạng thái mới `InspectionStatus.Draft = 9` (không dùng 4 vì DB cũ từng có "Pending"=4). Không cần migration (cột int).
  Job Draft bị ẩn bằng global query filter ở `InspectionConfiguration` → không lên lịch inspector, dashboard, thống kê, mail;
  chỉ danh sách/chi tiết/sửa/xoá/sinh JobNumber dùng `IgnoreQueryFilters()`. Danh sách Inspections có badge + lọc "Draft", không có nút Cancel cho Draft.
  Lưu nháp không bắt chọn inspector; Assign & Send bắt buộc. ⚠️ Phần API chưa chạy thử thật (không chạy API local vì MigrateAsync + mail worker vào DB thật)
- ✅ Khung Inspector Assignment của CLC nền vàng giống Product Inspection (`clc.css` `.clc-card-office`)
- ✅ Footer (www.inticoo.com + miễn trừ + số trang) luôn ở đáy trang khi Print/Save PDF: mỗi phần cao 270mm (Chrome/Edge/PDF server, A4)
  hoặc 236mm (Safari — có thể in Letter + header/footer), footer đẩy xuống cuối; bỏ padding layout chung khi in (trước làm lệch trang 1 + trang trắng cuối).
  Đo: PDF A4 28 trang/28 phần, footer cùng vị trí 270.9mm mọi trang; WebKit mỗi phần 236mm, không tràn. Trên Safari khổ A4 footer sẽ cách đáy ~4cm (đánh đổi để không tràn khi Safari in Letter)
- ✅ Xem báo cáo trên điện thoại (≤600px): ẩn footer (www.inticoo.com + miễn trừ + số trang). Khi in/PDF vẫn giữ footer

## File "Better Design" (QC trên điện thoại, `InspectionQC.razor`)
- ✅ Tiêu đề bước dạng dải xanh (áp cho mọi bước)
- ✅ B-i Carton Size / C-i Product Size: khung Specification (xanh) + Actual (xanh lá), thứ tự Spec → Actual → Result → Remark → Photos
- ✅ B-iii Carton Weight / C-ii Product Weight: nhãn chip màu (Spec xanh, Actual xanh lá, Result tím), ô Result đổi màu theo Pass/Fail/N/A
- ✅ D Performance Testing: thêm ô **Result** (Pass/Fail/N/A — đã có sẵn trong JSON lưu và trên báo cáo, trước đây thiếu ô nhập), Remark thành textarea lớn
- ✅ Inspector Comments: textarea lớn + đếm 0/500; Final Result 3 thẻ có icon + dấu chọn
- ✅ Khối ảnh chung `QxPhotos`: "Photos (min. N) x/N", ô trống ghi "Add Photo", đủ ảnh thì có ô "+ Add Photo"
- ⚠️ Theo số tối thiểu ghi trên mẫu, Next giờ **chặn** khi thiếu ảnh: Carton Size ≥ 2, Carton Weight ≥ 1, Product Weight ≥ 1 (trước đây không bắt buộc)
- 🔍 Chưa xem trên iPhone thật

## Deploy
Phải deploy cả API + Client (Client gửi `isDraft`; API cũ sẽ bỏ qua và tạo job thường).
