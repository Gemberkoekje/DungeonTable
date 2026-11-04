// The peek card on the DM's map: rest the pointer on a room for a quarter of a second and a card says
// what is in it (who is there, loot, checks, traps, the DM's note), without a click and without leaving
// the room on show in the dock. Moving off the room, pressing, panning or zooming puts it away.
//
// This module only notices where the pointer rests and places the card. Which room is under it is read
// from the regions the overlay draws (data-region-id); the card itself is drawn by .NET (MapPane, into
// .dt-peek-host inside the map's box), so it is the same card the Room tab's Nearby list shows. It is
// placed beside the pointer, on whichever side has room, whenever .NET draws it or changes it.
window.dtPeek = (function () {
    const DELAY = 250; // ms the pointer rests on a room before its card shows.
    const GAP = 16;    // px between the pointer and the card.

    function attach(box, dotnet) {
        if (!box) { return; }
        if (box.__dtpeek) { box.__dtpeek.dotnet = dotnet; return; }

        const s = {
            dotnet: dotnet,
            region: '',      // the region under the pointer
            shown: '',       // the region whose card .NET was asked to draw
            timer: 0,
            x: 0, y: 0       // where the pointer is, in client pixels
        };
        box.__dtpeek = s;

        function ask(id) {
            if (s.shown === id) { return; }
            s.shown = id;
            s.dotnet.invokeMethodAsync('OnRegionHover', id).catch(function () { /* the circuit is gone */ });
        }

        function hide() {
            clearTimeout(s.timer);
            s.timer = 0;
            ask('');
        }

        box.addEventListener('pointermove', function (e) {
            s.x = e.clientX; s.y = e.clientY;

            // A button held is a pan, a brush stroke, a measurement or a drag of the token: no card.
            if (e.buttons) { s.region = ''; hide(); return; }

            const hit = e.target && e.target.closest ? e.target.closest('[data-region-id]') : null;
            const id = hit && box.contains(hit) ? hit.getAttribute('data-region-id') || '' : '';
            if (id === s.region) { return; }

            s.region = id;
            hide();
            if (id) {
                s.timer = setTimeout(function () { s.timer = 0; ask(id); }, DELAY);
            }
        });

        // A press ends the peek, and the card stays away until the pointer moves to another room: a
        // click on a room shows it in the dock, where the same things are written out in full.
        box.addEventListener('pointerdown', function () { hide(); });
        box.addEventListener('pointerleave', function () { s.region = ''; hide(); });
        box.addEventListener('wheel', function () { s.region = ''; hide(); }, { passive: true });

        function place() {
            const card = box.querySelector('.dt-peek-host .room-peek');
            if (!card) { return; }
            const br = box.getBoundingClientRect();
            const px = s.x - br.left, py = s.y - br.top;
            const w = card.offsetWidth, h = card.offsetHeight;
            let left = px + GAP;
            if (left + w > box.clientWidth - 4) { left = px - GAP - w; }
            let top = py + GAP;
            if (top + h > box.clientHeight - 4) { top = py - GAP - h; }
            card.style.left = Math.max(4, left) + 'px';
            card.style.top = Math.max(4, top) + 'px';
            card.style.visibility = 'visible';
        }

        // .NET draws the card into the host; place it whenever the host's content changes.
        const host = box.querySelector('.dt-peek-host');
        if (host && window.MutationObserver) {
            s.observer = new MutationObserver(place);
            s.observer.observe(host, { childList: true, subtree: true, characterData: true });
        }
    }

    function detach(box) {
        const s = box && box.__dtpeek;
        if (!s) { return; }
        clearTimeout(s.timer);
        if (s.observer) { s.observer.disconnect(); }
        delete box.__dtpeek;
    }

    return { attach: attach, detach: detach };
})();
