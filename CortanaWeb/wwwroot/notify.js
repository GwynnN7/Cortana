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
        show: (title, body) => {
            if (!supported() || Notification.permission !== 'granted' || !read()) return;
            try { new Notification(title, { body, icon: '/icon-192x192.png', tag: 'cortana-log', renotify: false }); } catch { }
        }
    };
})();
