using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Stats;

namespace DungeonTable.ContentTests;

/// <summary>
/// Every <c>monsters*.json</c> and <c>spells*.json</c>, each checked as written: its entries have ids of
/// their own, its monsters have numbers a monster can have and cast spells the library has, and every
/// source it cites is a book the book index gives a title.
/// </summary>
public sealed class StatBlockTests
{
    public static TheoryData<string> StatDocuments => ContentDocuments.Names(DocumentKind.Monsters, DocumentKind.Spells);

    public static TheoryData<string> MonsterDocuments => ContentDocuments.Names(DocumentKind.Monsters);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(StatDocuments))]
    public void Every_entry_has_a_node_id_of_its_own(string document)
    {
        Report.None(StatAudit.Unidentified(Entries(document).Select(entry => (entry.NodeId, entry.Name)).ToList()), $"Entries in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(MonsterDocuments))]
    public void Every_monster_has_numbers_a_monster_can_have(string document)
    {
        Report.None(StatAudit.Implausible(Monsters(document)), $"Monsters in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(MonsterDocuments))]
    public void Every_monster_has_a_legal_challenge_rating_and_its_proficiency_bonus(string document)
    {
        Report.None(StatAudit.WrongChallenge(Monsters(document)), $"Challenge ratings in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(MonsterDocuments))]
    public void No_stat_block_entry_is_empty(string document)
    {
        Report.None(StatAudit.EmptyEntries(Monsters(document)), $"Empty entries in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(MonsterDocuments))]
    public void Every_spell_a_monster_casts_is_in_the_library(string document)
    {
        Report.None(StatAudit.UnextractedSpells(Monsters(document), ContentRoot.Stats), $"Spells cast in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(StatDocuments))]
    public void Every_source_is_a_book_the_book_index_titles(string document)
    {
        Report.None(
            StatAudit.UntitledSources(Entries(document).Select(entry => (entry.NodeId, entry.Source)), ContentRoot.Books),
            $"Sources in {document}");
    }

    // A document's entries, monsters or spells, as the fields both have.
    private static IEnumerable<(string NodeId, string Name, string Source)> Entries(string document) =>
        ContentDocuments.Named(document).Kind == DocumentKind.Monsters
            ? Monsters(document).Select(monster => (monster.NodeId, monster.Name, monster.Source))
            : Spells(document).Select(spell => (spell.NodeId, spell.Name, spell.Source));

    // A document's monsters and spells as the library reads them: a null entry, which the library skips
    // and the null-entry rule reports, is left out here too rather than failing every rule on it.
    private static List<StatBlock> Monsters(string document) =>
        ContentDocuments.Loaded<List<StatBlock>>(document).Where(monster => monster is not null).ToList();

    private static List<SpellEntry> Spells(string document) =>
        ContentDocuments.Loaded<List<SpellEntry>>(document).Where(spell => spell is not null).ToList();
}
