using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Art;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;

namespace DungeonTable.Web.Services;

/// <summary>
/// Server-authoritative state shared by the DM and player circuits (one live table). Holds the
/// player projector's viewport rectangle (in map world units), the player screen's aspect ratio,
/// the reveal state that drives fog-of-war (which regions and features are revealed and which grid
/// cells the DM has painted), the party token the DM moves on their own map, and the picture laid over
/// the map on the projector. Raises
/// <see cref="Changed"/> so both views re-render when any of it updates. Registered as a singleton;
/// mutated by the DM circuit and observed by the player.
/// </summary>
/// <remarks>
/// <para>
/// The reveals, the open doors, the framing and the party token belong to one map. When the table moves
/// to another,
/// <see cref="SetCurrentMap"/> sets them aside under the map they were on and brings back whatever
/// was left on the new one, so a party that goes down a floor and back up finds the first floor as
/// they left it.
/// </para>
/// <para>
/// <see cref="Capture"/> and <see cref="Restore"/> are how this outlives the process: the
/// persistence coordinator captures a <see cref="RevealSnapshot"/> whenever <see cref="Changed"/>
/// fires and restores one at startup, before any circuit connects.
/// </para>
/// </remarks>
public sealed class SessionState
{
    // What the player screen is assumed to be until it reports its own aspect ratio.
    private const double DefaultAspect = 16.0 / 9.0;

    // How many of the party's reveals Ctrl+Z can take back, newest first. An evening's worth of steps
    // back is more than anyone undoes; the bound only keeps the list from growing all night.
    private const int PartyUndoDepth = 50;

    // The DM and player circuits run on different threads; guard the shared fields so each sees
    // the other's writes. Changed is always raised outside the lock to avoid reentrancy.
    private readonly object gate = new object();
    private readonly HashSet<string> revealedRegionIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> revealedFeatureIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<(int Col, int Row)> fogCells = new HashSet<(int, int)>();
    // Doors standing open: seeded from each door's DefaultOpen the first time a map is shown and
    // toggled during play. Saved with the reveals, and set aside with them on a map switch.
    private readonly HashSet<string> openDoorIds = new HashSet<string>(StringComparer.Ordinal);
    // What was left on each map the table is not on now, keyed by map id. Never holds the current
    // map: its state is the fields above.
    private readonly Dictionary<string, MapRevealSnapshot> otherMaps =
        new Dictionary<string, MapRevealSnapshot>(StringComparer.Ordinal);
    // One-step undo for a clear: what the last one threw away. Clearing destroys a whole evening of
    // fog state on a misclick, so what it hid is kept until the next clear or a map switch. Reveals
    // belong to one map, so a snapshot taken on another map would be meaningless.
    private readonly HashSet<string> clearedRegionIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> clearedFeatureIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<(int Col, int Row)> clearedCells = new HashSet<(int, int)>();
    private bool hasClearSnapshot;
    // The picture on the projector. Held here beside the reveals rather than on the scoped DM
    // workspace for the same reason the reveals are: it is what the *table* is showing, so a DM
    // refresh, a second DM window and a restart must all agree on it.
    private readonly List<string> trayImageIds = new List<string>();
    private string shownArtImageId = string.Empty;
    private bool shownArtVisible;
    private ArtSize shownArtSize = ArtSize.Medium;
    // Where the DM last pointed at the projector. Live-only state beside the open doors: a ping is
    // "look here, now", so it is neither captured nor restored (see PingMarker), and it is dropped
    // on a map switch because its coordinates belong to the map it was placed on.
    private PingMarker ping = PingMarker.None;
    private long pingSequence;
    // The party token on the map in play: the DM's own, never drawn on the projector. Set aside with the
    // reveals on a map switch and saved with them, vision range included.
    private PartyToken party = PartyToken.None;
    // What each of the party's moves, opened doors and vision changes showed, newest last, for Ctrl+Z.
    // Live only, like the clear's undo: it belongs to the map it was made on and to this evening.
    private readonly List<PartyReveal> partyReveals = new List<PartyReveal>();
    // Whether the projector re-frames on the party when the token leaves the middle of its view.
    private bool projectorFollowsParty;
    private MapBounds playerViewport;
    private double playerAspect = DefaultAspect;
    private string currentMapId = string.Empty;
    // The map whose default-open doors and starting viewport have already been seeded. It lives here
    // rather than on the (scoped) DM workspace because seeding is a property of the *table*, not of a
    // browser tab: a refresh, a second DM window or an app restart would otherwise re-seed and throw
    // away the framing the DM had set and every door they had opened. A map brought back from
    // otherMaps counts as seeded: its doors and framing are the ones the table left there.
    private string seededMapId = string.Empty;

