using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests;

/// <summary>
/// Every quest document: its quests can be shown and are offered the way they say, their
/// prerequisites and givers name what exists, and their beats can be ticked and lead where they say.
/// </summary>
public sealed class QuestTests
{
    public static TheoryData<string> QuestDocuments => ContentDocuments.Names(DocumentKind.Quests);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(QuestDocuments))]
    public void Every_quest_can_be_shown(string document)
    {
        QuestLog log = ContentDocuments.Loaded<QuestLog>(document);

        Report.None(QuestAudit.Unshowable(log), $"Quests in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(QuestDocuments))]
    public void No_quest_id_is_written_twice(string document)
    {
        QuestLog log = ContentDocuments.Loaded<QuestLog>(document);

        Report.None(
            QuestAudit.WrittenTwice(document, log, ContentDocuments.LoadedOf<QuestLog>(DocumentKind.Quests)),
            $"Quest ids in {document} that are written twice");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(QuestDocuments))]
    public void Every_prerequisite_can_be_judged(string document)
    {
        QuestLog log = ContentDocuments.Loaded<QuestLog>(document);
        Result<QuestLog> all = ContentRoot.Dossiers.GetQuests();
        IReadOnlySet<string> ids = all.IsValid
            ? all.Value.Quests.Select(quest => quest.Id).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        Report.None(QuestAudit.BadPrerequisites(log, ids), $"Prerequisites in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(QuestDocuments))]
    public void Every_beat_can_be_ticked(string document)
    {
        QuestLog log = ContentDocuments.Loaded<QuestLog>(document);

        Report.None(QuestAudit.BadBeats(log), $"Beats in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(QuestDocuments))]
    public void Every_giver_that_is_written_opens_a_card(string document)
    {
        QuestLog log = ContentDocuments.Loaded<QuestLog>(document);

        Report.None(QuestAudit.BadGivers(log, Opens, NpcIds()), $"Givers in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(QuestDocuments))]
    public void Every_beat_leads_where_it_says(string document)
    {
        QuestLog log = ContentDocuments.Loaded<QuestLog>(document);

        Report.None(QuestAudit.BadTargets(log, ContentRoot.Targets), $"Beat destinations in {document}");
    }

    // What the DM screen opens a card for, in the order it tries them: an NPC, a player character, then
    // anything with a reference card.
    private static bool Opens(string id)
    {
        Result<PartyDossier> party = ContentRoot.Dossiers.GetParty();
        return NpcIds().Contains(id)
            || (party.IsValid && party.Value.Characters.Any(character => string.Equals(character.Id, id, StringComparison.Ordinal)))
            || ContentRoot.Projection.GetReferenceCard(id).IsValid;
    }

    private static HashSet<string> NpcIds()
    {
        Result<NpcRoster> roster = ContentRoot.Dossiers.GetNpcs();
        return roster.IsValid
            ? roster.Value.Npcs.Select(npc => npc.Id).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }
}
