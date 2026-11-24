# Checklist cải tiến InticooInspection (theo yêu cầu khách 2/11 + 4/11/2026)

Ký hiệu: ✅ đã sửa, build đạt | ⚠️ làm một phần | ❌ chưa làm | 🔍 chưa kiểm tra bằng mắt trên trình duyệt

Ghi chú chung: `dotnet build InticooInspection.sln` 0 lỗi, `i18n.py check` ✓ OK. Đã xem thật trên trình duyệt (đăng nhập admin, 1366px):
Inspection Schedule (KPI, tab Today/7/14/Overdue, thẻ job), Summary Inspections (bảng vừa khít 1366px, không cuộn ngang, cột Result), trang CLC.
Đã xem thêm: form New Vendor (gộp thẻ), phân trang Performance Test, menu dưới + KPI Schedule ở 375px, Vendor Performance Overview.
Wizard QC (job PST-JN100127-26, 375px, không lưu gì): toast lỗi xuống 2 dòng đọc đủ chữ; bấm Back thì toast tự xoá. Chưa tới được bước Colour Swatch / Performance Test vì bước A đòi nhập số lượng + ảnh.
Còn **chưa xem bằng mắt**: bố cục Colour Swatch / Performance Test trên điện thoại, quyền Start của tài khoản QC, form Product/Inspector, tab Report Settings — vẫn 🔍. Deploy: API và Client phải cùng phiên bản.

## A. Phân quyền
- ✅ 🔍 `ModulePermissions.cs`: Inspection Schedule cho phép "Edit (Use Module)" (MaxLevel = Edit); thêm `PermAction.Use`; role Customer mặc định chỉ View
- ✅ 🔍 `InspectionController`: 4 endpoint QC (UpdateStep, UpdateOverall, SaveQcResult, SaveQcResultDraft) chấp nhận quyền Inspection Schedule = Edit
- ✅ 🔍 `InspectionScheduleSummary.razor`: nút Start hiện khi Admin / Inspector (quyền cũ) / tài khoản không phải Customer có Edit ở Inspection Schedule
- ⚠️ Tài khoản đã có không tự đổi quyền; admin vào Users chọn "Edit (Use Module)". Tài khoản cũ (chưa có JSON quyền) suy từ PageAccess nên sẽ thành Edit ở Inspection Schedule (Customer không Start được vì bị loại theo role)

## B. Inspection Schedule
- ✅ 🔍 Hàng KPI Total Jobs (14 ngày) + PPI/DPI/PSI/CLC (CLC đếm từ `api/clc`, chỉ job chưa Submitted có ngày load dự kiến trong 14 ngày)
- ✅ 🔍 Today chỉ job đúng hôm nay; thêm tab "Overdue (n)" khi có job quá hạn; 7 days / 14 days dùng cùng thẻ job như Today, nhóm theo ngày (bảng 14 ngày chỉ còn dùng khi in/Export PDF)
- ✅ 🔍 Màn chi tiết job: thêm header (mã job, loại, "View Only") và dải Inspection Date / Category / PO / Product Code
- ✅ Job CLC hiện trong danh sách Schedule (khối "Container Loading Check" dưới các thẻ, theo tab Today/7/14/Overdue; đã xem ở 375px), bấm vào mở `/clc/{id}`
- ✅ 🔍 Trang `/clc/{id}`: dải thông tin (ngày load, container, loại, địa điểm) + bảng "Products / Items in Container" có tổng số thùng ở chế độ kiểm. 7 Inspection Points vẫn là các bước kiểm hiện có
- ✅ 🔍 Nút "Download All" ở Product Reference (tải lần lượt; nếu server file không cho CORS thì mở từng file ở tab mới)
- ✅ 🔍 "Không thấy tên mình khi tự tạo job": dropdown Inspector ở InspectionNew có thêm chính người đang đăng nhập (kể cả chưa có mã Inspector). Schedule chỉ lọc theo InspectorId với role Inspector (có mã), các role khác thấy toàn bộ nên job tự tạo vẫn hiện
- ✅ 🔍 Bỏ cắt chữ (ellipsis) ở dropdown chọn inspector

## C. QC trên điện thoại
- ✅ 🔍 Toast lỗi không còn bị cắt chữ (xuống dòng, tối đa chiều rộng màn hình)
- ✅ 🔍 Nguyên nhân thông báo sai bước: Skip và Back không xoá `_saveMsg` của bước trước → đã xoá
- ✅ 🔍 C-iv Colour Swatch và D Performance Test xếp dọc ≤ 640px (Material + Result một hàng, Remark dưới; nhãn Test Item/Protocol/Requirement... xếp dọc)
- ✅ 🔍 C-i Product Size ≤ 640px: nhãn chip, L/W/H đều nhau, Result cùng hàng với ô chọn
- ✅ Bản nháp QC lưu trước khi tách "Product Weight" được dịch đúng bước khi mở lại (thêm `stepSchema` = 2 vào payload nháp; nháp cũ không có thì +1 từ sau C-i)

