// The dock beside the DM's map: drag the divider to change its width, double-click the divider for
// the default width, or focus it and use the arrow keys. "\" (or the dock's swap button) swaps the dock
// between narrow and wide.
//
// Each layout keeps its own width: the reading tabs (Room, Campaign, Art) share one, and the Battle tab
// has its own, wide by default, so the fight's order and a creature's detail pane fit side by side
// and the map becomes a bird's-eye strip. The shell carries the layout as a class (layout-battle),
// which DmView renders; this file follows that class and puts back the layout's width.
//
// A width is kept per browser in local storage, as pixels or as "narrow" or "wide" (app.css's two
// defaults, --dock-narrow and --dock-wide). Local storage can be missing or refuse a write (a private
// window, blocked site data), so every read and write is guarded and the layout's default stands in.
//
// The width is set as --dock-width on the shell element. Blazor renders that element's class but no
// style attribute, so it never resets what is set here.
window.dtDock = (function () {
    const KEY = 'dungeontable.dockWidth.';
    const MIN = 360;      // px: narrower than this, the Room tab's header and the fight's rows wrap badly
    const MAP_MIN = 300;  // px the map always keeps, enough for a bird's-eye view
    const STEP = 32;      // px an arrow key moves the divider
    let current = null;
    let shellOf = null;

    function layoutOf(shell) {
        return shell.classList.contains('layout-battle') ? 'battle' : 'reading';
    }

    function load(layout) {
        try {
            const value = window.localStorage.getItem(KEY + layout) || '';
            if (value === 'narrow' || value === 'wide') { return value; }
            const px = parseInt(value, 10);
            return isFinite(px) && px > 0 ? px : '';
        } catch (e) {
            return '';
        }
    }

    function save(layout, width) {
        try {
            if (width) {
                window.localStorage.setItem(KEY + layout, String(width));
            } else {
                window.localStorage.removeItem(KEY + layout);
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

    // A width is pixels, a keyword for one of the two defaults, or nothing for the layout's default.
    function apply(shell, width) {
        if (width === 'narrow' || width === 'wide') {
            shell.style.setProperty('--dock-width', 'var(--dock-' + width + ')');
        } else if (width > 0) {
            shell.style.setProperty('--dock-width', clamp(shell, width) + 'px');
        } else {
            shell.style.removeProperty('--dock-width');
        }
    }

    function widthOf(shell) {
        const dock = shell.querySelector('.dm-dock');
        return dock ? Math.round(dock.getBoundingClientRect().width) : 0;
    }

    // Narrow to wide and back, for the layout on show: wide when the dock is less than half the
    // screen's width, narrow otherwise.
    function flip(shell) {
        shell = shell || shellOf;
        if (!shell) { return; }
        const body = shell.querySelector('.dm-body');
        const total = body ? body.getBoundingClientRect().width : window.innerWidth;
        const width = widthOf(shell) * 2 < total ? 'wide' : 'narrow';
        apply(shell, width);
        save(layoutOf(shell), width);
    }

    function attach(shell) {
        detach();
        const divider = shell && shell.querySelector('.dm-divider');
        if (!divider) { return; }
        shellOf = shell;

        let layout = layoutOf(shell);
        apply(shell, load(layout));
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
            save(layout, widthOf(shell));
        };
        const reset = function () {
            apply(shell, '');
            save(layout, '');
        };
        const key = function (e) {
            if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') { return; }
            e.preventDefault();
            apply(shell, widthOf(shell) + (e.key === 'ArrowLeft' ? STEP : -STEP));
            save(layout, widthOf(shell));
        };
        const swap = function (e) {
            if (e.target.closest && e.target.closest('.dock-swap-btn')) { flip(shell); }
        };
        // A width kept on a big screen must still leave the map its share of a smaller window.
        const resize = function () { apply(shell, load(layout)); };
        // The dock went to another layout: put back that layout's width before the browser paints.
        const observer = new MutationObserver(function () {
            const next = layoutOf(shell);
            if (next !== layout) {
                layout = next;
                apply(shell, load(layout));
            }
        });

        divider.addEventListener('pointerdown', down);
        divider.addEventListener('pointermove', move);
        divider.addEventListener('pointerup', up);
        divider.addEventListener('pointercancel', up);
        divider.addEventListener('dblclick', reset);
        divider.addEventListener('keydown', key);
        shell.addEventListener('click', swap);
        window.addEventListener('resize', resize);
        observer.observe(shell, { attributes: true, attributeFilter: ['class'] });

        current = function () {
            divider.removeEventListener('pointerdown', down);
            divider.removeEventListener('pointermove', move);
            divider.removeEventListener('pointerup', up);
            divider.removeEventListener('pointercancel', up);
            divider.removeEventListener('dblclick', reset);
            divider.removeEventListener('keydown', key);
            shell.removeEventListener('click', swap);
            window.removeEventListener('resize', resize);
            observer.disconnect();
            shellOf = null;
        };
    }

    function detach() {
        if (current) {
            current();
            current = null;
        }
    }

    return { attach: attach, detach: detach, flip: flip };
})();
