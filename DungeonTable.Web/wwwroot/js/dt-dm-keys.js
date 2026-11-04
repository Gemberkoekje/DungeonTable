// Global keyboard shortcuts for the DM screen: Ctrl+1..4 switch the dock (Room / Battle / Campaign /
// Art), Ctrl+Z takes back the party's last move, a bare 1-8 picks a map tool (Shift+1-8 keeps a one-click tool up), Esc puts the map back in
// Look and closes what is open in the dock, "\" swaps the dock between narrow and wide, Tab opens and
// closes the dock on a screen too narrow to show it beside the map, and a bare letter (n / p / r / h)
// goes to whichever dock tab is on show - the turn order on Battle, the art tray on Art.
// A single handler is registered at a time (there is one DM view); attaching again re-registers
// cleanly, and detaching removes the listener on teardown.
//
// The bare keys are deliberately input-aware: typing "3" into the vision-range box or an "n" into a
// combatant's note must not change tool or advance the turn under the DM's hands. Ctrl-chorded keys
// carry no such risk, so they work everywhere. Alt is left alone - Alt+Left/Right is the history
// (see dt-nav-keys.js), and Tab inside the initiative column belongs to dt-battle.js.
//
// Which tab a bare key applies to is decided in C# (DmView.DmKey), not here: this file only reports
// what was pressed, as "bare:<key>". It deliberately does not know which tab is up - two tabs claim
// n and p, and putting that routing in both places is how they would drift apart.
window.dtDmKeys = (function () {
    let handler = null;

    // The width below which the dock is a drawer over the map (app.css, the same breakpoint).
    const narrow = window.matchMedia('(max-width: 1199px)');

    function isTyping(e) {
        const el = e.target;
        if (!el) { return false; }
        if (el.isContentEditable) { return true; }
        const tag = (el.tagName || '').toLowerCase();
        return tag === 'input' || tag === 'textarea' || tag === 'select';
    }

    // The digit a key is, by its place on the keyboard: with Shift held, e.key is "!" or "@" (or
    // whatever the layout puts there), so the tool shortcuts read e.code.
    function digitOf(e) {
        const match = /^(?:Digit|Numpad)([1-8])$/.exec(e.code || '');
        return match ? match[1] : '';
    }

    return {
        attach: function (dotnet) {
            this.detach();
            handler = function (e) {
                if (e.altKey || e.metaKey || e.repeat) { return; }

                if (e.ctrlKey) {
                    if (e.key >= '1' && e.key <= '4' && !e.shiftKey) {
                        e.preventDefault();
                        dotnet.invokeMethodAsync('DmKey', 'tab:' + e.key);
                    } else if ((e.key === 'z' || e.key === 'Z') && !e.shiftKey && !isTyping(e)) {
                        // Ctrl+Z in a box undoes the typing; anywhere else it takes back the party's
                        // last move.
                        e.preventDefault();
                        dotnet.invokeMethodAsync('DmKey', 'undo');
                    }
                    return;
                }

                // Esc is the one bare key allowed through while typing: it closes the card sheet or
                // the combatant detail pane, and the DM is most likely to want those gone while a
                // cursor is in a box behind them. It types nothing, so letting it through costs no
                // keystroke.
                if (e.key === 'Escape' && !e.shiftKey) {
                    dotnet.invokeMethodAsync('DmKey', 'bare:esc');
                    return;
                }

                if (isTyping(e)) { return; }

                const digit = digitOf(e);
                if (digit) {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('DmKey', 'tool:' + digit + (e.shiftKey ? ':keep' : ''));
                    return;
                }

                // Tab moves the focus as usual while the dock sits beside the map; only on a narrow
                // screen, where the dock is a drawer, does it open and close the drawer.
                if (e.key === 'Tab' && !e.shiftKey && narrow.matches) {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('DmKey', 'bare:drawer');
                    return;
                }

                if (e.shiftKey) { return; }
                if (e.key === '\\') {
                    // The dock's width is the browser's own business (dt-dock.js), not the circuit's.
                    e.preventDefault();
                    if (window.dtDock) { window.dtDock.flip(); }
                } else if (e.key === 'n' || e.key === 'p' || e.key === 'r' || e.key === 'h') {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('DmKey', 'bare:' + e.key);
                }
            };
            window.addEventListener('keydown', handler);
        },

        detach: function () {
            if (handler) {
                window.removeEventListener('keydown', handler);
                handler = null;
            }
        }
    };
})();
