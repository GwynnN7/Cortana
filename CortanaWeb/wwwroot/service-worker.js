// Caches the shell for instant startup and serves an offline page when the Pi is down

const CACHE = 'cortana-shell-v11';
const SHELL = ['/', '/app.css', '/favicon.png', '/icon-192x192.png', '/icon-144x144.png', '/icon-72x72.png', '/icon-512x512.png', '/manifest.webmanifest', '/offline.html',
    '/badge.png'];

self.addEventListener('install', event => {
    event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(key => key !== CACHE).map(key => caches.delete(key))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    if (event.request.method !== 'GET') return;
    if (url.origin !== self.location.origin) return;
    if (url.pathname.startsWith('/_blazor') || url.pathname.startsWith('/auth') ||
        url.pathname.startsWith('/media') || url.pathname.startsWith('/quick')) return;

    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request)
                .then(response => response.status >= 500 ? caches.match('/offline.html') : response)
                .catch(() => caches.match('/offline.html'))
        );
        return;
    }

    // Static assets: cache first
    event.respondWith(
        caches.match(event.request).then(hit => hit || fetch(event.request).then(response => {
            if (response.ok) {
                const copy = response.clone();
                caches.open(CACHE).then(cache => cache.put(event.request, copy));
            }
            return response;
        }))
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();

    const target = (event.notification.data && event.notification.data.url) || '/logs';

    event.waitUntil(clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
        for (const client of list) {
            if (!('focus' in client)) continue;

            const sent = 'navigate' in client
                ? client.navigate(target).catch(() => client)
                : Promise.resolve(client);

            return sent.then(window => (window || client).focus());
        }
        return clients.openWindow(target);
    }));
});

self.addEventListener('push', event => {
    let title = 'Cortana';
    let body = '';
    let tag = 'cortana-push';
    let silent = false;
    let ongoing = false;
    let vibrate = [];
    let timestamp;
    let url = '/logs';

    if (event.data) {
        try {
            const payload = event.data.json();
            title = payload.title ?? title;
            body = payload.body || '';
            tag = payload.tag || tag;
            silent = !!payload.silent;
            ongoing = !!payload.ongoing;
            timestamp = Number.isFinite(payload.timestamp) ? payload.timestamp : undefined;
            vibrate = Array.isArray(payload.vibrate) ? payload.vibrate : [];
            url = payload.url || url;
        } catch {
            body = event.data.text();
        }
    }

    const options = {
        body,
        tag,
        silent,
        renotify: !silent,
        requireInteraction: ongoing,
        icon: '/badge.png',
        badge: '/badge.png',
        data: { url }
    };

    if (timestamp !== undefined) options.timestamp = timestamp;

    if (!silent && vibrate.length > 0)
        options.vibrate = vibrate;

    event.waitUntil(
        self.registration.showNotification(title, options).catch(error => {
            console.error('[Push] showNotification failed:', error);
        })
    );
});