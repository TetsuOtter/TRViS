(() => {
    const state = {
        watchId: null,
        dotNetRef: null,
        wakeLockSentinel: null,
    };

    const geolocation = {
        start(dotNetRef) {
            if (!('geolocation' in navigator)) {
                dotNetRef.invokeMethodAsync('OnError', 'Geolocation API is not available.');
                return;
            }
            geolocation.stop();
            state.dotNetRef = dotNetRef;
            state.watchId = navigator.geolocation.watchPosition(
                (pos) => {
                    const { longitude, latitude, accuracy } = pos.coords;
                    dotNetRef.invokeMethodAsync('OnPosition', longitude, latitude, accuracy ?? null);
                },
                (err) => {
                    dotNetRef.invokeMethodAsync('OnError', err.message || 'GPS error');
                },
                { enableHighAccuracy: true, maximumAge: 1000, timeout: 15000 }
            );
        },
        stop() {
            if (state.watchId !== null) {
                navigator.geolocation.clearWatch(state.watchId);
                state.watchId = null;
            }
            state.dotNetRef = null;
        },
    };

    const wakeLock = {
        async request() {
            if (!('wakeLock' in navigator)) return;
            try {
                if (state.wakeLockSentinel) return;
                state.wakeLockSentinel = await navigator.wakeLock.request('screen');
                state.wakeLockSentinel.addEventListener('release', () => {
                    state.wakeLockSentinel = null;
                });
            } catch {
                state.wakeLockSentinel = null;
            }
        },
        async release() {
            if (!state.wakeLockSentinel) return;
            try { await state.wakeLockSentinel.release(); } catch { /* noop */ }
            state.wakeLockSentinel = null;
        },
    };

    window.trvisWeb = { geolocation, wakeLock };
})();
