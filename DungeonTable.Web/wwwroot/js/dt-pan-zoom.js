// Cursor-anchored wheel zoom + drag pan for an inline SVG inside a bounded box.
// Listeners are attached once to the (stable) box element; the transform is applied to the stable
// .vector-map-pan wrapper (NOT the <svg>), so it survives Blazor replacing the SVG markup on every
// reveal/fog-paint without flashing back to the default view for a frame. getScreenCTM() on the SVG
// still reflects the wrapper's CSS transform, so hit-testing (dt-editor / dt-player-box) is unchanged.
// It also keeps --dt-u on the wrapper, world units per screen pixel, which sizes the rooms' labels, and
// hides a room's name where it is wider than the room on screen (declutter).
window.dtPanZoom = (function () {
    const MIN = 1, MAX = 60;

    function svgOf(box) { return box ? box.querySelector('svg') : null; }

    // The wrapper persists across SVG re-renders; fall back to the SVG for any non-wrapped host.
    function targetOf(box) { return box ? (box.querySelector('.vector-map-pan') || svgOf(box)) : null; }

    function apply(box) {
        const s = box && box.__dtpz;
        const target = targetOf(box);
        if (s && target) {
            target.style.transformOrigin = '0 0';
            target.style.transform = 'translate(' + s.tx + 'px,' + s.ty + 'px) scale(' + s.scale + ')';
            measure(box);
        }
        // Let an overlaid player-view box reposition itself against the new transform.
        if (box) { box.dispatchEvent(new Event('dtviewchange')); }
    }

    // Keeps --dt-u on the wrapper: how many world units one screen pixel is, at the current zoom and
    // the box's current size. The rooms' labels and badges are sized from it (VectorMapCanvas's CSS),
    // so they keep one size on screen at every zoom. Written only when it changes, which a pan never
    // does: a new value restyles the whole map, so it is done once per zoom step or resize, not per
    // pointer move.
    function measure(box) {
        const s = box && box.__dtpz;
        const svg = svgOf(box);
        const target = targetOf(box);
        const m = svg && svg.clientWidth > 0 ? svg.getScreenCTM() : null;
        if (!s || !target || !m || !(m.a > 0)) { return; }
        const u = 1 / m.a;
        if (s.u > 0 && Math.abs(u - s.u) <= s.u * 0.001) { return; }
        s.u = u;
        target.style.setProperty('--dt-u', String(u));
        declutter(box);
    }

    // A room's name shows only where it fits: one wider than its room on screen is hidden (dt-tight) and
    // its badges stay. A label keeps one size on screen, so with a whole level in view the names of a
    // crowded floor would lie across each other, and the badges are what the DM reads at that distance;
    // zoom in and the names come back. Both lengths are in world units, measured after --dt-u has sized
    // the name, so the test is the same at any box size. Run when the zoom changes, and when Blazor puts
    // a new overlay in (watched below), before the browser paints it.
    function declutter(box) {
        const svg = svgOf(box);
        const overlay = svg ? svg.querySelector('g.editor-overlay') : null;
        if (!overlay) { return; }
        box.__dtpz.overlay = overlay;
        overlay.querySelectorAll('g[data-region-id]').forEach(function (region) {
            const name = region.querySelector('text.dt-label:not(.dt-badges)');
            const shape = region.querySelector('polygon');
            if (!name || !shape) { return; }
            const fits = name.getComputedTextLength() <= shape.getBBox().width;
            name.classList.toggle('dt-tight', !fits);
        });
    }

    // Blazor replaces a layer of the map whenever its text changes; a new overlay has every name showing
    // again, so it is decluttered at once. Mutation callbacks run before the next paint, so no frame
    // shows the names that do not fit.
    function watchOverlay(box) {
        const svg = svgOf(box);
        if (!svg || !window.MutationObserver) { return; }
        box.__dtpz.mutations = new MutationObserver(function () {
            const overlay = svg.querySelector('g.editor-overlay');
            if (overlay && overlay !== box.__dtpz.overlay && box.__dtpz.u > 0) { declutter(box); }
        });
        box.__dtpz.mutations.observe(svg, { childList: true });
    }

    function zoomAt(box, px, py, factor) {
        const s = box.__dtpz;
        const next = Math.min(MAX, Math.max(MIN, s.scale * factor));
        const k = next / s.scale;
        s.tx = px - (px - s.tx) * k;
        s.ty = py - (py - s.ty) * k;
        s.scale = next;
        if (s.scale === MIN) { s.tx = 0; s.ty = 0; }
        apply(box);
    }

    function attach(box) {
        if (!box || box.__dtpz) { apply(box); return; }
        const s = { scale: 1, tx: 0, ty: 0, dragging: false, lx: 0, ly: 0 };
        box.__dtpz = s;

        box.addEventListener('wheel', function (e) {
            e.preventDefault();
            const r = box.getBoundingClientRect();
            zoomAt(box, e.clientX - r.left, e.clientY - r.top, e.deltaY < 0 ? 1.12 : 1 / 1.12);
        }, { passive: false });

        box.addEventListener('pointerdown', function (e) {
            // Paused while an editor tool owns the drag (e.g. dragging out a region rectangle).
            if (s.paused) { return; }
            s.dragging = true; s.lx = e.clientX; s.ly = e.clientY;
            box.classList.add('dragging');
            try { box.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
        });
        box.addEventListener('pointermove', function (e) {
            if (!s.dragging) { return; }
            s.tx += e.clientX - s.lx; s.ty += e.clientY - s.ly;
            s.lx = e.clientX; s.ly = e.clientY;
            apply(box);
        });
        function end(e) {
            s.dragging = false; box.classList.remove('dragging');
            try { box.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
        }
        box.addEventListener('pointerup', end);
        box.addEventListener('pointercancel', end);

        // The box changes size with the dock beside it, and a pixel is then another number of world units.
        if (window.ResizeObserver) {
            s.observer = new ResizeObserver(function () { measure(box); });
            s.observer.observe(box);
        }
        watchOverlay(box);

        apply(box);
    }

    function zoom(box, factor) {
        if (!box || !box.__dtpz) { return; }
        const r = box.getBoundingClientRect();
        zoomAt(box, r.width / 2, r.height / 2, factor);
    }

    function reset(box) {
        if (!box || !box.__dtpz) { return; }
        box.__dtpz.scale = 1; box.__dtpz.tx = 0; box.__dtpz.ty = 0;
        apply(box);
    }

    // Pause/resume drag-panning (wheel zoom stays active) so an editor tool can own the drag.
    function setPaused(box, paused) {
        if (box && box.__dtpz) { box.__dtpz.paused = !!paused; }
    }

    // Pan so a world point sits at the centre of the box, then optionally zoom about that centre.
    // Works from the SVG's live screen matrix (which already includes the current pan/zoom), so it
    // needs no knowledge of the viewBox. The box must be visible: a display:none panel has no
    // layout, so getScreenCTM() is null and this is a no-op — call it after the tab is shown.
    function centerOn(box, wx, wy, factor) {
        const s = box && box.__dtpz;
        const svg = svgOf(box);
        const m = svg && svg.getScreenCTM();
        if (!s || !m) { return; }
        const p = svg.createSVGPoint();
        p.x = wx; p.y = wy;
        const sp = p.matrixTransform(m);
        const r = box.getBoundingClientRect();
        if (r.width === 0 || r.height === 0) { return; }
        s.tx += (r.left + r.width / 2) - sp.x;
        s.ty += (r.top + r.height / 2) - sp.y;
        apply(box);
        if (factor > 0 && factor !== 1) { zoomAt(box, r.width / 2, r.height / 2, factor); }
    }

    // Keep the current pan and zoom under a name (the DM screen's dock layouts), and bring one back.
    // The state is the wrapper's transform, which does not depend on the box's size, so a view kept at
    // one width comes back exactly as it was when the box is that width again.
    function saveView(box, key) {
        const s = box && box.__dtpz;
        if (!s) { return; }
        box.__dtpzViews = box.__dtpzViews || {};
        box.__dtpzViews[key] = { scale: s.scale, tx: s.tx, ty: s.ty };
    }

    function restoreView(box, key) {
        const s = box && box.__dtpz;
        const view = box && box.__dtpzViews && box.__dtpzViews[key];
        if (!s || !view) { return false; }
        s.scale = view.scale; s.tx = view.tx; s.ty = view.ty;
        apply(box);
        return true;
    }

    // Zoom and pan so a world rectangle fills the box, centred. Measured at the unzoomed view, from the
    // SVG's own screen matrix, so it needs no knowledge of the viewBox. Never zooms out past the whole
    // map (scale 1). The box must be visible, as for centerOn.
    function fit(box, minX, minY, maxX, maxY) {
        const s = box && box.__dtpz;
        const svg = svgOf(box);
        const target = targetOf(box);
        if (!s || !svg || !target) { return; }
        s.scale = 1; s.tx = 0; s.ty = 0;
        apply(box);
        const m = svg.getScreenCTM();
        const frame = target.getBoundingClientRect();
        if (!m || frame.width === 0 || frame.height === 0) { return; }
        const at = function (x, y) {
            const p = svg.createSVGPoint();
            p.x = x; p.y = y;
            const q = p.matrixTransform(m);
            return { x: q.x - frame.left, y: q.y - frame.top };
        };
        const a = at(minX, minY), b = at(maxX, maxY);
        const width = Math.abs(b.x - a.x), height = Math.abs(b.y - a.y);
        if (width === 0 || height === 0) { return; }
        const k = Math.min(MAX, Math.max(MIN, Math.min(frame.width / width, frame.height / height)));
        if (k === MIN) { apply(box); return; }
        const cx = (a.x + b.x) / 2, cy = (a.y + b.y) / 2;
        s.scale = k;
        s.tx = frame.width / 2 - k * cx;
        s.ty = frame.height / 2 - k * cy;
        apply(box);
    }

    return {
        attach: attach, apply: apply, zoom: zoom, reset: reset, setPaused: setPaused, centerOn: centerOn,
        saveView: saveView, restoreView: restoreView, fit: fit
    };
})();
