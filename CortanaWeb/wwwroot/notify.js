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

            const options = { body, icon: '/icon-192x192.png', badge: '/icon-96x96.png', tag: 'cortana-log' };

            if ('serviceWorker' in navigator) {
                try {
                    const registration = await navigator.serviceWorker.ready;
                    await registration.showNotification(title, options);
                    return;
                } catch { }
            }

            try { new Notification(title, options); } catch { }
        }
    };
})();

window.cortanaScrollChat = () => {
    const log = document.getElementById('chat-log');
    if (log) log.scrollTop = log.scrollHeight;
};

window.cortanaChatId = {
    load: () => { try { return localStorage.getItem('cortana-chat'); } catch { return null; } },
    save: id => { try { localStorage.setItem('cortana-chat', id); } catch { } }
};
