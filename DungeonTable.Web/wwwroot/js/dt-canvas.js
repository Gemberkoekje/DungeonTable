// The map canvas's one call into its scripts per render (VectorMapCanvas). It used to make four after
// every render, whatever had changed, and five more when it first drew: on a phone's hotspot each call
// is a round trip, and a render happened several times a second while the party token moved. Now the
// canvas sets the scripts up in one call, and after a render tells them in one call only what changed.
window.dtCanvas = {
    // First render: pan and zoom always; the player-view box, the editor, the party token and the peek
    // card when the canvas has them. playerBox carries where the box starts, or is null for none.
    attach: function (box, dotnet, parts) {
        window.dtPanZoom.attach(box);
        if (parts.playerBox) {
            const p = parts.playerBox;
            window.dtPlayerBox.init(box, dotnet, p.world, p.aspect, p.turns, p.held);
        }
        if (parts.editor) { window.dtEditor.attach(box, dotnet); }
        if (parts.party) { window.dtParty.attach(box, dotnet); }
        if (parts.peek) { window.dtPeek.attach(box, dotnet); }
    },

    // After a render: apply is true when the map (its viewBox) changed; each other part is null when it
    // has not changed since the scripts were last told.
    sync: function (box, apply, playerBox, party, editor) {
        if (apply) { window.dtPanZoom.apply(box); }
        if (playerBox) {
            window.dtPlayerBox.update(box, playerBox.x, playerBox.y, playerBox.w, playerBox.h, playerBox.aspect, playerBox.turns, playerBox.held);
        }
        if (party) {
            window.dtParty.update(box, party.placed, party.x, party.y, party.radius, party.cell, party.originX, party.originY, party.draggable);
        }
        if (editor) {
            window.dtEditor.setTool(box, editor.tool, editor.unit, editor.originX, editor.originY);
        }
    }
};
