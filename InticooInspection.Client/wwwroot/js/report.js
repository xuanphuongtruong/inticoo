window.waitImagesAndPrint = function (rootId) {
    return new Promise(function (resolve) {
        var root = rootId ? document.getElementById(rootId) : document;
        var imgs = Array.from((root || document).querySelectorAll('img'));

        if (imgs.length === 0) {
            window.print();
            resolve();
            return;
        }

        var pending = 0;

        function done() {
            pending--;
            if (pending <= 0) {
                setTimeout(function () {
                    window.print();
                    resolve();
                }, 300);
            }
        }

        imgs.forEach(function (img) {
            if (!img.src && !img.getAttribute('src')) return;
            if (img.complete && img.naturalWidth > 0) return;

            pending++;
            var originalSrc = img.src;
            img.addEventListener('load', done, { once: true });
            img.addEventListener('error', done, { once: true });

            if (!img.complete || img.naturalWidth === 0) {
                img.src = '';
                img.src = originalSrc;
            }
        });

        if (pending <= 0) {
            setTimeout(function () {
                window.print();
                resolve();
            }, 300);
        }
    });
};

window.printReportOnly = function () {
    return window.waitImagesAndPrint('rpt-root');
};