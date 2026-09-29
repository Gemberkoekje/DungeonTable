using System.Collections.Generic;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Links;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for a quest document: every quest can be shown and is offered the way it says, every
/// prerequisite and giver names something that exists, and every beat can be ticked and leads where it
/// says. Beats are what progress is recorded against, so their ids matter as much as the quests'.
/// </summary>
internal static class QuestAudit
{
    /// <summary>
    /// Every quest the quest log could not show properly: no id (the store drops it), no title, hook or
    /// beats, no word on when it is offered, or prerequisites that contradict that.
    /// </summary>
    /// <param name="log">A quest document, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unshowable(QuestLog log)
    {
        var faults = new List<string>();
        for (int i = 0; i < log.Quests.Count; i++)
        {
            Quest quest = log.Quests[i];
            string which = $"The quest {Check.Named(quest.Id, $"quests[{i}] ('{quest.Title}')")}";
            Check.Written(faults, quest.Id, $"{which} has no id, so the app drops it.");
            Check.Written(faults, quest.Title, $"{which} has no title.");
            Check.Written(faults, quest.Hook, $"{which} has no hook.");
            faults.AddRange(new[]
                {
                    (Bad: quest.Beats.Count == 0, Fault: $"{which} has no beats, and a quest is ticked off beat by beat."),
                    (Bad: quest.Availability == QuestAvailability.None, Fault: $"{which} says nothing of when it is offered: write starting, future or side."),
                    (Bad: quest.Availability == QuestAvailability.Starting && quest.Prerequisites.Count > 0, Fault: $"{which} is offered from the start, but has prerequisites."),
                    (Bad: quest.Availability == QuestAvailability.Future && quest.Prerequisites.Count == 0, Fault: $"{which} is offered once its prerequisites are met, but has none."),
                }
                .Where(check => check.Bad)
                .Select(check => check.Fault));
        }

        return faults;
    }

