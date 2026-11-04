// The party token on the DM's map: a marker where the party stands, with a faint ring at its vision
// range, that the DM drags to move the party. What the party sees on the way is worked out on the
// server; this module only moves the marker under the pointer and reports it.
//
// It is an HTML overlay on the map's box, as the player-view box is, placed from world units through
// the SVG's screen matrix, so it keeps a fixed size on screen at every zoom while the ring scales with
// the map. The projector never has one: only the DM's canvas attaches it.
//
// A drag reports itself once per square entered (and once at the end), not once per pointer move: the
// party sees from squares, and a report per frame would make the server lift from the same square
// over and over. Reports go one at a time; while one is on its way, the latest position waits, and the
// server fills in the squares between the two (PartyVision.Path), so nothing is skipped. The first
// report of a drag says 'walk' and starts a move of its own for Ctrl+Z; the rest say 'walk-on'.
//
// While the DM drags, the marker follows the pointer, not the renders the reports cause: those carry
// where the server last put the token, which lags the pointer. A press that does not move is a click,
// which opens the token's menu (the vision range), drawn by .NET and placed beside the token here.
window.dtParty = (function () {
    const SLOP = 5; // px of movement below which a press is a click, not a drag.

    function svgOf(box) { return box ? box.querySelector('svg') : null; }

    function attach(box, dotnet) {
        if (!box) { return; }
        if (box.__dtparty) { box.__dtparty.dotnet = dotnet; return; }

        const ring = document.createElement('div');
        ring.className = 'dt-party-ring';
        const token = document.createElement('div');
        token.className = 'dt-party';
        token.title = 'The party: drag to move it, click for its vision range';
        token.setAttribute('role', 'button');
        token.setAttribute('aria-label', 'Party token');
        box.appendChild(ring);
        box.appendChild(token);

        const s = {
            dotnet: dotnet, token: token, ring: ring,
            server: { placed: false, x: 0, y: 0, radius: 0, cell: 0, originX: 0, originY: 0, draggable: true },
            local: null,          // where the marker is drawn while a drag (or its last report) is under way
            pressed: false, dragging: false, downX: 0, downY: 0,
            started: false,       // a report of this drag has been sent, so the next one says 'walk-on'
            lastSquare: null, pending: null, inFlight: false
        };
        box.__dtparty = s;

        function worldOf(clientX, clientY) {
            const svg = svgOf(box);
            const m = svg && svg.getScreenCTM();
            if (!m) { return null; }
            const p = svg.createSVGPoint();
            p.x = clientX; p.y = clientY;
            const w = p.matrixTransform(m.inverse());
            return { x: w.x, y: w.y };
        }

        function squareOf(w) {
            const g = s.server;
            if (!(g.cell > 0)) { return null; }
            return Math.floor((w.x - g.originX) / g.cell) + ':' + Math.floor((w.y - g.originY) / g.cell);
        }

        function send() {
            if (s.inFlight || !s.pending) { return; }
            const w = s.pending;
            s.pending = null;
            s.inFlight = true;
            const kind = s.started ? 'walk-on' : 'walk';
            s.started = true;
            const done = function () {
                s.inFlight = false;
                if (s.pending) { send(); return; }
                // The drag is over and its last report answered: draw the token where the server put it.
                if (!s.dragging) { s.local = null; reposition(); }
            };
            s.dotnet.invokeMethodAsync('OnPartyMoved', w.x, w.y, kind).then(done, done);
        }

        function report(w) { s.pending = w; send(); }

        token.addEventListener('pointerdown', function (e) {
            if (e.button !== 0 || !s.server.draggable) { return; }
            e.stopPropagation(); e.preventDefault(); // neither pan the map nor click the room under it
            s.pressed = true; s.dragging = false;
            s.downX = e.clientX; s.downY = e.clientY;
            try { token.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
        });

        token.addEventListener('pointermove', function (e) {
            if (!s.pressed) { return; }
            if (!s.dragging) {
                if (Math.abs(e.clientX - s.downX) <= SLOP && Math.abs(e.clientY - s.downY) <= SLOP) { return; }
                s.dragging = true;
                s.started = false;
                s.lastSquare = squareOf({ x: s.server.x, y: s.server.y });
                token.classList.add('dragging');
            }
            const w = worldOf(e.clientX, e.clientY);
            if (!w) { return; }
            s.local = w;
            reposition();
            const square = squareOf(w);
            if (square !== null && square !== s.lastSquare) {
                s.lastSquare = square;
                report(w);
            }
        });

        function end(e, cancelled) {
            if (!s.pressed) { return; }
            s.pressed = false;
            try { token.releasePointerCapture(e.pointerId); } catch (err) { /* ignore */ }
            token.classList.remove('dragging');
            if (!s.dragging) {
                if (!cancelled) { s.dotnet.invokeMethodAsync('OnPartyClicked'); }
                return;
            }
            s.dragging = false;
            // Where the drag ended settles the token, even within the square last reported.
            const w = worldOf(e.clientX, e.clientY) || s.local;
            if (w) { s.local = w; report(w); }
        }
        token.addEventListener('pointerup', function (e) { end(e, false); });
        token.addEventListener('pointercancel', function (e) { end(e, true); });
        // The token's own clicks must not reach the map's click handling either.
        token.addEventListener('click', function (e) { e.stopPropagation(); });

        function toScreen(wx, wy) {
            const svg = svgOf(box);
            const m = svg && svg.getScreenCTM();
            if (!m) { return null; }
            const br = box.getBoundingClientRect();
            const p = svg.createSVGPoint();
            p.x = wx; p.y = wy;
            const q = p.matrixTransform(m);
            return { x: q.x - br.left, y: q.y - br.top, scale: m.a };
        }

        function reposition() {
            const g = s.server;
            const at = s.local || (g.placed ? { x: g.x, y: g.y } : null);
            const svg = svgOf(box);
            const screen = at && svg && svg.clientWidth > 0 ? toScreen(at.x, at.y) : null;
            const pop = box.querySelector('.dt-party-pop');
            if (!screen) {
                token.style.display = 'none';
                ring.style.display = 'none';
                if (pop) { pop.style.visibility = 'hidden'; }
                return;
            }
            token.style.display = '';
            token.style.left = screen.x + 'px';
            token.style.top = screen.y + 'px';
            token.style.pointerEvents = g.draggable ? 'auto' : 'none';
            const r = g.radius * screen.scale;
            if (r > 0) {
                ring.style.display = '';
                ring.style.left = (screen.x - r) + 'px';
                ring.style.top = (screen.y - r) + 'px';
                ring.style.width = (2 * r) + 'px';
                ring.style.height = (2 * r) + 'px';
            } else {
                ring.style.display = 'none';
            }
            if (pop) {
                // A press in the menu is the menu's: it must neither pan the map nor click the room
                // under it. .NET draws the menu afresh each time it opens, so each one gets the guard.
                if (!pop.__dtGuarded) {
                    pop.addEventListener('pointerdown', function (e) { e.stopPropagation(); });
                    pop.__dtGuarded = true;
                }
                // Beside the token, on whichever side has room.
                const room = box.clientWidth - screen.x;
                const left = room > pop.offsetWidth + 40 ? screen.x + 22 : screen.x - 22 - pop.offsetWidth;
                const top = Math.max(4, Math.min(screen.y - 16, box.clientHeight - pop.offsetHeight - 4));
                pop.style.left = Math.max(4, left) + 'px';
                pop.style.top = top + 'px';
                pop.style.visibility = 'visible';
            }
        }
        s.reposition = reposition;

        box.addEventListener('dtviewchange', reposition);
        if (window.ResizeObserver) {
            const ro = new ResizeObserver(reposition);
            ro.observe(box);
            s.observer = ro;
        }
        requestAnimationFrame(function () { requestAnimationFrame(reposition); });
    }

    return {
        attach: attach,
        // After each render: where the server has the token, the vision ring's radius in world units, the
        // grid (for telling squares apart while dragging) and whether the tool that is up lets it be dragged.
        update: function (box, placed, x, y, radius, cell, originX, originY, draggable) {
            const s = box && box.__dtparty;
            if (!s) { return; }
            s.server = {
                placed: !!placed, x: x, y: y, radius: radius > 0 ? radius : 0,
                cell: cell > 0 ? cell : 0, originX: originX || 0, originY: originY || 0, draggable: !!draggable
            };
            requestAnimationFrame(s.reposition);
        },
        detach: function (box) {
            const s = box && box.__dtparty;
            if (!s) { return; }
            if (s.observer) { s.observer.disconnect(); }
            box.removeEventListener('dtviewchange', s.reposition);
            s.token.remove();
            s.ring.remove();
            delete box.__dtparty;
        }
    };
})();
