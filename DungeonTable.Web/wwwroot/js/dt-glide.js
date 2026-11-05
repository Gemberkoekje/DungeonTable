// The projector glides from one view to the next instead of jumping: when the DM moves the frame, or the
// projector follows the party, the players see the map move.
//
// The server only ever sets the new viewBox; this script makes the move. Drawing the projector's map
// is expensive (its masks and blurs; a second or more on a low-spec laptop), so tweening the viewBox
// would draw it again every frame and play as a slideshow. Instead the map is drawn once, at the new
// view, and the picture already drawn is slid there: the moment the new viewBox arrives, the wrapper
// around the map and the DM's pointer (.player-glide) gets the transform that puts the new view where
// the old one was on screen, and that transform then eases away to nothing. A transform is the
// compositor's work, so the slide stays smooth on hardware that redraws the map slowly. While it runs
// the map draws past its own box (overflow visible), so the part of the map coming into view is there.
//
// No glide when the map changes, when the screen changed size or turned (the old view no longer maps
// onto it), or when the viewer asks for reduced motion. A new view during a glide starts from wherever
// the picture is at that moment, so the follow-the-party re-framing in steps reads as one movement.
window.dtGlide = (function () {
    const DURATION = 800; // ms
    const EASING = 'cubic-bezier(0.45, 0, 0.25, 1)';

    let observer = null;
    let resizer = null;

    function reducedMotion() {
        return !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    }

    // A viewBox as numbers, or null when it is not a usable one.
    function parse(text) {
        const n = (text || '').trim().split(/[\s,]+/).map(Number);
        if (n.length !== 4 || !n.every(Number.isFinite) || n[2] <= 0 || n[3] <= 0) { return null; }
        return { x: n[0], y: n[1], w: n[2], h: n[3] };
    }

    // Where a viewBox puts the map in a box of w by h pixels, as the svg's preserveAspectRatio
    // (xMidYMid meet) does: a world point p lands at s * p + t.
    function fit(view, w, h) {
        const s = Math.min(w / view.w, h / view.h);
        return { s: s, tx: (w - view.w * s) / 2 - view.x * s, ty: (h - view.h * s) / 2 - view.y * s };
    }

    // The wrapper's transform at this moment (mid-glide or not), as a scale and a translation.
    function current(wrap) {
        const t = window.getComputedStyle(wrap).transform;
        if (!t || t === 'none') { return { s: 1, tx: 0, ty: 0 }; }
        const m = new DOMMatrixReadOnly(t);
        return { s: m.a, tx: m.e, ty: m.f };
    }

    function stop(wrap, svg) {
        const state = wrap.__dtGlide;
        if (state && state.animation) {
            state.animation.cancel();
            state.animation = null;
        }
        if (svg) { svg.style.overflow = ''; }
    }

    // What the wrapper last showed: its map and its size, which a glide needs unchanged.
    function remember(wrap) {
        const state = wrap.__dtGlide || (wrap.__dtGlide = {});
        state.map = wrap.getAttribute('data-map') || '';
        state.w = wrap.clientWidth;
        state.h = wrap.clientHeight;
        return state;
    }

    function glide(svg, oldViewBox) {
        const wrap = svg.closest('.player-glide');
        if (!wrap) { return; }
        const state = wrap.__dtGlide || remember(wrap);
        const w = wrap.clientWidth, h = wrap.clientHeight;
        const map = wrap.getAttribute('data-map') || '';
        const from = parse(oldViewBox), to = parse(svg.getAttribute('viewBox'));
        const sameStage = state.map === map && state.w === w && state.h === h;
        state.map = map; state.w = w; state.h = h;

        if (!from || !to || !sameStage || w === 0 || h === 0 || reducedMotion()) {
            stop(wrap, svg);
            return;
        }

        // On screen now: the wrapper's transform over the old view. Start the new view there.
        const now = current(wrap);
        const a = fit(from, w, h), b = fit(to, w, h);
        const k = a.s / b.s;
        const scale = now.s * k;
        const tx = now.s * (a.tx - k * b.tx) + now.tx;
        const ty = now.s * (a.ty - k * b.ty) + now.ty;
        if (Math.abs(scale - 1) < 1e-4 && Math.abs(tx) < 0.5 && Math.abs(ty) < 0.5) {
            stop(wrap, svg);
            return;
        }

        stop(wrap, null);
        svg.style.overflow = 'visible';
        const animation = wrap.animate(
            [
                { transform: 'translate(' + tx + 'px, ' + ty + 'px) scale(' + scale + ')' },
                { transform: 'translate(0px, 0px) scale(1)' }
            ],
            { duration: DURATION, easing: EASING });
        state.animation = animation;
        animation.onfinish = function () {
            if (state.animation === animation) {
                state.animation = null;
                svg.style.overflow = '';
            }
        };
    }

    function onMutations(records) {
        for (const record of records) {
            const el = record.target;
            if (record.attributeName === 'viewBox' && el.matches && el.matches('.player-glide svg.vector-map')) {
                glide(el, record.oldValue);
            } else if (record.attributeName === 'data-map' && el.classList && el.classList.contains('player-glide')) {
                // Another map: what was on screen is not on this one.
                stop(el, el.querySelector('svg.vector-map'));
                if (el.__dtGlide) { el.__dtGlide.map = el.getAttribute('data-map') || ''; }
            }
        }
    }

    return {
        attach: function (stage) {
            this.detach();
            if (!stage || !window.MutationObserver || !Element.prototype.animate) { return; }
            stage.querySelectorAll('.player-glide').forEach(remember);
            observer = new MutationObserver(onMutations);
            observer.observe(stage, { subtree: true, attributes: true, attributeOldValue: true, attributeFilter: ['viewBox', 'data-map'] });

            // Full screen, a resized window: the size a glide compares against follows. A turn changes the
            // size in the same render as the view, and is caught by the comparison instead.
            if (window.ResizeObserver) {
                resizer = new ResizeObserver(function (entries) {
                    entries.forEach(function (entry) {
                        stop(entry.target, entry.target.querySelector('svg.vector-map'));
                        remember(entry.target);
                    });
                });
                stage.querySelectorAll('.player-glide').forEach(function (wrap) { resizer.observe(wrap); });
            }
        },

        detach: function () {
            if (observer) { observer.disconnect(); observer = null; }
            if (resizer) { resizer.disconnect(); resizer = null; }
        }
    };
})();
