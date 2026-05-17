/**
 * PDF / File download helpers.
 *
 * Đặt file này ở: wwwroot/js/pdf-download.js
 * Reference trong index.html (hoặc _Host.cshtml nếu Blazor Server):
 *
 *     <script src="js/pdf-download.js"></script>
 *
 * Đặt SAU dòng <script src="_framework/blazor.webassembly.js"></script>
 * (hoặc blazor.server.js) để đảm bảo Blazor JS interop đã sẵn sàng.
 */

(function () {
    'use strict';

    /**
     * Tải file xuống máy user từ byte array do Blazor truyền sang.
     *
     * @param {string} fileName    - Tên file (đã sanitize phía C#).
     * @param {string} contentType - MIME type, vd: "application/pdf".
     * @param {Uint8Array|number[]} bytes - Mảng byte. Blazor truyền byte[] sẽ tự convert.
     */
    window.downloadFileFromBytes = function (fileName, contentType, bytes) {
        try {
            // Blazor có thể truyền sang Array hoặc Uint8Array tùy version.
            // Wrap lại để chắc chắn là binary view, không phải JSON serialized array.
            const view = bytes instanceof Uint8Array
                ? bytes
                : new Uint8Array(bytes);

            const blob = new Blob([view], { type: contentType || 'application/octet-stream' });
            const url  = URL.createObjectURL(blob);

            // Tạo anchor ẩn để trigger download. Phương pháp này hoạt động ở
            // tất cả browser hiện đại (Chrome, Edge, Firefox, Safari).
            const a = document.createElement('a');
            a.href     = url;
            a.download = fileName || 'download';
            a.style.display = 'none';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);

            // Revoke URL sau khi click để giải phóng memory.
            // setTimeout vì Safari đôi khi cần delay nhỏ trước khi revoke.
            setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
        } catch (err) {
            console.error('[downloadFileFromBytes] Failed:', err);
            throw err; // re-throw để Blazor catch
        }
    };
})();
