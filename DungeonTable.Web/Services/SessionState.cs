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
/// cells the DM has painted), and the picture laid over the map on the projector. Raises
/// <see cref="Changed"/> so both views re-render when any of it updates. Registered as a singleton;
/// mutated by the DM circuit and observed by the player.
/// </summary>
/// <remarks>
/// <see cref="Capture"/> and <see cref="Restore"/> are how this outlives the process: the
/// persistence coordinator captures a <see cref="RevealSnapshot"/> whenever <see cref="Changed"/>
/// fires and restores one at startup, before any circuit connects.
/// </remarks>
public sealed class SessionState
{
    // What the player screen is assumed to be until it reports its own aspect ratio.
    private const double DefaultAspect = 16.0 / 9.0;

    // The DM and player circuits run on different threads; guard the shared fields so each sees
    // the other's writes. Changed is always raised outside the lock to avoid reentrancy.
    private readonly object gate = new object();
    private readonly HashSet<string> revealedRegionIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> revealedFeatureIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<(int Col, int Row)> fogCells = new HashSet<(int, int)>();
    // Doors the DM has opened this session. Live table state (not persisted): seeded from each
    // door's DefaultOpen when a map loads and toggled during play. Cleared on a map switch.
    private readonly HashSet<string> openDoorIds = new HashSet<string>(StringComparer.Ordinal);
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
    private MapBounds playerViewport;
    private double playerAspect = DefaultAspect;
    private string currentMapId = string.Empty;
    // The map whose default-open doors and starting viewport have already been seeded. It lives here
    // rather than on the (scoped) DM workspace because seeding is a property of the *table*, not of a
    // browser tab: a refresh, a second DM window or an app restart would otherwise re-seed and throw
    // away the framing the DM had set and every door they had opened.
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
    /// Records which map the reveal state applies to. Switching to a different map clears all
    /// reveals — each map has its own world coordinates, regions and features.
    /// </summary>
    /// <param name="mapId">The id of the newly loaded map.</param>
    public void SetCurrentMap(string mapId)
    {
        bool changed = false;
        lock (gate)
        {
            if (!string.Equals(mapId, currentMapId, StringComparison.Ordinal))
            {
                currentMapId = mapId;
                ClearRevealState();
                openDoorIds.Clear();
                // A ping is a point in the old map's world coordinates; on another map it would
                // land somewhere arbitrary.
                ping = PingMarker.None;
                // The undo snapshot belongs to the map that was cleared; another map's reveal ids
                // mean nothing here, so drop it rather than let Undo restore foreign ids.
                DropClearSnapshot();
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
                FogCells = fogCells
                    .Select(cell => new FogCell { Col = cell.Col, Row = cell.Row })
                    .ToArray(),
                PlayerViewport = new ViewportSnapshot
                {
                    MinX = playerViewport.MinX,
                    MinY = playerViewport.MinY,
                    MaxX = playerViewport.MaxX,
                    MaxY = playerViewport.MaxY,
                },
                PlayerAspect = playerAspect,
                ShownArt = new ShownArtSnapshot
                {
                    ImageId = shownArtImageId,
                    Visible = shownArtVisible,
                    Size = shownArtSize,
                    TrayImageIds = trayImageIds.ToArray(),
                },
            };
        }
    }

    /// <summary>
    /// Puts a stored reveal state back, replacing whatever is held. Called once at startup, before
    /// any circuit connects, so the first DM and player views render the table as it was left.
    /// </summary>
    /// <remarks>
    /// The clear-reveals undo snapshot is dropped rather than restored: it is a "take that back"
    /// affordance measured in seconds, and offering to undo a clear from a previous evening would be
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

            fogCells.Clear();
            foreach (FogCell cell in snapshot.FogCells ?? Array.Empty<FogCell>())
            {
                fogCells.Add((cell.Col, cell.Row));
            }

            ShownArtSnapshot art = snapshot.ShownArt ?? new ShownArtSnapshot();
            shownArtImageId = art.ImageId ?? string.Empty;
            shownArtVisible = art.Visible && shownArtImageId.Length > 0;
            shownArtSize = art.Size == ArtSize.None ? ArtSize.Medium : art.Size;
            trayImageIds.Clear();
            trayImageIds.AddRange((art.TrayImageIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal));

            ViewportSnapshot view = snapshot.PlayerViewport ?? new ViewportSnapshot();
            playerViewport = new MapBounds(view.MinX, view.MinY, view.MaxX, view.MaxY);
            playerAspect = IsUsableAspect(snapshot.PlayerAspect)
                ? ClampAspect(snapshot.PlayerAspect)
                : DefaultAspect;

            DropClearSnapshot();
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
}
