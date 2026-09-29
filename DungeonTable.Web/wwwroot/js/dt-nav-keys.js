// Global Alt+Left / Alt+Right shortcuts for the DM briefing's navigation history. A single handler
// is registered at a time (there is one DM view), and calls back into the .NET component. Attaching
// again re-registers cleanly; detaching removes the listener on component teardown.
window.dtNavKeys = (function () {
    let handler = null;

    return {
        attach: function (dotnet) {
            this.detach();
            handler = function (e) {
                if (!e.altKey || e.ctrlKey || e.metaKey) {
                    return;
                }
                if (e.key === 'ArrowLeft') {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('NavKey', 'back');
                } else if (e.key === 'ArrowRight') {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('NavKey', 'forward');
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
