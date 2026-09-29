// Projector housekeeping for the player view: fullscreen, a cursor that gets out of the way, and a
// screen wake lock. All three exist for the same reason — the player window is put on a beamer at the
// start of an evening and then left alone for hours, so anything the browser or the OS does on its own
// (a blank screen, a cursor parked over a corridor, browser chrome eating a tenth of the projection)
// is a live interruption at the table rather than a cosmetic nit.
//
// Every capability is feature-detected and every failure is swallowed: the projector must render the
// map even in a browser that refuses all three.
window.dtProjector = (function () {
    // How long the mouse must be still before the cursor is hidden.
    const IDLE_MS = 2500;

    let stage = null;
    let dotnet = null;
    let idleTimer = 0;
    let wakeLock = null;
    let onMove = null;
    let onKey = null;
    let onFullscreenChange = null;
    let onVisibility = null;

    function isFullscreen() {
        return !!document.fullscreenElement;
    }

    function report() {
        if (dotnet) {
            // Fire and forget: a dropped circuit must not throw out of an event handler.
            dotnet.invokeMethodAsync('OnFullscreenChanged', isFullscreen()).catch(function () { });
        }
    }

    function showCursor() {
        if (stage) { stage.classList.remove('cursor-idle'); }
        if (idleTimer) { window.clearTimeout(idleTimer); }
        idleTimer = window.setTimeout(function () {
            if (stage) { stage.classList.add('cursor-idle'); }
        }, IDLE_MS);
    }

    // Keeps the screen from blanking while the player view is on show. The lock is dropped by the
    // browser whenever the tab is hidden, so it is re-requested when the tab comes back.
    async function acquireWakeLock() {
        if (!('wakeLock' in navigator) || wakeLock) { return; }
        try {
            wakeLock = await navigator.wakeLock.request('screen');
            wakeLock.addEventListener('release', function () { wakeLock = null; });
        } catch (e) {
            // Not permitted (an insecure origin, a policy, or a browser without support). The
            // projector still works; the screen may blank on its own.
            wakeLock = null;
        }
    }

    return {
        attach: function (el, net) {
            this.detach();
            stage = el;
            dotnet = net;

            onMove = showCursor;
            onKey = function (e) {
                if (e.ctrlKey || e.altKey || e.metaKey) { return; }
                if (e.key === 'f' || e.key === 'F') {
                    e.preventDefault();
                    window.dtProjector.toggleFullscreen();
                }
            };
            onFullscreenChange = function () {
                showCursor();
                report();
            };
            onVisibility = function () {
                if (document.visibilityState === 'visible') { acquireWakeLock(); }
            };

            window.addEventListener('mousemove', onMove);
            window.addEventListener('keydown', onKey);
            document.addEventListener('fullscreenchange', onFullscreenChange);
            document.addEventListener('visibilitychange', onVisibility);

            showCursor();
            acquireWakeLock();
            report();
        },

        // Requesting fullscreen only works inside a user gesture, which is why this is called from a
        // click or a keypress and never on render.
        toggleFullscreen: function () {
            try {
                if (isFullscreen()) {
                    const left = document.exitFullscreen();
                    if (left && left.catch) { left.catch(function () { }); }
                } else {
                    const target = document.documentElement;
                    if (target.requestFullscreen) {
                        const entered = target.requestFullscreen({ navigationUI: 'hide' });
                        if (entered && entered.catch) { entered.catch(function () { }); }
                    }
                }
            } catch (e) {
                // Blocked by the browser; the view stays windowed.
            }
        },

        detach: function () {
            if (onMove) { window.removeEventListener('mousemove', onMove); onMove = null; }
            if (onKey) { window.removeEventListener('keydown', onKey); onKey = null; }
            if (onFullscreenChange) {
                document.removeEventListener('fullscreenchange', onFullscreenChange);
                onFullscreenChange = null;
            }
            if (onVisibility) {
                document.removeEventListener('visibilitychange', onVisibility);
                onVisibility = null;
            }
            if (idleTimer) { window.clearTimeout(idleTimer); idleTimer = 0; }
            if (wakeLock) {
                const held = wakeLock;
                wakeLock = null;
                if (held.release) { held.release().catch(function () { }); }
            }
            if (stage) { stage.classList.remove('cursor-idle'); }
            stage = null;
            dotnet = null;
        }
    };
})();
