// The browser's distance from UTC, in minutes east (Amsterdam in summer is 120). The pages render on
// the server, whose clock is UTC in a container and may be in another zone entirely, so a time the DM
// reads against the clock on the wall is shifted by this.
window.dtClock = {
    utcOffsetMinutes: function () {
        return -new Date().getTimezoneOffset();
    }
};
