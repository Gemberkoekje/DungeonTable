using System;
using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Art;

namespace DungeonTable.Web.Services;

/// <summary>
/// The pictures the DM can put on the projector from a mention: which ones show the thing a link names
/// (<see cref="IPictureLinks"/>), whether one of them is on the projector now, and the one-click toggle
/// that puts it up or takes it down (<see cref="SessionState"/>). Behind every picture button on the
/// DM screen (<c>PictureButton</c>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Changed"/> fires only when what a button shows could be different: another picture went up
/// or down, or the content or the catalogue changed. The shared state changes many times a second while
/// the DM drags the party token, and a button needs none of those, so this listens to the session once
/// and passes on only a change of the picture on show. That keeps the buttons from rendering, and the
/// DM's line from carrying their renders, on every step.
/// </para>
/// <para>
/// A singleton, like the two it reads, so every DM window agrees on what is showing.
/// </para>
/// </remarks>
public sealed class ProjectorPictures
{
    private readonly IPictureLinks links;
    private readonly SessionState session;
    private readonly object gate = new object();
    private string shownSeen;

    /// <summary>Creates the service and starts following the picture on the projector.</summary>
    /// <param name="links">Which pictures show what.</param>
    /// <param name="session">The shared state that holds the picture on the projector.</param>
    public ProjectorPictures(IPictureLinks links, SessionState session)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(session);
        this.links = links;
        this.session = session;
        shownSeen = Showing();

        // Both live as long as this singleton, so neither subscription is ever dropped.
        session.Changed += OnSessionChanged;
        links.Changed += OnLinksChanged;
    }

    /// <summary>
    /// Raised after the picture on the projector changed, or which pictures show what. Raised on the
    /// thread that made the change; a component marshals onto its own dispatcher.
    /// </summary>
    public event Action Changed;

    /// <summary>The pictures that show a thing, best first.</summary>
    /// <param name="targetId">The id a resolved link carries.</param>
    /// <returns>The pictures (possibly empty).</returns>
    public IReadOnlyList<ArtImage> Of(string targetId) =>
        string.IsNullOrEmpty(targetId) ? Array.Empty<ArtImage>() : links.PicturesOf(targetId);

    /// <summary>True when at least one picture shows the thing.</summary>
    /// <param name="targetId">The id a resolved link carries.</param>
    /// <returns>Whether a picture button belongs beside it.</returns>
    public bool Has(string targetId) => Of(targetId).Count > 0;

    /// <summary>True when this picture is on the projector now: staged and not hidden.</summary>
    /// <param name="imageId">The picture's catalogue id.</param>
    /// <returns>Whether the players see it.</returns>
    public bool IsShowing(string imageId) =>
        !string.IsNullOrEmpty(imageId) && string.Equals(Showing(), imageId, StringComparison.Ordinal);

    /// <summary>
    /// Puts a picture on the projector, or takes it down when it is the one showing. The second click is
    /// what a DM reaches for when the scene moves on, without going to the bar or the Art tab.
    /// </summary>
    /// <param name="imageId">The picture's catalogue id; a blank id does nothing.</param>
    public void Toggle(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
        {
            return;
        }

        if (IsShowing(imageId))
        {
            session.HideArt();
        }
        else
        {
            session.ShowArt(imageId);
        }
    }

    // The picture the players see now; empty when none is up.
    private string Showing() => session.ShownArtVisible ? session.ShownArtImageId : string.Empty;

    private void OnSessionChanged()
    {
        string now = Showing();
        bool changed;
        lock (gate)
        {
            changed = !string.Equals(now, shownSeen, StringComparison.Ordinal);
            shownSeen = now;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private void OnLinksChanged() => Changed?.Invoke();
}
