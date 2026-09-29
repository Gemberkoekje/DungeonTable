using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Tests.Battles;

/// <summary>
/// A cross-reference resolver driven by a phrase list, standing in for the real one so a caller can
/// be tested against known prose without writing link markup into it. The <em>first</em> occurrence
/// of each phrase in a block is a reference, later ones are not.
/// </summary>
internal sealed class ScriptedCrossRefResolver : ICrossRefResolver
{
    private readonly List<Entry> phrases = new List<Entry>();

    /// <summary>Registers a phrase that resolves to a target whenever it appears in prose.</summary>
    /// <param name="phrase">The literal phrase to look for, matched case-insensitively.</param>
    /// <param name="nodeId">The node id the phrase points at.</param>
    /// <param name="kind">What kind of thing the target is.</param>
    /// <returns>This resolver, so registrations chain.</returns>
    public ScriptedCrossRefResolver Add(string phrase, string nodeId, CrossRefKind kind)
    {
        phrases.Add(new Entry(phrase, nodeId, kind));
        return this;
    }

    public IReadOnlyList<CrossRef> Resolve(string text) => Resolve(text, string.Empty);

    // The scripted phrase list is level-independent, so the floor makes no difference here: a
    // creature is the same creature on every floor.
    public IReadOnlyList<CrossRef> Resolve(string text, string levelNodeId)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<CrossRef>();
        }

        return phrases
            .Select(entry => new { entry, at = text.IndexOf(entry.Phrase, StringComparison.OrdinalIgnoreCase) })
            .Where(found => found.at >= 0)
            .Select(found => new CrossRef
            {
                Text = text.Substring(found.at, found.entry.Phrase.Length),
                Kind = found.entry.Kind,
                TargetId = found.entry.NodeId,
                Start = found.at,
                Length = found.entry.Phrase.Length,
            })
            .OrderBy(reference => reference.Start)
            .ToArray();
    }

    private readonly record struct Entry(string Phrase, string NodeId, CrossRefKind Kind);
}