## D. Directory / form
- ✅ 🔍 Form Product / Inspector / Vendor / Customer gộp thành 1 thẻ liền (CSS chung `party-profile.css`: `.pf-cols`/`.pf-card2`)
- ✅ 🔍 Report Settings: bỏ ô soạn Email Message + Available Fields (giữ Language, Subject, Preview Email, Save Template). `RichEmailEditor` không còn dùng
- ✅ 🔍 Product: bộ lọc category chỉ liệt kê category đang có dữ liệu; nhãn mặc định "All Type"
- ✅ 🔍 Vendor: bộ lọc "All Types" liệt kê Business Type có trong dữ liệu (API `businessTypes`, tham số `businessType`)

## E. Bảng / layout / menu
- ✅ 🔍 Inspections: bỏ cột Inspector ID, thêm cột Result (Pass/Fail), cột Customer/Vendor min-width để hiện đủ tên
- ✅ 🔍 Performance Test: sửa phân trang chồng lên bảng (thiếu CSS `.lp-pager` ngoài trang có `.lp-head`)
- ✅ 🔍 Menu dưới mobile: Dashboard · Inspection Schedule · Summary · CLC · Defects · Performance Test (Directory vào menu ☰); submenu CLC trong Summary đổi tên "CLC Reports"
- ⚠️ "Phone Format – Remove icon": hiểu mơ hồ nên chưa bỏ icon ở thanh menu dưới — cần hỏi lại khách icon nào

## F. Performance Review
- ✅ 🔍 VendorReview: header (tiêu đề giữa, nút bên phải) và thanh Vendor Name / Vendor ID một hàng, giống Inspector

## G. CLC
- ✅ 🔍 KPI: Total / Completed / New / Delay / Pass / Fail (API thêm `stats.delay`, `createdAt`)
- ✅ 🔍 Bảng: thêm cột No, Receive Date, View (mắt); Result đứng trước Status
- ✅ 🔍 Bảng CLC: nút Edit/Start/Delete gộp vào menu "⋯"

## Việc nhỏ từ checklist 28/10 đã làm tiếp
- ✅ Import Excel Vendor: có cột `BusinessType` (nhiều giá trị cách nhau , ; / |; chỉ nhận Manufacturer/Exporter/Supplier/Trading Company/Other); file mẫu `Vendors_Import_Template.xlsx` thêm cột này (cột cuối)
- ✅ 🔍 Contacts: hiện dòng "Main Contact Person: <tên>" và đổi cột "Primary" thành "Main Contact"
- ✅ 🔍 Inspector: ô chọn mã vùng điện thoại bên cạnh số di động (giữ nguyên các chữ số đã nhập)
- ❌ PDF trên mobile với server thật: chưa thử (cần server production)
- ❌ "Phone Format – Remove icon": chờ khách chỉ rõ icon nào

## Đã xem thật ngày 6/11 (admin, desktop)
Ô mã vùng Inspector (đổi +60 → +84 giữ nguyên số), trang CLC (KPI + icon đúng, menu "⋯" dạng fixed không bị cắt), trang chi tiết CLC (dải thông tin + bảng Products/Items), Contacts Vendor (dòng Main Contact Person + cột Main Contact).
Chưa xem: nút Download All, Product Size/Colour Swatch/Performance Test trên điện thoại, nút Start của tài khoản QC.

## Ảnh khách gửi 4/11 (iPhone Safari) — đã sửa 6/11, 🔍 chưa xem trên iPhone thật
- ✅ Menu dưới: chữ "Performance Test" bị cắt → font 10px, cho phép ngắt chữ (`MainLayout.razor`)
- ✅ Thẻ job CLC trên điện thoại: nút Delete tràn ra ngoài thẻ → cho xuống dòng (`clc.css` `.clc-jc-foot/.clc-jc-act`)
- ✅ Dashboard điện thoại: ô lọc "All Factories/All Customers/Nov 2026" bị cắt chữ → select tự vẽ mũi tên, font 12px (Safari)
- ✅ Dashboard: thẻ Total Customers (khoanh tím) có icon ép chữ xuống nhiều dòng → bỏ chế độ wide, cùng kiểu thẻ Total Vendors; chỉ thẻ Total Quantity Shipped chiếm cả hàng
- ℹ️ Các ảnh còn lại (phân quyền Edit cho Inspection Schedule, bảng Summary có cột Result/KPI Pass-Fail, KPI Schedule) đã làm ở các mục A/B/E phía trên — bản khách chụp là bản chưa deploy, cần deploy lại cả API + Client
- ✅ Phân quyền: thêm module "Container Loading Check" (Create/Edit/Delete) vào màn Create/Edit User, nằm sau Inspection Schedule. Create = tạo/sửa/giao job request; Edit = thực hiện kiểm. Trước đây CLC đi theo Job Request + Summary. Tài khoản cũ tự kế thừa quyền Inspection Summary; role Customer mặc định chỉ View. Menu CLC ẩn theo quyền mới. 🔍 chưa xem trên trình duyệt

## Ảnh khách gửi 2/11 — phần còn thiếu, làm 7/11 (build đạt, 🔍 chưa xem bằng mắt)
- ✅ Defect Management (desktop): các cột Inspection / Factory / Product / Defect Type / Responsible tự xuống dòng, bỏ cắt "…" (`DefectManagement.razor`)
- ✅ QC điện thoại: các bước dạng "nhãn + ô chọn" (B-v Assembly, B-vi Hardware, B-vii Hangtag…) — nhãn chiếm 32%, ô chọn giãn hết phần còn lại (trước đó `max-width:100px` làm cắt chữ) (`InspectionQC.razor`, khối media ≤640px)
