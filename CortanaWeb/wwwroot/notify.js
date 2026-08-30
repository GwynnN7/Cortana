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

            const options = { body, icon: '/icon-192x192.png', badge: '/badge-96x96.png', tag: 'cortana-log' };

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

window.cortanaChatId = {
    load: () => { try { return localStorage.getItem('cortana-chat'); } catch { return null; } },
    save: id => { try { localStorage.setItem('cortana-chat', id); } catch { } }
};