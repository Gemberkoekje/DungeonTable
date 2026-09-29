// An aspect-locked, draggable/resizable rectangle overlaid on the DM map that marks the region
// the player projector shows. It lives in map world units and repositions against the DM's own
// pan/zoom via the SVG screen matrix. Drag the body to move; drag a corner to scale (aspect kept).
window.dtPlayerBox = (function () {
    function init(box, dotnet, world, aspect) {
        if (box.__dtbox) { box.__dtbox.set(world, aspect); return; }

        const overlay = document.createElement('div');
        overlay.className = 'dt-player-box';
        const label = document.createElement('div');
        label.className = 'dt-player-box-label';
        label.textContent = 'Player view';
        overlay.appendChild(label);
        ['nw', 'ne', 'sw', 'se'].forEach(function (h) {
            const d = document.createElement('div');
            d.className = 'dt-handle dt-' + h;
            d.dataset.h = h;
            overlay.appendChild(d);
        });
        // Draggable frame edges so the box can be moved by its border while its interior stays
        // click-through (the DM can select the region underneath).
        ['n', 's', 'w', 'e'].forEach(function (edge) {
            const d = document.createElement('div');
            d.className = 'dt-edge dt-edge-' + edge;
            d.dataset.h = 'move';
            overlay.appendChild(d);
        });
        box.appendChild(overlay);

        let W = world;      // { x, y, w, h } in world units
        let A = aspect;     // width / height lock

        function svg() { return box.querySelector('svg'); }
        function ctm() { const s = svg(); return s ? s.getScreenCTM() : null; }
        function toScreen(wx, wy) {
            const s = svg(), m = ctm(), br = box.getBoundingClientRect();
            const p = s.createSVGPoint(); p.x = wx; p.y = wy;
            const sp = p.matrixTransform(m);
            return { x: sp.x - br.left, y: sp.y - br.top };
        }
        function reposition() {
            const s = svg();
            // Bail until the SVG has a real layout, else getScreenCTM is identity and the box
            // would be sized in world units instead of screen pixels.
            if (!s || !s.getScreenCTM() || s.clientWidth === 0) { return; }
            const a = toScreen(W.x, W.y), b = toScreen(W.x + W.w, W.y + W.h);
            overlay.style.left = a.x + 'px';
            overlay.style.top = a.y + 'px';
            overlay.style.width = Math.max(0, b.x - a.x) + 'px';
            overlay.style.height = Math.max(0, b.y - a.y) + 'px';
        }
        function repositionSoon() { requestAnimationFrame(function () { requestAnimationFrame(reposition); }); }
        function push() { dotnet.invokeMethodAsync('OnPlayerBoxChanged', W.x, W.y, W.w, W.h); }

        let mode = null, sx = 0, sy = 0, start = null, grabbed = null;
        overlay.addEventListener('pointerdown', function (e) {
            // Only the interactive frame parts (edges / corners / label) reach here — the interior
            // is pointer-events:none, so a press there falls through to select the region below.
            // When a paint tool (fog brush / draw-rect) owns the drag, let the gesture fall through to
            // the map layer too, so the DM can paint fog under the box frame instead of moving the box.
            const tool = box.__dted && box.__dted.tool;
            if (tool === 'fog' || tool === 'draw-rect') { return; }
            e.stopPropagation(); e.preventDefault();       // don't pan the map underneath
            mode = e.target.dataset.h || 'move';
            grabbed = e.target;
            sx = e.clientX; sy = e.clientY; start = { x: W.x, y: W.y, w: W.w, h: W.h };
            try { grabbed.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
        });
        overlay.addEventListener('pointermove', function (e) {
            if (!mode) { return; }
            const m = ctm(); const sc = m ? m.a : 1;
            const dx = (e.clientX - sx) / sc, dy = (e.clientY - sy) / sc;
            if (mode === 'move') {
                W = { x: start.x + dx, y: start.y + dy, w: start.w, h: start.h };
            } else {
                let w = start.w;
                if (mode.indexOf('e') >= 0) { w = Math.max(20, start.w + dx); }
                if (mode.indexOf('w') >= 0) { w = Math.max(20, start.w - dx); }
                const h = w / A;
                let x = start.x, y = start.y;
                if (mode.indexOf('w') >= 0) { x = start.x + (start.w - w); }
                if (mode.indexOf('n') >= 0) { y = start.y + (start.h - h); }
                W = { x: x, y: y, w: w, h: h };
            }
            reposition();
        });
        function end(e) {
            if (!mode) { return; }
            mode = null; push();
            try { (grabbed || overlay).releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
            grabbed = null;
        }
        overlay.addEventListener('pointerup', end);
        overlay.addEventListener('pointercancel', end);
        box.addEventListener('dtviewchange', reposition);

        // Reposition once the SVG actually has a layout, and whenever the box resizes.
        if (window.ResizeObserver) {
            const ro = new ResizeObserver(reposition);
            ro.observe(box);
            const s0 = svg();
            if (s0) { ro.observe(s0); }
        }

        box.__dtbox = {
            set: function (nw, na) {
                if (nw) { W = nw; }
                if (na && na > 0) { A = na; }
                repositionSoon();
            }
        };
        repositionSoon();
    }

    return {
        init: init,
        update: function (box, x, y, w, h, aspect) {
            if (box && box.__dtbox) { box.__dtbox.set({ x: x, y: y, w: w, h: h }, aspect); }
        }
    };
})();

// Reports an element's live aspect ratio (width / height) to .NET on connect and on resize.
// unwatch removes the window listener when the component is disposed so it does not leak or fire
// against a disposed .NET reference.
window.dtAspect = {
    watch: function (el, dotnet) {
        if (!el || el.__dtAspectReport) { return; }
        function report() {
            const b = el.getBoundingClientRect();
            if (b.height > 0) { dotnet.invokeMethodAsync('OnPlayerAspect', b.width / b.height); }
        }
        el.__dtAspectReport = report;
        report();
        window.addEventListener('resize', report);
    },
    unwatch: function (el) {
        if (el && el.__dtAspectReport) {
            window.removeEventListener('resize', el.__dtAspectReport);
            delete el.__dtAspectReport;
        }
    }
};
