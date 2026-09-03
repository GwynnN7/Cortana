window.cortanaNotify = (() => {
    const KEY = 'cortana-notify';
    const supported = () => typeof Notification !== 'undefined';

    const read = () => {
        try { return localStorage.getItem(KEY) === 'on'; } catch { return false; }
    };
    const write = value => {
        try { localStorage.setItem(KEY, value ? 'on' : 'off'); } catch { }
    };

    return {
        permission: () => supported() ? Notification.permission : 'unsupported',
        alertsOnly: () => { try { return localStorage.getItem('cortana-notify-alerts') !== 'off'; } catch { return true; } },
        setAlertsOnly: value => { try { localStorage.setItem('cortana-notify-alerts', value ? 'on' : 'off'); } catch { } },
        sources: () => { try { return JSON.parse(localStorage.getItem('cortana-notify-sources') || '[]'); } catch { return []; } },
        setSources: value => { try { localStorage.setItem('cortana-notify-sources', JSON.stringify(value)); } catch { } },
        sticky: () => { try { return localStorage.getItem('cortana-notify-sticky') === 'on'; } catch { return false; } },
        setSticky: value => { try { localStorage.setItem('cortana-notify-sticky', value ? 'on' : 'off'); } catch { } },
        vibrate: () => { try { return localStorage.getItem('cortana-notify-vibrate') !== 'off'; } catch { return true; } },
        setVibrate: value => { try { localStorage.setItem('cortana-notify-vibrate', value ? 'on' : 'off'); } catch { } },
        isEnabled: () => supported() && Notification.permission === 'granted' && read(),
        setEnabled: value => { write(!!value); return read(); },
        toggle: async () => {
            if (!supported()) return false;
            if (read()) { write(false); return false; }

            let state = Notification.permission;
            if (state === 'default') state = await Notification.requestPermission();
            if (state !== 'granted') { write(false); return false; }

            write(true);
            return true;
        },
        show: async (title, body) => {
            if (!supported() || Notification.permission !== 'granted' || !read()) return;

            const options = { body, icon: '/icon-192x192.png', badge: '/badge.png', tag: 'cortana-log' };

            if ('serviceWorker' in navigator) {
                try {
                    const registration = await Promise.race([
                        navigator.serviceWorker.ready,
                        new Promise((_, reject) => setTimeout(() => reject(new Error('no service worker')), 3000))
                    ]);
                    await registration.showNotification(title, options);
                    return;
                } catch { }
            }

            try { new Notification(title, options); } catch { }
        }
    };
})();

window.cortanaPush = {
    supported: () => 'serviceWorker' in navigator && 'PushManager' in window && window.isSecureContext,

    subscribe: async publicKey => {
        if (!window.cortanaPush.supported()) return null;

        let state = Notification.permission;
        if (state === 'default') state = await Notification.requestPermission();
        if (state !== 'granted') return null;

        const registration = await navigator.serviceWorker.ready;
        let subscription = await registration.pushManager.getSubscription();

        if (!subscription) {
            const key = Uint8Array.from(atob(publicKey.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));
            subscription = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
        }

        const raw = subscription.toJSON();
        return { endpoint: raw.endpoint, p256dh: raw.keys.p256dh, auth: raw.keys.auth };
    },

    current: async () => {
        if (!window.cortanaPush.supported()) return null;

        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        return subscription ? subscription.endpoint : null;
    },

    unsubscribe: async () => {
        if (!window.cortanaPush.supported()) return null;

        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        if (!subscription) return null;

        const endpoint = subscription.endpoint;
        await subscription.unsubscribe();
        return endpoint;
    }
};

window.cortanaScrollChat = () => {
    const log = document.getElementById('chat-log');
    if (log) log.scrollTop = log.scrollHeight;
};

// Horizontal swipe over a panel. Vertical scrolling always wins, so a lazy diagonal
// drag scrolls the page rather than changing tab
window.cortanaSwipe = {
    attach: (element, target) => {
        if (!element || element.dataset.swipe === 'on') return;
        element.dataset.swipe = 'on';

        let x = 0, y = 0, tracking = false;

        const start = event => {
            const touch = event.touches ? event.touches[0] : event;
            x = touch.clientX;
            y = touch.clientY;
            tracking = !event.touches || event.touches.length === 1;
        };

        const end = event => {
            if (!tracking) return;
            tracking = false;

            const touch = event.changedTouches ? event.changedTouches[0] : event;
            const dx = touch.clientX - x;
            const dy = touch.clientY - y;

            if (Math.abs(dx) < 60 || Math.abs(dx) < Math.abs(dy) * 1.5) return;

            target.invokeMethodAsync('Swiped', dx < 0 ? 1 : -1);
        };

        element.addEventListener('touchstart', start, { passive: true });
        element.addEventListener('touchend', end, { passive: true });
    }
};
