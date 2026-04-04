// wwwroot/js/signature.js
// Signature canvas functions — loaded via OnAfterRenderAsync in Blazor

(function () {
    let drawing = false;
    let lastX = 0, lastY = 0;

    function initCanvas(id) {
        const canvas = document.getElementById(id);
        if (!canvas || canvas._sigInited) return;
        canvas._sigInited = true;
        const ctx = canvas.getContext('2d');
        ctx.strokeStyle = '#1a1a2e';
        ctx.lineWidth = 2;
        ctx.lineCap = 'round';

        function getPos(e) {
            const r = canvas.getBoundingClientRect();
            const src = e.touches ? e.touches[0] : e;
            return { x: src.clientX - r.left, y: src.clientY - r.top };
        }
        function start(e) {
            e.preventDefault();
            drawing = true;
            const p = getPos(e);
            lastX = p.x; lastY = p.y;
        }
        function move(e) {
            if (!drawing) return;
            e.preventDefault();
            const p = getPos(e);
            ctx.beginPath();
            ctx.moveTo(lastX, lastY);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            lastX = p.x; lastY = p.y;
        }
        function stop() { drawing = false; }

        canvas.addEventListener('mousedown', start);
        canvas.addEventListener('mousemove', move);
        canvas.addEventListener('mouseup', stop);
        canvas.addEventListener('mouseleave', stop);
        canvas.addEventListener('touchstart', start, { passive: false });
        canvas.addEventListener('touchmove', move, { passive: false });
        canvas.addEventListener('touchend', stop);
    }

    window.getSignatureDataUrl = function (id) {
        initCanvas(id);
        const c = document.getElementById(id);
        return c ? c.toDataURL('image/png') : '';
    };

    window.clearCanvas = function (id) {
        const c = document.getElementById(id);
        if (c) {
            const ctx = c.getContext('2d');
            ctx.clearRect(0, 0, c.width, c.height);
        }
    };

    window.initSignatureCanvas = function (id) {
        // Gọi từ Blazor OnAfterRenderAsync để đảm bảo canvas đã có trong DOM
        setTimeout(() => initCanvas(id), 50);
    };

    // MutationObserver — tự init lại khi Blazor re-render canvas
    const observer = new MutationObserver(() => {
        const c = document.getElementById('sig-canvas');
        if (c && !c._sigInited) initCanvas('sig-canvas');
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();
