// Global keyboard shortcuts for the DM screen: Ctrl+1..4 switch the Map / Info / Battle / Art tabs,
// a bare 1-8 picks a tool on the Map tab, and a bare letter (n / p / r / h) or Esc goes to whichever
// tab is on show - the turn order on the Battle tab, the art tray on the Art tab.
// A single handler is registered at a time (there is one DM view); attaching again re-registers
// cleanly, and detaching removes the listener on teardown.
//
// The bare keys are deliberately input-aware: typing "3" into the vision-range box or an "n" into a
// combatant's note must not change tool or advance the turn under the DM's hands. Ctrl-chorded keys
// carry no such risk, so they work everywhere. Alt is left alone — Alt+Left/Right is the history
// (see dt-nav-keys.js), and Tab inside the initiative column belongs to dt-battle.js.
//
// Which tab a bare key applies to is decided in C# (DmView.DmKey), not here: this file only reports
// what was pressed, as "bare:<key>". It deliberately does not know which tab is up - two tabs claim
// n and p, and putting that routing in both places is how they would drift apart.
window.dtDmKeys = (function () {
    let handler = null;

    function isTyping(e) {
        const el = e.target;
        if (!el) { return false; }
        if (el.isContentEditable) { return true; }
        const tag = (el.tagName || '').toLowerCase();
        return tag === 'input' || tag === 'textarea' || tag === 'select';
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
                    }
                    return;
                }

                // Esc is the one bare key allowed through while typing: it closes the combatant
                // detail pane, and the DM is most likely to want it gone while a cursor is in a box
                // behind it. It types nothing, so letting it through costs no keystroke.
                if (e.key === 'Escape' && !e.shiftKey) {
                    dotnet.invokeMethodAsync('DmKey', 'bare:esc');
                    return;
                }

                if (e.shiftKey || isTyping(e)) { return; }
                if (e.key >= '1' && e.key <= '8') {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('DmKey', 'tool:' + e.key);
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
