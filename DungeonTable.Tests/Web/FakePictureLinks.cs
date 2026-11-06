using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Art;

namespace DungeonTable.Tests.Web;

/// <summary>Which pictures show what, held in memory: each target id with its pictures, best first.</summary>
internal sealed class FakePictureLinks : IPictureLinks
{
    /// <inheritdoc />
    public event Action Changed;

    /// <summary>The pictures of each target id, best first.</summary>
    public Dictionary<string, ArtImage[]> Pictures { get; } = new Dictionary<string, ArtImage[]>(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<ArtImage> PicturesOf(string targetId) =>
        Pictures.TryGetValue(targetId ?? string.Empty, out ArtImage[] found) ? found : Array.Empty<ArtImage>();

    /// <summary>Says the answers changed, as a content reload or a catalogue edit would.</summary>
    public void Announce() => Changed?.Invoke();
}
