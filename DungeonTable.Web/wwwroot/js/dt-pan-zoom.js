// Cursor-anchored wheel zoom + drag pan for an inline SVG inside a bounded box.
// Listeners are attached once to the (stable) box element; the transform is applied to the stable
// .vector-map-pan wrapper (NOT the <svg>), so it survives Blazor replacing the SVG markup on every
// reveal/fog-paint without flashing back to the default view for a frame. getScreenCTM() on the SVG
// still reflects the wrapper's CSS transform, so hit-testing (dt-editor / dt-player-box) is unchanged.
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
        }
        // Let an overlaid player-view box reposition itself against the new transform.
        if (box) { box.dispatchEvent(new Event('dtviewchange')); }
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

    return { attach: attach, apply: apply, zoom: zoom, reset: reset, setPaused: setPaused, centerOn: centerOn };
})();
