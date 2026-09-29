using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// Splits a block of dossier prose into alternating plain-text and cross-reference segments, so the
/// DM panel can render the reference spans as in-place links while leaving the rest of the prose as
/// text. The <see cref="CrossRef"/> spans come from <c>ICrossRefResolver.Resolve</c> for the same
/// body, so they are ascending and non-overlapping; stray offsets are skipped defensively.
/// </summary>
/// <remarks>
/// A span's words come from the reference rather than the body: for an authored
/// <c>[[bandit|human bandits]]</c> the body holds the markup and the reference holds "human bandits".
/// </remarks>
public static class CrossRefText
{
    /// <summary>A run of a block body: either plain text or a single cross-reference.</summary>
    /// <param name="Text">The words the run shows.</param>
    /// <param name="IsLink">True when the run is a cross-reference to render as a link.</param>
    /// <param name="Kind">
    /// The reference kind (styling / target view); <see cref="CrossRefKind.Unresolved"/> for an
    /// authored link that goes nowhere, which renders as marked plain text rather than a link, and
    /// <see cref="CrossRefKind.Pending"/> for one to a floor that is not authored yet, which renders
    /// as muted text.
    /// </param>
    /// <param name="TargetId">The id to open, when <paramref name="IsLink"/>.</param>
    /// <param name="Source">The run as written in the body, markup and all.</param>
    public readonly record struct Segment(string Text, bool IsLink, CrossRefKind Kind, string TargetId, string Source);

    /// <summary>
    /// Interleaves <paramref name="body"/> with its resolved cross-references, ordered start to end.
    /// </summary>
    /// <param name="body">The block body.</param>
    /// <param name="refs">The cross-references found in the body (ascending, non-overlapping).</param>
    /// <returns>The plain / link segments covering the whole body in order.</returns>
    public static IReadOnlyList<Segment> Split(string body, IReadOnlyList<CrossRef> refs)
    {
        if (string.IsNullOrEmpty(body))
        {
            return Array.Empty<Segment>();
        }

        if (refs is null || refs.Count == 0)
        {
            return new[] { Plain(body) };
        }

        var segments = new List<Segment>(refs.Count * 2 + 1);
        int pos = 0;
        foreach (CrossRef reference in refs)
        {
            // Skip a span that overlaps a previous one or falls outside the body (stale offsets).
            if (reference.Start < pos || reference.Length <= 0 || reference.Start + reference.Length > body.Length)
            {
                continue;
            }

            if (reference.Start > pos)
            {
                segments.Add(Plain(body.Substring(pos, reference.Start - pos)));
            }

            string source = body.Substring(reference.Start, reference.Length);
            segments.Add(new Segment(
                string.IsNullOrEmpty(reference.Text) ? source : reference.Text,
                reference.Kind is not (CrossRefKind.Unresolved or CrossRefKind.Pending),
                reference.Kind,
                reference.TargetId,
                source));
            pos = reference.Start + reference.Length;
        }

        if (pos < body.Length)
        {
            segments.Add(Plain(body.Substring(pos)));
        }

        return segments;
    }

    /// <summary>
    /// The body as the DM reads it: every reference replaced by its words, so an authored link shows
    /// what it says rather than its markup. For places that show prose without links.
    /// </summary>
    /// <param name="body">The block body.</param>
    /// <param name="refs">The cross-references found in the body.</param>
    /// <returns>The readable text.</returns>
    public static string Display(string body, IReadOnlyList<CrossRef> refs) =>
        string.Concat(Split(body, refs).Select(segment => segment.Text));

    private static Segment Plain(string text) => new Segment(text, false, CrossRefKind.None, string.Empty, text);
}
