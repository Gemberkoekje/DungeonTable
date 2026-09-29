using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests;

/// <summary>
/// The people: every NPC and character card has what it shows, every contact says where that person
/// stands, the party's dossier and roster seed agree, and every ally's stat block is one the library has.
/// </summary>
public sealed class PeopleTests
{
    public static TheoryData<string> NpcRosters => ContentDocuments.Names(DocumentKind.Npcs);

    public static TheoryData<string> PartyDossiers => ContentDocuments.Names(DocumentKind.Characters);

    public static TheoryData<string> AllyRosters => ContentDocuments.Names(DocumentKind.AllyRoster);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(NpcRosters))]
    public void Every_npc_card_has_what_it_shows(string document)
    {
        NpcRoster roster = ContentDocuments.Loaded<NpcRoster>(document);

        Report.None(PeopleAudit.IncompleteNpcs(roster), $"NPCs in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(NpcRosters))]
    public void Every_contact_says_where_that_person_stands(string document)
    {
        NpcRoster roster = ContentDocuments.Loaded<NpcRoster>(document);

        Report.None(PeopleAudit.IncompleteContacts(roster), $"Contacts in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(PartyDossiers))]
    public void Every_character_card_has_what_it_shows(string document)
    {
        PartyDossier party = ContentDocuments.Loaded<PartyDossier>(document);

        Report.None(PeopleAudit.IncompleteCharacters(party), $"Characters in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(PartyDossiers))]
    public void The_party_dossier_and_the_roster_seed_agree(string document)
    {
        PartyDossier party = ContentDocuments.Loaded<PartyDossier>(document);
        Party roster = ContentDocuments.Of(DocumentKind.PartyRoster) is [ContentDocument seed, ..]
            ? ContentDocuments.Loaded<Party>(seed.Name)
            : new Party();

        Report.None(PeopleAudit.Disagreements(party, roster), $"Where {document} and the party roster disagree");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(AllyRosters))]
    public void Every_ally_that_names_a_stat_block_names_one_the_library_has(string document)
    {
        AllyRoster allies = ContentDocuments.Loaded<AllyRoster>(document);

        Report.None(PeopleAudit.BadAllyBlocks(allies, ContentRoot.Stats), $"Allies in {document}");
    }
}
