using System.Collections.Concurrent;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Links;

/// <summary>
/// Resolves the links authors write into dossier prose (<c>[[bandit|human bandits]]</c>, see
/// <see cref="LinkMarkup"/>) against the loaded content. Immutable after construction, with results
/// cached per text and floor, so it is safe to share as a singleton.
/// </summary>
/// <remarks>
/// <para>
/// Only what an author marked is a link. Nothing is guessed from the prose around the links: a
/// mention is not a reference until someone says so, and a guessed link ("disguised as vampires"
/// opening the vampire stat block) is worse than none.
/// </para>
/// <para>
/// Every link yields a span, resolved or not. A broken one comes back as
/// <see cref="CrossRefKind.Unresolved"/> carrying its words, so the page shows "human bandits" rather
/// than the brackets, and the DM can still see that the link goes nowhere. One to a floor nobody has
/// authored yet comes back as <see cref="CrossRefKind.Pending"/>, with no target to open.
/// </para>
/// </remarks>
public sealed class MarkupCrossRefResolver : ICrossRefResolver
{
    private readonly LinkTargets targets;
    private readonly ConcurrentDictionary<(string Level, string Text), IReadOnlyList<CrossRef>> cache = new();

    /// <summary>Creates the resolver.</summary>
    /// <param name="targets">What a link's target can name.</param>
    public MarkupCrossRefResolver(LinkTargets targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        this.targets = targets;
    }

    /// <inheritdoc />
    public IReadOnlyList<CrossRef> Resolve(string text) => Resolve(text, string.Empty);

    /// <inheritdoc />
    public IReadOnlyList<CrossRef> Resolve(string text, string levelNodeId)
    {
        // Most prose carries no markup at all, and there is nothing to find in it.
        if (!LinkMarkup.Contains(text))
        {
            return Array.Empty<CrossRef>();
        }

        return cache.GetOrAdd((levelNodeId ?? string.Empty, text), static (key, self) => self.Scan(key.Text, key.Level), this);
    }

    private CrossRef[] Scan(string text, string level) =>
        LinkMarkup.Parse(text)
            .Select(link => ToCrossRef(text, link, targets.Resolve(link.Target, level)))
            .ToArray();

    private static CrossRef ToCrossRef(string text, MarkedLink link, Result<LinkTarget> resolved)
    {
        if (!resolved.IsValid)
        {
            return new CrossRef
            {
                Text = LinkMarkup.Words(text, link),
                Kind = CrossRefKind.Unresolved,
                TargetId = string.Empty,
                Start = link.Start,
                Length = link.Length,
            };
        }

        LinkTarget target = resolved.Value;
        return new CrossRef
        {
            Text = link.Shown.Length > 0 ? link.Shown : target.DisplayName,
            Kind = target.Kind,
            TargetId = target.TargetId,
            Start = link.Start,
            Length = link.Length,
        };
    }
}
