// File này đặt vào wwwroot/js/pdf-download.js
// Nhớ thêm <script src="js/pdf-download.js"></script> vào index.html / _Host.cshtml
//
// Hàm này nhận chuỗi base64 từ Blazor (qua JSInterop) và trigger download
// trong browser bằng cách tạo Blob → object URL → anchor click.
window.downloadPdfFromBase64 = function (base64, fileName) {
    try {
        // Decode base64 → byte array
        const binaryString = atob(base64);
        const len = binaryString.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }

        // Tạo Blob PDF
        const blob = new Blob([bytes], { type: 'application/pdf' });
        const url = URL.createObjectURL(blob);

        // Trigger download
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);

        // Giải phóng object URL sau 1s (đủ cho download bắt đầu)
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (err) {
        console.error('PDF download error:', err);
        alert('Failed to download PDF: ' + err.message);
    }
};