    /// <summary>Raised whenever the shared state changes.</summary>
    public event Action Changed;

    /// <summary>The region of the map shown on the player projector, in world units.</summary>
    public MapBounds PlayerViewport
    {
        get { lock (gate) { return playerViewport; } }
    }

    /// <summary>The player screen's width / height aspect ratio.</summary>
    public double PlayerAspect
    {
        get { lock (gate) { return playerAspect; } }
    }

    /// <summary>The id of the map the reveal state belongs to.</summary>
    public string CurrentMapId
    {
        get { lock (gate) { return currentMapId; } }
    }

    /// <summary>
    /// The map whose starting viewport and default-open doors have already been seeded, or an empty
    /// string. A DM circuit compares its loaded map against this to decide whether to seed at all.
    /// </summary>
    public string SeededMapId
    {
        get { lock (gate) { return seededMapId; } }
    }

    /// <summary>
    /// Records that a map's starting viewport and default-open doors have been seeded, so no later
    /// circuit re-seeds over the DM's own framing and opened doors.
    /// </summary>
    /// <param name="mapId">The map that has just been seeded.</param>
    public void MarkSeeded(string mapId)
    {
        string id = mapId ?? string.Empty;
        bool changed;
        lock (gate)
        {
            changed = !string.Equals(id, seededMapId, StringComparison.Ordinal);
            seededMapId = id;
        }

        // Not visible state, but it is *saved* state: without the notification a restart could restore
        // a table that thinks it still needs seeding and re-seed over the restored framing.
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Sets the player viewport rectangle and notifies observers.</summary>
    /// <param name="viewport">The new viewport in world units.</param>
    public void SetPlayerViewport(MapBounds viewport)
    {
        lock (gate)
        {
            playerViewport = viewport;
        }

        Changed?.Invoke();
    }

    /// <summary>Reports the player screen aspect ratio; ignored when non-finite or non-positive, and
    /// clamped to a sane range so a bogus report can't degenerate the DM viewport.</summary>
    /// <param name="aspect">Width divided by height.</param>
    public void SetPlayerAspect(double aspect)
    {
        // This arrives from JS (a browser trust boundary): reject non-finite / non-positive values and
        // clamp the rest, so Infinity or an absurd ratio can never collapse the seeded viewport.
        if (!IsUsableAspect(aspect))
        {
            return;
        }

        double clamped = ClampAspect(aspect);
        bool changed = false;
        lock (gate)
        {
            if (Math.Abs(clamped - playerAspect) > 0.0005)
            {
                playerAspect = clamped;
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Records which map the table is on. Switching to a different map sets the one being left aside,
    /// with what was revealed on it, its open doors and its framing, and brings back what was left on
    /// the new one, or nothing the first time it is shown. Each map has its own world coordinates,
    /// regions and doors, so nothing carries over from one to the other.
    /// </summary>
    /// <param name="mapId">The id of the newly loaded map.</param>
    public void SetCurrentMap(string mapId)
    {
        string id = mapId ?? string.Empty;
        bool changed = false;
        lock (gate)
        {
            if (!string.Equals(id, currentMapId, StringComparison.Ordinal))
            {
                SetAside();
                currentMapId = id;
                ClearRevealState();
                openDoorIds.Clear();
                party = new PartyToken { VisionFeet = PartyToken.DefaultVisionFeet };
                BringBack();
                // A ping is a point in the old map's world coordinates; on another map it would
                // land somewhere arbitrary.
                ping = PingMarker.None;
                // The undo snapshot belongs to the map that was cleared; another map's reveal ids
                // mean nothing here, so drop it rather than let Undo restore foreign ids. The party's
                // undo is the same: its cells and positions belong to the map it was made on.
                DropClearSnapshot();
                partyReveals.Clear();
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Reveals or hides a region for the players.</summary>
    /// <param name="regionId">The region id to toggle.</param>
    public void ToggleRegion(string regionId) => ToggleId(revealedRegionIds, regionId);

    /// <summary>Reveals or hides an individual feature (a marker, or a door/stair object).</summary>
    /// <param name="featureId">The feature or object id to toggle.</param>
    public void ToggleFeature(string featureId) => ToggleId(revealedFeatureIds, featureId);

    /// <summary>Toggles a door between open and closed for the players.</summary>
    /// <param name="doorId">The door object id to toggle.</param>
    public void ToggleDoorOpen(string doorId) => ToggleId(openDoorIds, doorId);

    /// <summary>Sets a door's open state explicitly; raises <see cref="Changed"/> only on a real change.</summary>
    /// <param name="doorId">The door object id.</param>
    /// <param name="open">True to open the door, false to close it.</param>
    public void SetDoorOpen(string doorId, bool open)
    {
        if (string.IsNullOrEmpty(doorId))
        {
            return;
        }

        bool changed;
        lock (gate)
        {
            changed = open ? openDoorIds.Add(doorId) : openDoorIds.Remove(doorId);
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Replaces the set of open doors (a map's default-open doors at load time), raising
    /// <see cref="Changed"/> only when the set actually differs.</summary>
    /// <param name="doorIds">The ids of the doors that should start open.</param>
    public void SeedOpenDoors(IEnumerable<string> doorIds)
    {
        var seed = new HashSet<string>(doorIds, StringComparer.Ordinal);
        bool changed;
        lock (gate)
        {
            changed = !openDoorIds.SetEquals(seed);
            if (changed)
            {
                openDoorIds.Clear();
                foreach (string id in seed)
                {
                    openDoorIds.Add(id);
                }
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Paints or erases a single grid cell in the fog reveal mask.</summary>
    /// <param name="col">Grid column.</param>
    /// <param name="row">Grid row.</param>
    /// <param name="erase">True to hide the cell again, false to reveal it.</param>
    public void PaintCell(int col, int row, bool erase)
    {
        bool changed;
        lock (gate)
        {
            changed = erase ? fogCells.Remove((col, row)) : fogCells.Add((col, row));
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Reveals a batch of grid cells at once (the fog lifter), raising <see cref="Changed"/>
    /// only if any were newly revealed.</summary>
    /// <param name="cells">The grid cells to reveal.</param>
    public void PaintCells(IEnumerable<(int Col, int Row)> cells) => PaintCells(cells, erase: false);

    /// <summary>Paints or erases a batch of grid cells at once (a multi-cell fog brush stroke),
    /// raising <see cref="Changed"/> only if the mask actually changed.</summary>
    /// <param name="cells">The grid cells to paint or erase.</param>
    /// <param name="erase">True to hide the cells again, false to reveal them.</param>
    public void PaintCells(IEnumerable<(int Col, int Row)> cells, bool erase)
    {
        bool changed = false;
        lock (gate)
        {
            foreach ((int col, int row) in cells)
            {
                changed |= erase ? fogCells.Remove((col, row)) : fogCells.Add((col, row));
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Hides everything again: clears revealed regions, features and painted cells. What was
    /// cleared is snapshotted first, so a misclick can be taken back with
    /// <see cref="UndoClearReveals"/> until the next clear or map switch.
    /// </summary>
    public void ClearReveals()
    {
        bool changed;
        lock (gate)
        {
            clearedRegionIds.Clear();
            clearedFeatureIds.Clear();
            clearedCells.Clear();
            clearedRegionIds.UnionWith(revealedRegionIds);
            clearedFeatureIds.UnionWith(revealedFeatureIds);
            clearedCells.UnionWith(fogCells);

            changed = ClearRevealState();

            // What the party's moves showed is gone with everything else; taking one back now would
            // only move the token.
            partyReveals.Clear();

            // Nothing was cleared: leave no snapshot, so Undo stays unavailable rather than
            // offering to restore an empty set.
            hasClearSnapshot = changed;
            if (!changed)
            {
                DropClearSnapshot();
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>True when the last <see cref="ClearReveals"/> can still be undone.</summary>
    public bool CanUndoClearReveals
    {
        get { lock (gate) { return hasClearSnapshot; } }
    }

    /// <summary>
    /// Puts back what the last <see cref="ClearReveals"/> hid. Restored ids are merged into the
    /// current reveal state, so anything revealed since the clear survives the undo. Does nothing
    /// when there is no snapshot; the snapshot is one step deep and is spent by this call.
    /// </summary>
    public void UndoClearReveals()
    {
        bool changed;
        lock (gate)
        {
            changed = hasClearSnapshot;
            if (changed)
            {
                revealedRegionIds.UnionWith(clearedRegionIds);
                revealedFeatureIds.UnionWith(clearedFeatureIds);
                fogCells.UnionWith(clearedCells);
            }

            DropClearSnapshot();
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Snapshot of the revealed region ids, safe to enumerate off-thread.</summary>
    public IReadOnlyCollection<string> RevealedRegionIds()
    {
        lock (gate) { return revealedRegionIds.ToArray(); }
    }

    /// <summary>Snapshot of the revealed feature/object ids, safe to enumerate off-thread.</summary>
    public IReadOnlyCollection<string> RevealedFeatureIds()
    {
        lock (gate) { return revealedFeatureIds.ToArray(); }
    }

    /// <summary>Snapshot of the painted reveal cells, safe to enumerate off-thread.</summary>
    public IReadOnlyCollection<(int Col, int Row)> FogCells()
    {
        lock (gate) { return fogCells.ToArray(); }
    }

    /// <summary>Snapshot of the doors currently open, safe to enumerate off-thread.</summary>
    public IReadOnlyCollection<string> OpenDoorIds()
    {
        lock (gate) { return openDoorIds.ToArray(); }
    }

    // ---- The party token -----------------------------------------------------------------------

    /// <summary>The party token on the map in play: where it stands, and how far the party sees.</summary>
    public PartyToken Party
    {
        get { lock (gate) { return party; } }
    }

    /// <summary>True when the projector follows the party token (see <see cref="SetProjectorFollowsParty"/>).</summary>
    public bool ProjectorFollowsParty
    {
        get { lock (gate) { return projectorFollowsParty; } }
    }

    /// <summary>True when Ctrl+Z has one of the party's reveals to take back.</summary>
    public bool CanUndoPartyMove
    {
        get { lock (gate) { return partyReveals.Count > 0; } }
    }

    /// <summary>How a door stands for the players now: open or closed, found or not.</summary>
    /// <param name="doorId">The door's object id.</param>
    /// <returns>The door's state, or <see cref="DoorState.None"/> for an empty id.</returns>
    public DoorState DoorStateOf(string doorId)
    {
        if (string.IsNullOrEmpty(doorId))
        {
            return DoorState.None;
        }

        lock (gate)
        {
            return new DoorState(doorId, openDoorIds.Contains(doorId), revealedFeatureIds.Contains(doorId));
        }
    }

    /// <summary>
    /// Moves the party token and reveals what the party saw on the way. A move is one thing to take
    /// back: a drag reports itself a cell at a time, and every report after the first joins the move the
    /// first one started, so Ctrl+Z takes back the whole drag.
    /// </summary>
    /// <param name="worldX">Where the token now stands, in world units.</param>
    /// <param name="worldY">Where the token now stands, in world units.</param>
    /// <param name="seen">The cells the party saw on the way; those not yet revealed are revealed.</param>
    /// <param name="joinLast">True when this continues the last move rather than starting a new one.</param>
    public void MoveParty(double worldX, double worldY, IEnumerable<(int Col, int Row)> seen, bool joinLast)
    {
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return;
        }

        bool changed;
        lock (gate)
        {
            PartyToken before = party;
            party = new PartyToken { IsPlaced = true, X = worldX, Y = worldY, VisionFeet = before.VisionFeet };

            bool joining = joinLast && partyReveals.Count > 0;
            PartyReveal move = joining ? partyReveals[^1] : new PartyReveal(before, DoorState.None);
            int added = RevealInto(move, seen);
            changed = added > 0 || !before.IsPlaced || Moved(before, worldX, worldY);

            // A move that went nowhere and showed nothing (a drag let go in the square it started from)
            // is nothing to take back: kept, it would make the next Ctrl+Z seem to do nothing.
            if (changed && !joining)
            {
                AddPartyReveal(move);
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Reveals what the party sees from where it stands now that something changed: a door opened, or
    /// the vision range grew. Something to take back only when it showed anything new; then Ctrl+Z hides
    /// it again and puts the door back as it stood.
    /// </summary>
    /// <param name="seen">The cells the party now sees.</param>
    /// <param name="door">The door that was changed, as it stood before; <see cref="DoorState.None"/> for none.</param>
    public void RevealFromParty(IEnumerable<(int Col, int Row)> seen, DoorState door)
    {
        bool changed;
        lock (gate)
        {
            var reveal = new PartyReveal(party, door);
            changed = RevealInto(reveal, seen) > 0;
            if (changed)
            {
                AddPartyReveal(reveal);
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Takes back the party's last reveal: hides what it showed that was not already showing, puts the
    /// token back where it stood, and puts back a door it came from as that door stood. The vision
    /// range stays as the DM last set it. Does nothing when there is nothing to take back.
    /// </summary>
    public void UndoPartyMove()
    {
        lock (gate)
        {
            if (partyReveals.Count == 0)
            {
                return;
            }

            PartyReveal last = partyReveals[^1];
            partyReveals.RemoveAt(partyReveals.Count - 1);
            fogCells.ExceptWith(last.Added);
            party = new PartyToken
            {
                IsPlaced = last.Before.IsPlaced,
                X = last.Before.X,
                Y = last.Before.Y,
                VisionFeet = party.VisionFeet,
            };

            if (last.Door.IsSet)
            {
                SetIn(openDoorIds, last.Door.Id, last.Door.Open);
                SetIn(revealedFeatureIds, last.Door.Id, last.Door.Discovered);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Sets how far the party sees, brought into bounds (<see cref="PartyToken.ClampVision"/>).</summary>
    /// <param name="feet">The vision range in feet.</param>
    public void SetPartyVision(int feet)
    {
        int vision = PartyToken.ClampVision(feet);
        bool changed;
        lock (gate)
        {
            changed = vision != party.VisionFeet;
            party = new PartyToken { IsPlaced = party.IsPlaced, X = party.X, Y = party.Y, VisionFeet = vision };
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Takes the token off the map, keeping its vision range. Nothing it revealed is hidden: what the
    /// party has seen stays seen.
    /// </summary>
    public void RemoveParty()
    {
        bool changed;
        lock (gate)
        {
            changed = party.IsPlaced;
            party = new PartyToken { VisionFeet = party.VisionFeet };
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Sets whether the projector follows the party. The re-framing itself needs the map, so the DM's
    /// screen does it as the token moves (see <see cref="PartyMoves"/>); this is the setting, which is
    /// the table's, so every DM window and a restart agree on it.
    /// </summary>
    /// <param name="follow">True to follow the party.</param>
    public void SetProjectorFollowsParty(bool follow)
    {
        bool changed;
        lock (gate)
        {
            changed = follow != projectorFollowsParty;
            projectorFollowsParty = follow;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    // ---- Pointing at the projector ------------------------------------------------------------

    /// <summary>Where the DM last pointed, or <see cref="PingMarker.None"/>.</summary>
    public PingMarker Ping
    {
        get { lock (gate) { return ping; } }
    }

    /// <summary>
    /// Points at a spot on the map for the players. Always issues a new sequence number, even for
    /// the same spot twice — pointing again is a fresh "look <em>here</em>", and the projector's
    /// animation only replays when the number changes.
    /// </summary>
    /// <param name="worldX">The point's X in map world units.</param>
    /// <param name="worldY">The point's Y in map world units.</param>
    public void PingAt(double worldX, double worldY)
    {
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return;
        }

        lock (gate)
        {
            pingSequence++;
            ping = new PingMarker { X = worldX, Y = worldY, Sequence = pingSequence };
        }

        Changed?.Invoke();
    }

    /// <summary>Takes the pointer down again. The animation fades on its own; this is for the DM's
    /// own map, where the last ping stays marked so they can see where they pointed.</summary>
    public void ClearPing()
    {
        bool changed;
        lock (gate)
        {
            changed = ping.IsSet;
            ping = PingMarker.None;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    // ---- The picture on the projector ---------------------------------------------------------

    /// <summary>The catalogue id of the staged picture; an empty string when none is staged.</summary>
    public string ShownArtImageId
    {
        get { lock (gate) { return shownArtImageId; } }
    }

    /// <summary>Whether the staged picture is currently on the projector.</summary>
    public bool ShownArtVisible
    {
        get { lock (gate) { return shownArtVisible; } }
    }

    /// <summary>How large the staged picture is drawn.</summary>
    public ArtSize ShownArtSize
    {
        get { lock (gate) { return shownArtSize; } }
    }

    /// <summary>Snapshot of the prepared tray, in flip order, safe to enumerate off-thread.</summary>
    public IReadOnlyList<string> TrayImageIds()
    {
        lock (gate) { return trayImageIds.ToArray(); }
    }

    /// <summary>
    /// Stages a picture and puts it on the projector. Staging is the show: the DM clicks a picture
    /// because they want the party to see it now, and needing a second click for that would cost a
    /// beat at exactly the wrong moment. Use <see cref="HideArt"/> to take it down again.
    /// </summary>
    /// <param name="imageId">The catalogue id to show; a blank id clears the selection.</param>
    public void ShowArt(string imageId)
    {
        string id = imageId ?? string.Empty;
        lock (gate)
        {
            shownArtImageId = id;
            shownArtVisible = id.Length > 0;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Takes the picture off the projector without forgetting it. Hiding is not clearing — the
    /// selection and the tray survive, so the same picture comes straight back.
    /// </summary>
    public void HideArt()
    {
        bool changed;
        lock (gate)
        {
            changed = shownArtVisible;
            shownArtVisible = false;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Puts the staged picture back on the projector; does nothing when none is staged.</summary>
    public void ShowStagedArt()
    {
        bool changed;
        lock (gate)
        {
            changed = !shownArtVisible && shownArtImageId.Length > 0;
            shownArtVisible = changed || shownArtVisible;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Sets how large the picture is drawn on the projector.</summary>
    /// <param name="size">The new size; <see cref="ArtSize.None"/> is read as <see cref="ArtSize.Medium"/>.</param>
    public void SetArtSize(ArtSize size)
    {
        ArtSize chosen = size == ArtSize.None ? ArtSize.Medium : size;
        bool changed;
        lock (gate)
        {
            changed = chosen != shownArtSize;
            shownArtSize = chosen;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Adds a picture to the tray if it is not already there.</summary>
    /// <param name="imageId">The catalogue id to stage for this scene.</param>
    public void AddToTray(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
        {
            return;
        }

        bool changed;
        lock (gate)
        {
            changed = !trayImageIds.Contains(imageId, StringComparer.Ordinal);
            if (changed)
            {
                trayImageIds.Add(imageId);
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Removes a picture from the tray. The picture stays on the projector if it was showing —
    /// dropping it from the tray is a prep action, and having it blank the beamer would be a nasty
    /// surprise mid-scene.
    /// </summary>
    /// <param name="imageId">The catalogue id to drop.</param>
    public void RemoveFromTray(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
        {
            return;
        }

        bool changed;
        lock (gate)
        {
            changed = trayImageIds.Remove(imageId);
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Empties the tray, leaving whatever is on the projector where it is.</summary>
    public void ClearTray()
    {
        bool changed;
        lock (gate)
        {
            changed = trayImageIds.Count > 0;
            trayImageIds.Clear();
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Moves to the next or previous picture in the tray and shows it, wrapping at both ends. Does
    /// nothing when the tray is empty. When the staged picture is not in the tray, stepping starts
    /// from the tray's first entry rather than doing nothing.
    /// </summary>
    /// <param name="forward">True for the next picture, false for the previous one.</param>
    public void StepTray(bool forward)
    {
        string next;
        lock (gate)
        {
            if (trayImageIds.Count == 0)
            {
                return;
            }

            int current = trayImageIds.IndexOf(shownArtImageId);
            int step = forward ? 1 : -1;
            int fromOutside = forward ? 0 : trayImageIds.Count - 1;
            int index = current < 0
                ? fromOutside
                : (current + step + trayImageIds.Count) % trayImageIds.Count;

            next = trayImageIds[index];
            shownArtImageId = next;
            shownArtVisible = true;
        }

        Changed?.Invoke();
    }

    // ---- Persistence ------------------------------------------------------------------------

    /// <summary>
    /// Captures everything about the reveal state that must survive an app restart, as one consistent
    /// picture taken under the lock. This is the <em>persistence</em> snapshot; the per-collection
    /// accessors above are what the views render from.
    /// </summary>
    /// <returns>The state to store.</returns>
    public RevealSnapshot Capture()
    {
        lock (gate)
        {
            return new RevealSnapshot
            {
                CurrentMapId = currentMapId,
                SeededMapId = seededMapId,
                RevealedRegionIds = revealedRegionIds.ToArray(),
                RevealedFeatureIds = revealedFeatureIds.ToArray(),
                OpenDoorIds = openDoorIds.ToArray(),
                FogCells = ToFogCells(fogCells),
                PlayerViewport = ToViewport(playerViewport),
                PlayerAspect = playerAspect,
                ShownArt = new ShownArtSnapshot
                {
                    ImageId = shownArtImageId,
                    Visible = shownArtVisible,
                    Size = shownArtSize,
                    TrayImageIds = trayImageIds.ToArray(),
                },
                OtherMaps = otherMaps.Values
                    .OrderBy(map => map.MapId, StringComparer.Ordinal)
                    .Select(Copy)
                    .ToArray(),
                PartyToken = ToSnapshot(party),
                ProjectorFollowsParty = projectorFollowsParty,
            };
        }
    }

    /// <summary>
    /// Puts a stored reveal state back, replacing whatever is held. Called once at startup, before
    /// any circuit connects, so the first DM and player views render the table as it was left.
    /// </summary>
    /// <remarks>
    /// The clear-reveals undo snapshot, and the party's, are dropped rather than restored: each is a
    /// "take that back" affordance measured in seconds, and offering to undo a clear from a previous evening would be
    /// worse than not offering it. A stored aspect ratio is put through the same validation as one
    /// reported by a browser, so a corrupt document cannot collapse the viewport.
    /// </remarks>
    /// <param name="snapshot">The stored state; a null snapshot is ignored.</param>
    public void Restore(RevealSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        lock (gate)
        {
            currentMapId = snapshot.CurrentMapId ?? string.Empty;
            seededMapId = snapshot.SeededMapId ?? string.Empty;

            Replace(revealedRegionIds, snapshot.RevealedRegionIds);
            Replace(revealedFeatureIds, snapshot.RevealedFeatureIds);
            Replace(openDoorIds, snapshot.OpenDoorIds);
            ReplaceCells(snapshot.FogCells);

            // The map in play is the fields above, so an entry for it is not read, and neither is a
            // second entry for a map already read.
            otherMaps.Clear();
            IEnumerable<MapRevealSnapshot> others = (snapshot.OtherMaps ?? Array.Empty<MapRevealSnapshot>())
                .Where(map => map is not null && !string.IsNullOrEmpty(map.MapId))
                .Where(map => !string.Equals(map.MapId, currentMapId, StringComparison.Ordinal));
            foreach (MapRevealSnapshot map in others)
            {
                otherMaps.TryAdd(map.MapId, Copy(map));
            }

            ShownArtSnapshot art = snapshot.ShownArt ?? new ShownArtSnapshot();
            shownArtImageId = art.ImageId ?? string.Empty;
            shownArtVisible = art.Visible && shownArtImageId.Length > 0;
            shownArtSize = art.Size == ArtSize.None ? ArtSize.Medium : art.Size;
            trayImageIds.Clear();
            trayImageIds.AddRange((art.TrayImageIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal));

            playerViewport = ToBounds(snapshot.PlayerViewport);
            playerAspect = IsUsableAspect(snapshot.PlayerAspect)
                ? ClampAspect(snapshot.PlayerAspect)
                : DefaultAspect;

            party = ToToken(snapshot.PartyToken);
            projectorFollowsParty = snapshot.ProjectorFollowsParty;

            DropClearSnapshot();
            partyReveals.Clear();
        }

        Changed?.Invoke();
    }

    private static void Replace(HashSet<string> set, IReadOnlyList<string> values)
    {
        set.Clear();
        IEnumerable<string> stored = (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrEmpty(value));

        foreach (string value in stored)
        {
            set.Add(value);
        }
    }

    private static FogCell[] ToFogCells(IEnumerable<(int Col, int Row)> cells) =>
        cells.Select(cell => new FogCell { Col = cell.Col, Row = cell.Row }).ToArray();

    private static ViewportSnapshot ToViewport(MapBounds bounds) => new ViewportSnapshot
    {
        MinX = bounds.MinX,
        MinY = bounds.MinY,
        MaxX = bounds.MaxX,
        MaxY = bounds.MaxY,
    };

    private static MapBounds ToBounds(ViewportSnapshot viewport)
    {
        ViewportSnapshot view = viewport ?? new ViewportSnapshot();
        return new MapBounds(view.MinX, view.MinY, view.MaxX, view.MaxY);
    }

    // A copy that shares nothing a caller could change, with what a stored document may lack (a list,
    // an id, a cell) left out rather than kept as null.
    private static MapRevealSnapshot Copy(MapRevealSnapshot map) => new MapRevealSnapshot
    {
        MapId = map.MapId,
        RevealedRegionIds = Ids(map.RevealedRegionIds),
        RevealedFeatureIds = Ids(map.RevealedFeatureIds),
        OpenDoorIds = Ids(map.OpenDoorIds),
        FogCells = (map.FogCells ?? Array.Empty<FogCell>())
            .Where(cell => cell is not null)
            .Select(cell => new FogCell { Col = cell.Col, Row = cell.Row })
            .ToArray(),
        PlayerViewport = ToViewport(ToBounds(map.PlayerViewport)),
        PartyToken = ToSnapshot(ToToken(map.PartyToken)),
    };

    private static PartyTokenSnapshot ToSnapshot(PartyToken token) => new PartyTokenSnapshot
    {
        Placed = token.IsPlaced,
        X = token.X,
        Y = token.Y,
        VisionFeet = token.VisionFeet,
    };

    // A stored token with a position that is not a number is left off the map rather than drawn
    // nowhere, and a range a document lacks or garbles is the default.
    private static PartyToken ToToken(PartyTokenSnapshot stored)
    {
        PartyTokenSnapshot token = stored ?? new PartyTokenSnapshot();
        bool placed = token.Placed && double.IsFinite(token.X) && double.IsFinite(token.Y);
        return new PartyToken
        {
            IsPlaced = placed,
            X = placed ? token.X : 0,
            Y = placed ? token.Y : 0,
            VisionFeet = PartyToken.ClampVision(token.VisionFeet),
        };
    }

    private static bool Moved(PartyToken token, double worldX, double worldY) =>
        Math.Abs(token.X - worldX) > 1e-9 || Math.Abs(token.Y - worldY) > 1e-9;

    private static void SetIn(HashSet<string> set, string id, bool present)
    {
        if (present)
        {
            set.Add(id);
        }
        else
        {
            set.Remove(id);
        }
    }

    private static string[] Ids(IReadOnlyList<string> ids) =>
        (ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).ToArray();

    private static bool IsUsableAspect(double aspect) => double.IsFinite(aspect) && aspect > 0;

    private static double ClampAspect(double aspect) => Math.Clamp(aspect, 0.2, 5.0);

    private void ToggleId(HashSet<string> set, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        lock (gate)
        {
            if (!set.Remove(id))
            {
                set.Add(id);
            }
        }

        Changed?.Invoke();
    }

    // Replaces the painted cells with stored ones, leaving out any a document holds as null. Caller
    // holds gate.
    private void ReplaceCells(IReadOnlyList<FogCell> cells)
    {
        fogCells.Clear();
        foreach (FogCell cell in (cells ?? Array.Empty<FogCell>()).Where(cell => cell is not null))
        {
            fogCells.Add((cell.Col, cell.Row));
        }
    }

    // Keeps what is on the map being left, under its id, for when the table comes back to it. A map
    // never set up and with nothing on it is not kept: coming back to it must seed its doors and
    // framing as a first visit would. Caller holds gate.
    private void SetAside()
    {
        bool seeded = string.Equals(seededMapId, currentMapId, StringComparison.Ordinal);
        bool anything = revealedRegionIds.Count > 0 || revealedFeatureIds.Count > 0
            || fogCells.Count > 0 || openDoorIds.Count > 0 || party.IsPlaced;
        if (currentMapId.Length == 0 || !(seeded || anything))
        {
            return;
        }

        otherMaps[currentMapId] = new MapRevealSnapshot
        {
            MapId = currentMapId,
            RevealedRegionIds = revealedRegionIds.ToArray(),
            RevealedFeatureIds = revealedFeatureIds.ToArray(),
            OpenDoorIds = openDoorIds.ToArray(),
            FogCells = ToFogCells(fogCells),
            PlayerViewport = ToViewport(playerViewport),
            PartyToken = ToSnapshot(party),
        };
    }

    // Puts back what was left on the map now in play, if anything was, and counts that map as seeded:
    // its doors and framing are the ones the table left there, not the map's defaults. Caller holds
    // gate.
    private void BringBack()
    {
        if (!otherMaps.Remove(currentMapId, out MapRevealSnapshot left))
        {
            return;
        }

        Replace(revealedRegionIds, left.RevealedRegionIds);
        Replace(revealedFeatureIds, left.RevealedFeatureIds);
        Replace(openDoorIds, left.OpenDoorIds);
        ReplaceCells(left.FogCells);
        playerViewport = ToBounds(left.PlayerViewport);
        party = ToToken(left.PartyToken);
        seededMapId = currentMapId;
    }

    // Keeps a party reveal for Ctrl+Z, keeping only the newest PartyUndoDepth. Caller holds gate.
    private void AddPartyReveal(PartyReveal reveal)
    {
        partyReveals.Add(reveal);
        if (partyReveals.Count > PartyUndoDepth)
        {
            partyReveals.RemoveAt(0);
        }
    }

    // Reveals the cells not yet revealed and records them on the reveal, so taking it back hides those
    // and leaves alone what was showing already. Returns how many were new. Caller holds gate.
    private int RevealInto(PartyReveal reveal, IEnumerable<(int Col, int Row)> seen)
    {
        (int Col, int Row)[] fresh = (seen ?? Array.Empty<(int, int)>())
            .Where(cell => !fogCells.Contains(cell))
            .Distinct()
            .ToArray();
        fogCells.UnionWith(fresh);
        reveal.Added.UnionWith(fresh);
        return fresh.Length;
    }

    // Forgets the one-step clear-reveals undo snapshot. Caller holds gate.
    private void DropClearSnapshot()
    {
        clearedRegionIds.Clear();
        clearedFeatureIds.Clear();
        clearedCells.Clear();
        hasClearSnapshot = false;
    }

    // Clears every reveal set; returns true when anything was actually removed. Caller holds gate.
    private bool ClearRevealState()
    {
        bool changed = revealedRegionIds.Count > 0 || revealedFeatureIds.Count > 0 || fogCells.Count > 0;
        revealedRegionIds.Clear();
        revealedFeatureIds.Clear();
        fogCells.Clear();
        return changed;
    }

    // One thing Ctrl+Z takes back: where the token stood before, the cells it revealed that were not
    // showing already, and the door it came from as that door stood.
    private sealed class PartyReveal
    {
        public PartyReveal(PartyToken before, DoorState door)
        {
            Before = before;
            Door = door;
        }

        public PartyToken Before { get; }

        public DoorState Door { get; }

        public HashSet<(int Col, int Row)> Added { get; } = new HashSet<(int Col, int Row)>();
    }
}
