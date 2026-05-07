// ============================================================
//  Đặt tại: InticooInspection.Client/wwwroot/js/ptm-download.js
//  ----------------------------------------------------------
//  Helper tải/ mở file từ byte[] (blob) — dùng từ Razor để bypass
//  Azure Static Web Apps auth gateway (vốn chặn <a target="_blank">).
// ============================================================
window.ptmDownloadBlob = function (bytesBase64Or, contentType, fileName, openInline) {
    try {
        // Blazor truyền byte[] sang JS dạng Uint8Array → blob trực tiếp.
        let blob;
        if (bytesBase64Or instanceof Uint8Array) {
            blob = new Blob([bytesBase64Or], { type: contentType || 'application/octet-stream' });
        } else if (typeof bytesBase64Or === 'string') {
            // fallback: base64
            const bin = atob(bytesBase64Or);
            const arr = new Uint8Array(bin.length);
            for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
            blob = new Blob([arr], { type: contentType || 'application/octet-stream' });
        } else {
            blob = new Blob([bytesBase64Or], { type: contentType || 'application/octet-stream' });
        }

        const url = URL.createObjectURL(blob);

        if (openInline) {
            // Mở trong tab mới (ảnh / pdf / text) — không bị SWA chặn vì là blob: URL local.
            const w = window.open(url, '_blank');
            if (!w) {
                // Trình duyệt chặn popup → fallback tải về.
                triggerDownload(url, fileName);
            }
        } else {
            triggerDownload(url, fileName);
        }

        // Giải phóng URL sau 1 phút (đủ để tab mới load xong).
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (err) {
        console.error('[ptmDownloadBlob] error:', err);
        alert('Open file failed: ' + (err && err.message ? err.message : err));
    }

    function triggerDownload(url, name) {
        const a = document.createElement('a');
        a.href = url;
        a.download = name || 'download';
        document.body.appendChild(a);
        a.click();
        a.remove();
    }
};
