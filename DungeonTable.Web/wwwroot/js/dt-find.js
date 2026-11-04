// Find anything's keys, inside its box: the up and down arrows walk the results and Enter opens the one
// they are on, by clicking it, so a click and Enter run the same handler. The arrows' own caret move in
// the text box is swallowed, which Blazor cannot decide when the key arrives (it decides preventDefault
// when it renders), so this lives here.
//
// The row the arrows are on is this script's, not the circuit's: it is marked with the "active" class,
// and goes back to the first row whenever the results change (a MutationObserver on the box), so typing
// always starts from the best match. A press outside the box puts it away, except on the bar's own Find
// button, which opens it.
//
// The box is drawn by the circuit, a round trip after Ctrl+K or /, and a DM who types straight away would
// lose the first letters to the page. So the key handler calls expect() the moment the shortcut is
// pressed: until the box is up, what is typed is held here (and kept from the other shortcuts, so a "3"
// typed early is a letter, not a tool), then put into the box as if typed there. Esc lets go of it.
window.dtFind = (function () {
    let state = null;
    let early = null;

    function stopEarly() {
        if (!early) {
            return '';
        }

        window.removeEventListener('keydown', early.handler, true);
        clearTimeout(early.timer);
        const typed = early.keys;
        early = null;
        return typed;
    }

    function hits() {
        return state ? Array.from(state.box.querySelectorAll('.find-hit')) : [];
    }

    function mark(index) {
        const all = hits();
        if (all.length === 0) {
            state.active = 0;
            return;
        }

        state.active = Math.max(0, Math.min(index, all.length - 1));
        all.forEach(function (hit, i) {
            hit.classList.toggle('active', i === state.active);
        });
        all[state.active].scrollIntoView({ block: 'nearest' });
    }

    return {
        expect: function () {
            if (early) {
                return;
            }

            early = { keys: '' };
            early.handler = function (e) {
                if (e.key === 'Escape') {
                    stopEarly();
                    return;
                }

                if (e.ctrlKey || e.altKey || e.metaKey) {
                    return;
                }

                if (e.key.length === 1) {
                    early.keys += e.key;
                } else if (e.key === 'Backspace') {
                    early.keys = early.keys.slice(0, -1);
                } else {
                    return;
                }

                e.preventDefault();
                e.stopImmediatePropagation();
            };
            window.addEventListener('keydown', early.handler, true);

            // A box that never comes (the circuit gone) must not swallow the keyboard for good.
            early.timer = setTimeout(stopEarly, 3000);
        },

        attach: function (box, input, dotnet) {
            this.detach();
            const typed = stopEarly();
            if (!box || !input) {
                return;
            }

            state = { box: box, input: input, active: 0 };

            state.onKey = function (e) {
                if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                    e.preventDefault();
                    mark(state.active + (e.key === 'ArrowDown' ? 1 : -1));
                } else if (e.key === 'Enter') {
                    e.preventDefault();
                    const all = hits();
                    if (all[state.active]) {
                        all[state.active].click();
                    }
                }
            };

            state.onPress = function (e) {
                if (!box.isConnected) {
                    window.dtFind.detach();
                    return;
                }

                const target = e.target;
                if (box.contains(target) || (target.closest && target.closest('.find-open-btn'))) {
                    return;
                }

                dotnet.invokeMethodAsync('FindDismissed').catch(function () { });
            };

            state.observer = new MutationObserver(function () {
                if (state) {
                    mark(0);
                }
            });
            state.observer.observe(box, { childList: true, subtree: true, characterData: true });

            input.addEventListener('keydown', state.onKey);
            document.addEventListener('pointerdown', state.onPress, true);
            input.focus();
            if (typed.length > 0) {
                // What was typed before the box was up replaces the last query, as typing over it would.
                input.value = typed;
                input.dispatchEvent(new Event('input', { bubbles: true }));
            } else {
                input.select();
            }
            mark(0);
        },

        detach: function () {
            if (!state) {
                return;
            }

            state.input.removeEventListener('keydown', state.onKey);
            document.removeEventListener('pointerdown', state.onPress, true);
            state.observer.disconnect();
            state = null;
        }
    };
})();
