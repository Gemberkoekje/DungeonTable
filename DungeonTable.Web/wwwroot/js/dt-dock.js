// The dock beside the DM's map: drag the divider to change its width, double-click the divider for
// the default width, or focus it and use the arrow keys. The width is kept per browser in local
// storage. That can be missing or refuse a write (a private window, blocked site data), so every read
// and write is guarded and the default width from app.css stands in.
//
// The width is set as --dock-width on the shell element. Blazor renders that element's class but no
// style attribute, so it never resets what is set here.
window.dtDock = (function () {
    const KEY = 'dungeontable.dockWidth';
    const MIN = 360;      // px: narrower than this, the Room tab's header and the fight's rows wrap badly
    const MAP_MIN = 420;  // px the map always keeps
    const STEP = 32;      // px an arrow key moves the divider
    let current = null;

    function load() {
        try {
            const value = parseInt(window.localStorage.getItem(KEY), 10);
            return isFinite(value) && value > 0 ? value : 0;
        } catch (e) {
            return 0;
        }
    }

    function save(px) {
        try {
            if (px > 0) {
                window.localStorage.setItem(KEY, String(px));
            } else {
                window.localStorage.removeItem(KEY);
            }
        } catch (e) {
            // Nowhere to keep it: the width lasts until the page is reloaded.
        }
    }

    function clamp(shell, px) {
        const body = shell.querySelector('.dm-body');
        const total = body ? body.getBoundingClientRect().width : window.innerWidth;
        return Math.round(Math.max(MIN, Math.min(px, total - MAP_MIN)));
    }

    function apply(shell, px) {
        if (px > 0) {
            shell.style.setProperty('--dock-width', clamp(shell, px) + 'px');
        } else {
            shell.style.removeProperty('--dock-width');
        }
    }

    function widthOf(shell) {
        const dock = shell.querySelector('.dm-dock');
        return dock ? Math.round(dock.getBoundingClientRect().width) : 0;
    }

    function attach(shell) {
        detach();
        const divider = shell && shell.querySelector('.dm-divider');
        if (!divider) { return; }

        apply(shell, load());
        let drag = null;

        const down = function (e) {
            if (e.button !== 0) { return; }
            drag = { x: e.clientX, width: widthOf(shell) };
            shell.classList.add('resizing');
            try { divider.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
            e.preventDefault();
        };
        const move = function (e) {
            if (!drag) { return; }
            apply(shell, drag.width + (drag.x - e.clientX));
        };
        const up = function (e) {
            if (!drag) { return; }
            drag = null;
            shell.classList.remove('resizing');
            try { divider.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
            save(widthOf(shell));
        };
        const reset = function () {
            apply(shell, 0);
            save(0);
        };
        const key = function (e) {
            if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') { return; }
            e.preventDefault();
            apply(shell, widthOf(shell) + (e.key === 'ArrowLeft' ? STEP : -STEP));
            save(widthOf(shell));
        };
        // A width kept on a big screen must still leave the map its share of a smaller window.
        const resize = function () { apply(shell, load()); };

        divider.addEventListener('pointerdown', down);
        divider.addEventListener('pointermove', move);
        divider.addEventListener('pointerup', up);
        divider.addEventListener('pointercancel', up);
        divider.addEventListener('dblclick', reset);
        divider.addEventListener('keydown', key);
        window.addEventListener('resize', resize);

        current = function () {
            divider.removeEventListener('pointerdown', down);
            divider.removeEventListener('pointermove', move);
            divider.removeEventListener('pointerup', up);
            divider.removeEventListener('pointercancel', up);
            divider.removeEventListener('dblclick', reset);
            divider.removeEventListener('keydown', key);
            window.removeEventListener('resize', resize);
        };
    }

    function detach() {
        if (current) {
            current();
            current = null;
        }
    }

    return { attach: attach, detach: detach };
})();
