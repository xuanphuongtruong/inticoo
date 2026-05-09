// ============================================================================
// FILE: wwwroot/js/file-download.js
// THÊM script tag vào index.html / _Host.cshtml:
//   <script src="js/file-download.js"></script>
// ============================================================================
//
// Component <ImportExcelButton> gọi hàm này với 3 tham số:
//   downloadFileFromBase64(filename, contentType, base64Data)
// ============================================================================

window.downloadFileFromBase64 = (filename, contentType, base64Data) => {
    const byteChars = atob(base64Data);
    const byteNumbers = new Array(byteChars.length);
    for (let i = 0; i < byteChars.length; i++) {
        byteNumbers[i] = byteChars.charCodeAt(i);
    }
    const blob = new Blob([new Uint8Array(byteNumbers)], { type: contentType });

    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
};
