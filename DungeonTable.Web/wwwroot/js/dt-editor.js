// Room Editor input layer. Sits on the same bounded box as dtPanZoom and translates pointer
// gestures on the map into world-unit coordinates that it reports to .NET. The .NET side owns the
// tool/mode; this module stays "dumb":
//   - tool 'select'  : a click reports the world point plus whatever region/feature/object group
//                      was under it (OnCanvasClick). Panning stays active (drag = pan, not click).
//   - tool 'marker'  : a click reports the world point to drop a marker (OnCanvasClick).
//   - tool 'draw-rect': a drag draws a rectangle; on release its normalized world rect is reported
//                      (OnRegionDragged). Drag-panning is paused so the drag draws instead of pans.
//   - tool 'draw-poly': a click reports the world point as the next corner of a region outline
//                      (OnCanvasClick); .NET draws the outline so far. Panning stays active, so a
//                      large cave can be panned across mid-outline. The segment from the last corner
//                      to the pointer is drawn here, since it has to follow the pointer.
//   - tool 'fog'     : press-and-drag paints reveal cells; each sampled world point is reported
//                      (OnFogPaint) with an erase flag (secondary button / alt / shift). Panning is
//                      paused so the drag paints instead of pans.
//   - tool 'measure' : a drag rubber-bands a line and shows the distance live; on release the two
//                      world points are reported (OnMeasured). Panning is paused so the drag
//                      measures instead of pans.
//
// The measure preview computes its own figure rather than waiting for a round trip, so the number
// tracks the pointer at frame rate. It must therefore agree with the server exactly, which is why
// setTool is handed the grid ORIGIN as well as the cell size: without the origin the preview could
// only count cells from the drag's own start, and would disagree with the committed figure by a
// square whenever the drag began mid-cell.
window.dtEditor = (function () {
    const SVG_NS = 'http://www.w3.org/2000/svg';
    const CLICK_SLOP = 5; // px of movement below which a press counts as a click, not a drag.
    const FEET_PER_CELL = 5;

    function svgOf(box) { return box ? box.querySelector('svg') : null; }

    // Screen (clientX/clientY) -> map world units, honouring the live pan/zoom via the SVG's
    // on-screen matrix. Returns null until the SVG has a real layout.
    function worldOf(box, clientX, clientY) {
        const svg = svgOf(box);
        const m = svg && svg.getScreenCTM();
        if (!m) { return null; }
        const p = svg.createSVGPoint();
        p.x = clientX; p.y = clientY;
        const w = p.matrixTransform(m.inverse());
        return { x: w.x, y: w.y };
    }

    // Classify what was clicked using the same data-* hooks the renderer emits. Feature and object
    // glyphs (and the editor's transparent object hit-targets) sit on top of regions, so they win.
    function hitAt(clientX, clientY) {
        const el = document.elementFromPoint(clientX, clientY);
        if (!el) { return { kind: '', id: '' }; }
        const feature = el.closest('[data-feature-id]');
        if (feature) { return { kind: 'feature', id: feature.getAttribute('data-feature-id') }; }
        const object = el.closest('[data-object-id]');
        if (object) {
            return { kind: object.getAttribute('data-object-kind') || 'object', id: object.getAttribute('data-object-id') };
        }
        const region = el.closest('[data-region-id]');
        if (region) { return { kind: 'region', id: region.getAttribute('data-region-id') }; }
        return { kind: '', id: '' };
    }

    // True for events that start on the pan/zoom tool buttons; those must not be treated as map
    // gestures (otherwise a "+"/"-"/Reset click adds a stray vertex, marker, or deselect).
    function onTools(e) { return !!(e.target && e.target.closest && e.target.closest('.vector-map-tools')); }

    function previewRect(box) {
        const svg = svgOf(box);
        if (!svg) { return null; }
        let rect = svg.querySelector('rect.dt-draft-rect');
        if (!rect) {
            rect = document.createElementNS(SVG_NS, 'rect');
            rect.setAttribute('class', 'dt-draft-rect');
            rect.setAttribute('fill', 'rgba(230,126,34,0.15)');
            rect.setAttribute('stroke', '#e67e22');
            rect.setAttribute('pointer-events', 'none');
            svg.appendChild(rect);
        }
        return rect;
    }

    function clearPreview(box) {
        const svg = svgOf(box);
        const rect = svg && svg.querySelector('rect.dt-draft-rect');
        if (rect) { rect.remove(); }
    }

    function drawPreview(box, s, cx, cy) {
        const w = worldOf(box, cx, cy);
        const rect = previewRect(box);
        if (!w || !rect || !s.startWorld) { return; }
        const x = Math.min(s.startWorld.x, w.x), y = Math.min(s.startWorld.y, w.y);
        const ww = Math.abs(w.x - s.startWorld.x), hh = Math.abs(w.y - s.startWorld.y);
        rect.setAttribute('x', x);
        rect.setAttribute('y', y);
        rect.setAttribute('width', ww);
        rect.setAttribute('height', hh);
        rect.setAttribute('stroke-width', 0.16 * s.unit);
        rect.setAttribute('stroke-dasharray', (0.5 * s.unit) + ' ' + (0.35 * s.unit));
    }

    function endDraw(box, s) {
        s.drawing = false;
        clearPreview(box);
    }

    // ---- Polygon outline ---------------------------------------------------------------------

    // The dashed segment from the outline's last corner to the pointer. .NET draws the corners
    // placed so far (the g.dt-draft-poly group) and writes the last one onto that group, with
    // whether the next corner snaps; the segment ends where a click would put that corner. The SVG
    // is re-rendered after every click, which wipes this element, so it is drawn again from the
    // last pointer position as soon as the tool is re-applied (setTool).
    function drawRubber(box, s, cx, cy) {
        const svg = svgOf(box);
        const draft = svg && svg.querySelector('g.dt-draft-poly');
        const w = draft ? worldOf(box, cx, cy) : null;
        const lx = draft ? parseFloat(draft.getAttribute('data-last-x')) : NaN;
        const ly = draft ? parseFloat(draft.getAttribute('data-last-y')) : NaN;
        if (!w || !Number.isFinite(lx) || !Number.isFinite(ly)) { clearRubber(box); return; }

        let x = w.x, y = w.y;
        if (draft.getAttribute('data-snap') === 'true' && s.unit > 0) {
            x = Math.round((x - s.originX) / s.unit) * s.unit + s.originX;
            y = Math.round((y - s.originY) / s.unit) * s.unit + s.originY;
        }

        let line = svg.querySelector('line.dt-rubber');
        if (!line) {
            line = document.createElementNS(SVG_NS, 'line');
            line.setAttribute('class', 'dt-rubber');
            line.setAttribute('stroke', '#e67e22');
            line.setAttribute('stroke-linecap', 'round');
            line.setAttribute('pointer-events', 'none');
            svg.appendChild(line);
        }
        line.setAttribute('x1', lx); line.setAttribute('y1', ly);
        line.setAttribute('x2', x); line.setAttribute('y2', y);
        line.setAttribute('stroke-width', 0.12 * s.unit);
        line.setAttribute('stroke-dasharray', (0.4 * s.unit) + ' ' + (0.3 * s.unit));
    }

    function clearRubber(box) {
        const svg = svgOf(box);
        const line = svg && svg.querySelector('line.dt-rubber');
        if (line) { line.remove(); }
    }

    // ---- Measuring ---------------------------------------------------------------------------

    function measureGroup(box) {
        const svg = svgOf(box);
        if (!svg) { return null; }
        let g = svg.querySelector('g.dt-measure');
        if (!g) {
            g = document.createElementNS(SVG_NS, 'g');
            g.setAttribute('class', 'dt-measure');
            g.setAttribute('pointer-events', 'none');

            const under = document.createElementNS(SVG_NS, 'line');
            under.setAttribute('stroke', '#1b1b1b');
            under.setAttribute('stroke-opacity', '0.55');
            under.setAttribute('stroke-linecap', 'round');

            const line = document.createElementNS(SVG_NS, 'line');
            line.setAttribute('stroke', '#f5c542');
            line.setAttribute('stroke-linecap', 'round');

            const label = document.createElementNS(SVG_NS, 'text');
            label.setAttribute('text-anchor', 'middle');
            label.setAttribute('dominant-baseline', 'central');
            label.setAttribute('font-family', 'sans-serif');
            label.setAttribute('font-weight', '700');
            label.setAttribute('paint-order', 'stroke');
            label.setAttribute('stroke', '#f4f1ea');
            label.setAttribute('fill', '#1b1b1b');

            g.appendChild(under);
            g.appendChild(line);
            g.appendChild(label);
            svg.appendChild(g);
        }
        return g;
    }

    function clearMeasure(box) {
        const svg = svgOf(box);
        const g = svg && svg.querySelector('g.dt-measure');
        if (g) { g.remove(); }
    }

    // The same rule the server applies (see MapMeasure): Chebyshev between the CELLS the two points
    // fall in, at 5 ft a square.
    function squaresBetween(s, a, b) {
        if (!(s.unit > 0)) { return -1; }
        const c0 = Math.floor((a.x - s.originX) / s.unit), r0 = Math.floor((a.y - s.originY) / s.unit);
        const c1 = Math.floor((b.x - s.originX) / s.unit), r1 = Math.floor((b.y - s.originY) / s.unit);
        return Math.max(Math.abs(c1 - c0), Math.abs(r1 - r0));
    }

    function drawMeasure(box, s, cx, cy) {
        const w = worldOf(box, cx, cy);
        const g = measureGroup(box);
        if (!w || !g || !s.startWorld) { return; }
        const a = s.startWorld;
        const [under, line, label] = g.childNodes;
        for (const el of [under, line]) {
            el.setAttribute('x1', a.x); el.setAttribute('y1', a.y);
            el.setAttribute('x2', w.x); el.setAttribute('y2', w.y);
        }
        under.setAttribute('stroke-width', 0.22 * s.unit);
        line.setAttribute('stroke-width', 0.1 * s.unit);

        const squares = squaresBetween(s, a, w);
        const straight = Math.round(Math.hypot(w.x - a.x, w.y - a.y) / s.unit * FEET_PER_CELL);
        label.textContent = squares >= 0 ? (squares * FEET_PER_CELL) + ' ft' : '~' + straight + ' ft';
        label.setAttribute('x', (a.x + w.x) / 2);
        label.setAttribute('y', ((a.y + w.y) / 2) - 0.55 * s.unit);
        label.setAttribute('font-size', 0.9 * s.unit);
        label.setAttribute('stroke-width', 0.9 * s.unit * 0.28);
    }

    function attach(box, dotnet) {
        if (!box) { return; }
        if (box.__dted) { box.__dted.dotnet = dotnet; return; }

        const s = { dotnet: dotnet, tool: 'select', unit: 1, originX: 0, originY: 0, downX: 0, downY: 0, moved: false, tracking: false, drawing: false, painting: false, measuring: false, erase: false, startWorld: null, paintX: 0, paintY: 0, pointer: null };
        box.__dted = s;

        function reportWorld(wx, wy) { s.dotnet.invokeMethodAsync('OnFogPaint', wx, wy, s.erase); }
        function paintAt(cx, cy) {
            const w = worldOf(box, cx, cy);
            if (w) { reportWorld(w.x, w.y); }
        }
        // Paint every cell along the drag segment, not just the sampled endpoints: pointermove is
        // coalesced to ~1 per frame, so a fast drag while zoomed out would otherwise skip cells and
        // leave a dashed reveal. Step in world units (half a cell) so coverage is gap-free at any zoom.
        function paintSegment(cx0, cy0, cx1, cy1) {
            const a = worldOf(box, cx0, cy0), b = worldOf(box, cx1, cy1);
            if (!a || !b) { paintAt(cx1, cy1); return; }
            const dx = b.x - a.x, dy = b.y - a.y;
            const step = (s.unit > 0 ? s.unit : 1) * 0.5;
            const n = Math.max(1, Math.ceil(Math.hypot(dx, dy) / step));
            for (let i = 1; i <= n; i++) { const t = i / n; reportWorld(a.x + dx * t, a.y + dy * t); }
        }

        box.addEventListener('pointerdown', function (e) {
            if (onTools(e)) { return; }
            // Only the primary button drives select/marker/draw gestures — otherwise a right- or
            // middle-click (or its context menu) would register as a map click and toggle a reveal.
            // The fog tool is the exception: it uses the secondary button (and Alt/Shift) to erase.
            if (e.button !== 0 && s.tool !== 'fog') { return; }
            s.tracking = true; s.moved = false; s.downX = e.clientX; s.downY = e.clientY;
            if (s.tool === 'draw-rect') {
                s.drawing = true;
                s.startWorld = worldOf(box, e.clientX, e.clientY);
                try { box.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
            } else if (s.tool === 'fog') {
                s.painting = true;
                s.erase = (e.button === 2 || e.altKey || e.shiftKey);
                s.paintX = e.clientX; s.paintY = e.clientY;
                try { box.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
                paintAt(e.clientX, e.clientY);
            } else if (s.tool === 'measure') {
                s.measuring = true;
                s.startWorld = worldOf(box, e.clientX, e.clientY);
                try { box.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
                drawMeasure(box, s, e.clientX, e.clientY);
            }
        });
        box.addEventListener('pointermove', function (e) {
            if (s.tool === 'draw-poly' && !onTools(e)) {
                s.pointer = { x: e.clientX, y: e.clientY };
                drawRubber(box, s, e.clientX, e.clientY);
            }
            if (!s.tracking) { return; }
            if (Math.abs(e.clientX - s.downX) > CLICK_SLOP || Math.abs(e.clientY - s.downY) > CLICK_SLOP) {
                s.moved = true;
            }
            if (s.drawing) { drawPreview(box, s, e.clientX, e.clientY); }
            if (s.measuring) { drawMeasure(box, s, e.clientX, e.clientY); }
            if (s.painting) { paintSegment(s.paintX, s.paintY, e.clientX, e.clientY); s.paintX = e.clientX; s.paintY = e.clientY; }
        });
        box.addEventListener('contextmenu', function (e) { if (s.tool === 'fog') { e.preventDefault(); } });
        box.addEventListener('pointerup', function (e) {
            if (onTools(e)) { s.tracking = false; return; }
            const tracking = s.tracking;
            s.tracking = false;

            if (s.painting) { s.painting = false; return; }

            if (s.measuring) {
                const w = worldOf(box, e.clientX, e.clientY);
                const start = s.startWorld;
                s.measuring = false;
                // The live preview is handed over to .NET, which re-draws the committed line into
                // the SVG itself — leaving this one behind would double it up.
                clearMeasure(box);
                if (w && start) {
                    s.dotnet.invokeMethodAsync('OnMeasured', start.x, start.y, w.x, w.y);
                }
                return;
            }

            if (s.drawing) {
                const w = worldOf(box, e.clientX, e.clientY);
                const start = s.startWorld;
                endDraw(box, s);
                if (!w || !start) { return; }
                const x = Math.min(start.x, w.x), y = Math.min(start.y, w.y);
                const ww = Math.abs(w.x - start.x), hh = Math.abs(w.y - start.y);
                // Ignore an accidental tiny drag (a mis-click in draw mode).
                if (ww >= 0.4 * s.unit && hh >= 0.4 * s.unit) {
                    s.dotnet.invokeMethodAsync('OnRegionDragged', x, y, ww, hh);
                }
                return;
            }

            // select / marker tools: a stationary press is a click.
            if (!tracking || s.moved) { return; }
            const wp = worldOf(box, e.clientX, e.clientY);
            if (!wp) { return; }
            const hit = hitAt(e.clientX, e.clientY);
            s.dotnet.invokeMethodAsync('OnCanvasClick', wp.x, wp.y, hit.kind, hit.id || '');
        });
        function cancel() {
            if (s.drawing) { endDraw(box, s); }
            if (s.measuring) { s.measuring = false; clearMeasure(box); }
            s.painting = false;
            s.tracking = false;
            s.pointer = null;
            clearRubber(box);
        }
        box.addEventListener('pointercancel', cancel);
        box.addEventListener('pointerleave', cancel);
    }

    return {
        attach: attach,
        // Set the active tool, its world-unit cell size and the grid's world-unit origin.
        // 'draw-rect', 'fog' and 'measure' each pause drag-panning so the drag does its own thing
        // instead of panning; other tools leave panning active.
        setTool: function (box, tool, unit, originX, originY) {
            if (!box || !box.__dted) { return; }
            box.__dted.tool = tool || 'select';
            box.__dted.unit = unit > 0 ? unit : 1;
            box.__dted.originX = Number.isFinite(originX) ? originX : 0;
            box.__dted.originY = Number.isFinite(originY) ? originY : 0;
            // Switching tools mid-gesture must not leave a half-finished drag armed: reset the
            // press-tracking flags so the next pointerup can't be misread as a stale click/paint.
            box.__dted.tracking = false;
            box.__dted.moved = false;
            if (window.dtPanZoom && window.dtPanZoom.setPaused) {
                window.dtPanZoom.setPaused(box, tool === 'draw-rect' || tool === 'fog' || tool === 'measure');
            }
            if (tool !== 'draw-rect') { endDraw(box, box.__dted); }
            if (tool !== 'fog') { box.__dted.painting = false; }
            if (tool !== 'measure') { box.__dted.measuring = false; clearMeasure(box); }
            // This runs after every render, and a render replaces the SVG the segment lived in.
            const pointer = box.__dted.pointer;
            if (tool === 'draw-poly' && pointer) {
                drawRubber(box, box.__dted, pointer.x, pointer.y);
            } else {
                clearRubber(box);
            }
        },
        detach: function (box) {
            if (box) { clearPreview(box); clearMeasure(box); clearRubber(box); delete box.__dted; }
            if (box && window.dtPanZoom && window.dtPanZoom.setPaused) { window.dtPanZoom.setPaused(box, false); }
        }
    };
})();

// While the Room Editor has unsaved changes (its toolbar says data-unsaved="true"), a click on a link
// that would take the page elsewhere asks first. The app's router is static, so the layout's links
// are followed by blazor.web.js's enhanced navigation, which Blazor's NavigationLock never sees; its
// ConfirmExternalNavigation covers only a reload or a closed tab. Registered once, in the capture
// phase, so it runs before blazor.web.js handles the click; nothing on the page, nothing to guard.
(function () {
    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) { return; }
        const unsaved = document.querySelector('[data-unsaved="true"]');
        const link = unsaved && e.target.closest ? e.target.closest('a[href]') : null;
        if (!link || link.target === '_blank' || link.hasAttribute('download')) { return; }
        if (!window.confirm(unsaved.getAttribute('data-unsaved-message') || 'Leave without saving?')) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);
})();