    /// <summary>
    /// Every quest id this document repeats, or shares with another quest document. The store keeps the
    /// last of them, so the others are silently gone.
    /// </summary>
    /// <param name="fileName">This document's name.</param>
    /// <param name="log">This document, as written.</param>
    /// <param name="others">The other quest documents, by name.</param>
    /// <returns>One sentence per id (empty when there are none).</returns>
    internal static IReadOnlyList<string> WrittenTwice(string fileName, QuestLog log, IEnumerable<(string File, QuestLog Log)> others)
    {
        string[] ids = log.Quests.Select(quest => quest.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        var faults = Check.Repeated(ids)
            .Select(id => $"The quest id '{id}' is written more than once in {fileName}: the app keeps only the last.")
            .ToList();

        foreach ((string file, QuestLog other) in others.Where(other => !string.Equals(other.File, fileName, StringComparison.Ordinal)))
        {
            var theirs = new HashSet<string>(other.Quests.Select(quest => quest.Id), StringComparer.Ordinal);
            faults.AddRange(ids.Distinct(StringComparer.Ordinal).Where(theirs.Contains)
                .Select(id => $"The quest id '{id}' is also in {file}: the app keeps only one of them."));
        }

        return faults;
    }

    /// <summary>
    /// Every prerequisite that cannot be judged met or unmet: no kind or text, a quest to complete that
    /// no quest document has, or a character level no character reaches.
    /// </summary>
    /// <param name="log">A quest document, as written.</param>
    /// <param name="questIds">Every quest id the loaded content has.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadPrerequisites(QuestLog log, IReadOnlySet<string> questIds)
    {
        var faults = new List<string>();
        foreach (Quest quest in log.Quests)
        {
            for (int i = 0; i < quest.Prerequisites.Count; i++)
            {
                QuestPrerequisite prerequisite = quest.Prerequisites[i];
                string where = $"The quest '{quest.Id}', prerequisites[{i}],";
                Check.Written(faults, prerequisite.Text, $"{where} has no text to show.");
                faults.AddRange(prerequisite.Kind switch
                {
                    QuestPrerequisiteKind.QuestComplete when prerequisite.AnyOfQuestIds.Count == 0 =>
                        new[] { $"{where} names no quest to complete." },
                    QuestPrerequisiteKind.QuestComplete => prerequisite.AnyOfQuestIds
                        .Where(id => !questIds.Contains(id ?? string.Empty))
                        .Select(id => $"{where} waits for the quest '{id}', which no quest document has."),
                    QuestPrerequisiteKind.CharacterLevel when prerequisite.CharacterLevel is < 1 or > 20 =>
                        new[] { $"{where} waits for character level {prerequisite.CharacterLevel}; a character is level 1 to 20." },
                    QuestPrerequisiteKind.CharacterLevel => Array.Empty<string>(),
                    _ => new[] { $"{where} says nothing of what it waits for: write questComplete or characterLevel." },
                });
            }
        }

        return faults;
    }

    /// <summary>
    /// Every beat that cannot be ticked: no id or summary, an id another beat of the quest shares (both
    /// would tick at once), or ordinals that do not run 1, 2, 3… in the order written.
    /// </summary>
    /// <param name="log">A quest document, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadBeats(QuestLog log)
    {
        var faults = new List<string>();
        foreach (Quest quest in log.Quests)
        {
            for (int i = 0; i < quest.Beats.Count; i++)
            {
                QuestBeat beat = quest.Beats[i];
                Check.Written(faults, beat.Id, $"The quest '{quest.Id}', beats[{i}], has no id, and progress is recorded against it.");
                Check.Written(faults, beat.Summary, $"The quest '{quest.Id}', beat {Check.Named(beat.Id, $"beats[{i}]")}, has no summary.");
            }

            faults.AddRange(Check.Repeated(quest.Beats.Select(beat => beat.Id))
                .Select(id => $"The quest '{quest.Id}' has two beats with the id '{id}': ticking one would tick both."));

            int[] ordinals = quest.Beats.Select(beat => beat.Ordinal).ToArray();
            if (!ordinals.SequenceEqual(Enumerable.Range(1, ordinals.Length)))
            {
                faults.Add($"The quest '{quest.Id}' numbers its beats {string.Join(", ", ordinals)}; write them 1 to {ordinals.Length}, in order.");
            }
        }

        return faults;
    }

    /// <summary>
    /// Every giver the quest log cannot open: a <c>giverNodeId</c> that opens no card, and a giver
    /// labelled as someone on the NPC roster who does not open that NPC's card. A giver may name no id
    /// at all; the label is then plain text.
    /// </summary>
    /// <param name="log">A quest document, as written.</param>
    /// <param name="opens">Whether the app opens a card for an id: an NPC, a player character, or anything with a reference card.</param>
    /// <param name="npcIds">The NPC roster's ids.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadGivers(QuestLog log, Func<string, bool> opens, IReadOnlySet<string> npcIds)
    {
        var faults = new List<string>();
        foreach (Quest quest in log.Quests)
        {
            string giver = (quest.GiverNodeId ?? string.Empty).Trim();
            if (giver.Length > 0 && !opens(giver))
            {
                faults.Add($"The quest '{quest.Id}' is given by '{giver}', which opens no card: no NPC, player character or reference card has that id.");
            }

            string named = LinkTargets.Slug(quest.GiverLabel);
            if (npcIds.Contains(named) && !string.Equals(giver, named, StringComparison.Ordinal))
            {
                faults.Add($"The quest '{quest.Id}' is given by '{quest.GiverLabel}', who is the NPC '{named}', but its giverNodeId is '{giver}'.");
            }
        }

        return faults;
    }

    /// <summary>
    /// Every beat whose destination is wrong. An area target on a written floor has to open the area
    /// that floor prints under its key. One on a floor not written yet cannot be checked, so it has to
    /// carry the id that area will have (<see cref="AreaIds"/>), or its chip never lights up. Any other
    /// kind of target jumps nowhere, so it carries no id.
    /// </summary>
    /// <param name="log">A quest document, as written.</param>
    /// <param name="targets">What a link can name in the loaded content.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadTargets(QuestLog log, LinkTargets targets)
    {
        var faults = new List<string>();
        foreach (Quest quest in log.Quests)
        {
            foreach (QuestBeat beat in quest.Beats.Where(beat => beat.Target is not null))
            {
                string where = $"The quest '{quest.Id}', beat '{beat.Id}',";
                faults.AddRange(beat.Target.Kind == QuestTargetKind.Area
                    ? AreaTargetFaults(beat.Target, where, targets)
                    : OtherTargetFaults(beat.Target, where));
            }
        }

        return faults;
    }

    private static string[] AreaTargetFaults(QuestTarget target, string where, LinkTargets targets)
    {
        string key = (target.AreaKey ?? string.Empty).Trim();
        string nodeId = target.NodeId ?? string.Empty;
        if (target.Level <= 0 || key.Length == 0 || nodeId.Length == 0)
        {
            return new[] { $"{where} targets an area, and so needs a level, an areaKey and the area's nodeId." };
        }

        Result<LinkTarget> found = targets.Resolve($"L{target.Level} area {key}", string.Empty);
        if (!found.IsValid)
        {
            return new[] { $"{where} targets level {target.Level}, area {key}: {Check.Messages(found)}" };
        }

        if (found.Value.Kind != CrossRefKind.Pending)
        {
            return string.Equals(nodeId, found.Value.TargetId, StringComparison.Ordinal)
                ? Array.Empty<string>()
                : new[] { $"{where} targets level {target.Level}, area {key}, as '{nodeId}', but that area is '{found.Value.TargetId}'." };
        }

        string future = AreaIds.For(target.Level, key);
        return string.Equals(nodeId, future, StringComparison.Ordinal)
            ? Array.Empty<string>()
            : new[] { $"{where} targets level {target.Level}, which is not written yet, as '{nodeId}'. Write the id its area {key} will have, '{future}', or the chip will not light up when the level lands." };
    }

    private static string[] OtherTargetFaults(QuestTarget target, string where) =>
        string.IsNullOrWhiteSpace(target.NodeId)
            ? Array.Empty<string>()
            : new[] { $"{where} is a {target.Kind} target with the nodeId '{target.NodeId}', but only an area target jumps anywhere." };
}
