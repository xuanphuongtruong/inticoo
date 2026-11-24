// Tải lần lượt nhiều file (Product Reference → "Download All"). Ưu tiên fetch → blob để ép tải về;
// nếu server không cho CORS thì mở file ở tab mới.
window.issDownloadAll = async function (items) {
    for (const it of items || []) {
        try {
            const r = await fetch(it.url);
            if (!r.ok) throw new Error(r.status);
            const url = URL.createObjectURL(await r.blob());
            const a = document.createElement('a');
            a.href = url; a.download = it.name || 'download';
            document.body.appendChild(a); a.click(); a.remove();
            setTimeout(() => URL.revokeObjectURL(url), 60000);
        } catch {
            const a = document.createElement('a');
            a.href = it.url; a.target = '_blank'; a.rel = 'noopener';
            document.body.appendChild(a); a.click(); a.remove();
        }
        await new Promise(r => setTimeout(r, 400));
    }
};
