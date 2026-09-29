using System.Collections.Generic;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for the DM's own prep: every session plan's scenes say who is on and run in order, and
/// every story chapter has its cast and says where each of them stands. A <c>source</c> is not required
/// of either: both cards show one only when it is written.
/// </summary>
internal static class PrepAudit
{
    /// <summary>
    /// Every session plan missing what its card shows, and every scene that does not say who is on and
    /// how long to give it, which is the question a running order exists to answer.
    /// </summary>
    /// <param name="log">The session prep.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> IncompleteSessions(SessionLog log)
    {
        var faults = new List<string>();
        for (int i = 0; i < log.Sessions.Count; i++)
        {
            SessionPlan plan = log.Sessions[i];
            string which = $"The session {Check.Named(plan.Id, $"sessions[{i}]")}";
            Check.Written(faults, plan.Id, $"{which} has no id.");
            Check.Written(faults, plan.Title, $"{which} has no title.");
            Check.Written(faults, plan.Summary, $"{which} has no summary.");
            for (int j = 0; j < plan.Scenes.Count; j++)
            {
                SessionScene scene = plan.Scenes[j];
                string where = $"{which}, scene {Check.Named(scene.Id, $"scenes[{j}]")},";
                Check.Written(faults, scene.Id, $"{where} has no id.");
                Check.Written(faults, scene.Title, $"{where} has no title.");
                Check.Written(faults, scene.Cast, $"{where} does not say who is on.");
                Check.Written(faults, scene.When, $"{where} gives no timing.");
                if (scene.Beats.Count == 0)
                {
                    faults.Add($"{where} has no beats.");
                }
            }
        }

        faults.AddRange(Check.Repeated(log.Sessions.Select(plan => plan.Id))
            .Select(id => $"Two sessions have the id '{id}'."));
        return faults;
    }

    /// <summary>
    /// Every session whose scenes are not numbered 0, 1, 2… in the order they are written, or share an
    /// id. The scenes are shown in order and keyed by id.
    /// </summary>
    /// <param name="log">The session prep.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> MisorderedScenes(SessionLog log)
    {
        var faults = new List<string>();
        foreach (SessionPlan plan in log.Sessions)
        {
            int[] ordinals = plan.Scenes.Select(scene => scene.Ordinal).ToArray();
            if (!ordinals.SequenceEqual(Enumerable.Range(0, ordinals.Length)))
            {
                faults.Add($"The session '{plan.Id}' numbers its scenes {string.Join(", ", ordinals)}; write them 0 to {ordinals.Length - 1}, in order.");
            }

            faults.AddRange(Check.Repeated(plan.Scenes.Select(scene => scene.Id))
                .Select(id => $"The session '{plan.Id}' has two scenes with the id '{id}'."));
        }

        return faults;
    }

    /// <summary>
    /// Every story chapter missing what its card shows: a title, a summary, sections, and a cast whose
    /// every row says where that person stands.
    /// </summary>
    /// <param name="story">The story so far.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> IncompleteChapters(StoryLibrary story)
    {
        var faults = new List<string>();
        for (int i = 0; i < story.Chapters.Count; i++)
        {
            StoryDossier chapter = story.Chapters[i];
            string which = string.IsNullOrWhiteSpace(chapter.Title) ? $"chapters[{i}]" : $"The chapter '{chapter.Title}'";
            Check.Written(faults, chapter.Title, $"{which} has no title.");
            Check.Written(faults, chapter.Summary, $"{which} has no summary.");
            if (chapter.Sections.Count == 0)
            {
                faults.Add($"{which} has no sections.");
            }

            if (chapter.Cast.Count == 0)
            {
                faults.Add($"{which} has no cast.");
            }

            faults.AddRange(chapter.Cast.SelectMany((member, j) => PeopleAudit.CastRowFaults(member, $"{which}, cast[{j}],")));
        }

        return faults;
    }
}
