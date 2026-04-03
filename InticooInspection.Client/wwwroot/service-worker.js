// File này dùng để quản lý cache và cập nhật cho InticooInspection
self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('fetch', event => {
    // Để ứng dụng luôn tải bản mới nhất từ mạng khi có thể
    event.respondWith(fetch(event.request).catch(() => caches.match(event.request)));
});