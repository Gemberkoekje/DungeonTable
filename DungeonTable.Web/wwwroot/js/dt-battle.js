// The Battle tab's two DOM-side jobs, both of which the browser has to do rather than the server:
// walking the initiative column with Tab, and putting the cursor in a box after a re-render.
//
// Tab lives here because preventing a key's default is a *render-time* decision in Blazor — there is
// no way to decide per event — and blanket-preventing keydown would stop the DM typing at all. The
// listener is delegated from the document and does nothing unless the key came from an initiative
// box, so unlike dt-dm-keys.js there is nothing to attach or detach per component.
//
// The n/p/r shortcuts are NOT here: they are global to the DM screen and share dt-dm-keys.js's
// single input-aware handler, which the shell already owns.
window.dtBattle = (function () {
    const BOX = 'battle-init';

    function boxes() {
        return Array.prototype.slice.call(document.querySelectorAll('.' + BOX));
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Tab' || e.ctrlKey || e.altKey || e.metaKey) { return; }

        const el = e.target;
        if (!el || !el.classList || !el.classList.contains(BOX)) { return; }

        const all = boxes();
        const at = all.indexOf(el);
        const next = at + (e.shiftKey ? -1 : 1);

        // Both ends stay ordinary tab stops, so the column can always be tabbed out of and is never
        // a keyboard trap. Blur commits the typed value through Blazor's own change event.
        if (at < 0 || next < 0 || next >= all.length) { return; }

        e.preventDefault();
        all[next].focus();
        if (all[next].select) { all[next].select(); }
    });

    return {
        focus: function (id) {
            const el = document.getElementById(id);
            if (el) {
                el.focus();
                if (el.select) { el.select(); }
            }
        }
    };
})();
