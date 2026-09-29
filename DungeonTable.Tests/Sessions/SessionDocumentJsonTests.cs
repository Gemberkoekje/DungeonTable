using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DungeonTable.Core.Art;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Session;
using DungeonTable.Infrastructure.Sessions;
using Marten;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// Verifies the shape the session document is really stored in, through <b>Marten's own serializer</b>
/// rather than a second set of JSON options that would agree with it only until one of them changed.
/// No database is needed: what is under test is the serialization contract, not the round trip to
/// Postgres (that is <see cref="MartenSessionStoreTests"/>, which needs one).
/// </summary>
public sealed class SessionDocumentJsonTests
{
    [Fact]
    public void A_whole_table_survives_the_stored_json()
    {
        TableSnapshot stored = Sample();

        TableSnapshot restored = RoundTrip(stored);

        Assert.Equal("table", restored.SessionId);
        Assert.Equal(TableSnapshot.CurrentVersion, restored.Version);
        Assert.Equal(stored.SavedAt, restored.SavedAt);
        Assert.Equal("level-1-undercroft", restored.Reveals.CurrentMapId);
        Assert.Equal("level-1-undercroft", restored.Reveals.SeededMapId);
        Assert.Equal("region-entrywell", Assert.Single(restored.Reveals.RevealedRegionIds));
        Assert.Equal("trap-pit-1", Assert.Single(restored.Reveals.RevealedFeatureIds));
        Assert.Equal("door-7", Assert.Single(restored.Reveals.OpenDoorIds));
        Assert.Equal(16.0 / 10.0, restored.Reveals.PlayerAspect, 6);
        Assert.Equal(762.5, restored.Reveals.PlayerViewport.MaxY);

        Assert.Equal("mm-vampire-spawn", restored.Reveals.ShownArt.ImageId);
        Assert.True(restored.Reveals.ShownArt.Visible);
        Assert.Equal(ArtSize.Large, restored.Reveals.ShownArt.Size);
        Assert.Equal(2, restored.Reveals.ShownArt.TrayImageIds.Count);

        FogCell cell = Assert.Single(restored.Reveals.FogCells);
        Assert.Equal(4, cell.Col);
        Assert.Equal(9, cell.Row);

        Assert.Equal("b1", restored.Battle.Id);
        Assert.Equal(3, restored.Battle.Round);
        Assert.Equal(12, restored.Battle.LastIdNumber);
        Assert.Equal(4, restored.Battle.NextSequence);

        IssuedOrdinal issued = Assert.Single(restored.Battle.IssuedOrdinals);
        Assert.Equal("data_statblocks_monsters_bugbear", issued.TypeKey);
        Assert.Equal(3, issued.Issued);

        InitiativeEntry entry = Assert.Single(restored.Battle.Entries);
        Assert.Equal("Bugbears", entry.Label);
        Assert.Equal(17, entry.Initiative);
        Assert.True(entry.HasInitiative);
        Assert.Equal(CombatantKind.Monster, entry.Kind);
        Assert.Equal("c5", Assert.Single(entry.MemberIds));

        QuestProgress quest = Assert.Single(restored.Campaign.Quests);
        Assert.Equal("the-millers-son", quest.QuestId);
        Assert.Equal(QuestStatus.Active, quest.Status);
        Assert.Equal("esvele-asks", Assert.Single(quest.CompletedBeatIds));
        DeckProgress deck = Assert.Single(restored.Campaign.Decks);
        Assert.Equal("village-rumours", deck.DeckId);
        Assert.Equal("the-millers-debt", Assert.Single(deck.DrawnCardIds));

        AreaProgress area = Assert.Single(restored.Campaign.Areas);
        Assert.Equal("data_dossiers_level_1_area_1", area.AreaNodeId);
        Assert.Equal("data_statblocks_monsters_bugbear", Assert.Single(area.ClearedCreatureIds));
        Assert.Equal("They spiked the door open.", area.Note);
    }

    [Fact]
    public void A_document_written_before_the_area_checklist_existed_still_restores_the_campaign()
    {
        // Areas went into CampaignSnapshot additively, again without bumping CurrentVersion, so a
        // document from an earlier build must keep restoring its quest ticks and drawn cards and
        // simply arrive with no area progress.
        string json = SessionDocuments.Serializer().ToJson(Sample()).Replace(
            "\"areas\"",
            "\"areasRemovedByTest\"",
            StringComparison.OrdinalIgnoreCase);

        TableSnapshot restored = Read(json);

        Assert.Equal("the-millers-son", Assert.Single(restored.Campaign.Quests).QuestId);
        Assert.NotNull(restored.Campaign.Areas);
        Assert.Empty(restored.Campaign.Areas);
    }

    [Fact]
    public void A_document_written_before_decks_were_content_restores_with_nothing_drawn()
    {
        // Decks, one entry per deck, replaced a single deck's drawnSecretIds, and deliberately
        // migrated nothing: the decks had not been used at the table. A document from before must
        // still restore everything else, simply arriving with nothing drawn from any deck.
        JsonNode document = JsonNode.Parse(SessionDocuments.Serializer().ToJson(Sample()));
        JsonObject campaign = document["campaign"].AsObject();
        Assert.True(campaign.Remove("decks"));
        campaign["drawnSecretIds"] = new JsonArray("secret-3", "secret-7");

        TableSnapshot restored = Read(document.ToJsonString());

        Assert.Equal("the-millers-son", Assert.Single(restored.Campaign.Quests).QuestId);
        Assert.Equal("data_dossiers_level_1_area_1", Assert.Single(restored.Campaign.Areas).AreaNodeId);
        Assert.Equal("b1", restored.Battle.Id);
        Assert.NotNull(restored.Campaign.Decks);
        Assert.Empty(restored.Campaign.Decks);
    }

    [Fact]
    public void A_document_written_before_the_campaign_existed_still_restores_the_table()
    {
        // Campaign was added additively and deliberately did NOT bump CurrentVersion, so a document
        // written by an earlier build has to keep restoring — losing quest ticks it never had, and
        // nothing else.
        TableSnapshot before = Sample();
        string json = SessionDocuments.Serializer().ToJson(before).Replace(
            "\"campaign\"",
            "\"campaignRemovedByTest\"",
            StringComparison.OrdinalIgnoreCase);

        TableSnapshot restored = Read(json);

        Assert.Equal(TableSnapshot.CurrentVersion, restored.Version);
        Assert.Equal("level-1-undercroft", restored.Reveals.CurrentMapId);
        Assert.Equal("b1", restored.Battle.Id);
        Assert.NotNull(restored.Campaign);
        Assert.Empty(restored.Campaign.Quests);
        Assert.Empty(restored.Campaign.Decks);
    }

    [Fact]
    public void A_document_written_before_the_projector_art_existed_still_restores_the_table()
    {
        // ShownArt was added inside Reveals, additively and again without bumping CurrentVersion.
        // A document from any earlier build has to keep restoring the map, the reveals and the
        // fight, losing only a staged picture it never had.
        string json = SessionDocuments.Serializer().ToJson(Sample()).Replace(
            "\"shownArt\"",
            "\"shownArtRemovedByTest\"",
            StringComparison.OrdinalIgnoreCase);

        TableSnapshot restored = Read(json);

        Assert.Equal("level-1-undercroft", restored.Reveals.CurrentMapId);
        Assert.Equal("region-entrywell", Assert.Single(restored.Reveals.RevealedRegionIds));
        Assert.Equal("b1", restored.Battle.Id);
        Assert.NotNull(restored.Reveals.ShownArt);
        Assert.Equal(string.Empty, restored.Reveals.ShownArt.ImageId);
        Assert.False(restored.Reveals.ShownArt.Visible);
        Assert.Empty(restored.Reveals.ShownArt.TrayImageIds);
    }

    [Fact]
    public void Enums_are_stored_as_names_so_inserting_a_condition_cannot_reinterpret_a_saved_one()
    {
        // Asserted case-insensitively on purpose: what matters is that the *name* is written, not which
        // casing policy Marten applies to enum members.
        string json = SessionDocuments.Serializer().ToJson(Sample());

        Assert.Contains("\"prone\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"monster\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"conditions\":[10]", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Derived_combatant_flags_are_not_written_into_the_document()
    {
        // "down" / "bloodied" follow from the hit points beside them. Storing them would leave a
        // database row that can disagree with itself the day either rule changes.
        string json = SessionDocuments.Serializer().ToJson(Sample());

        Assert.DoesNotContain("\"down\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"bloodied\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_settable_property_of_a_combatant_survives_the_stored_json()
    {
        // The document reuses the live Combatant type so that state added later (death saves, innate
        // spell uses) is persisted without anyone having to remember to extend a parallel DTO. This
        // walks the type by reflection so a property that CANNOT round-trip fails the build instead of
        // silently not being saved.
        var combatant = new Combatant
        {
            Id = "c5",
            Kind = CombatantKind.Ally,
            StatBlockNodeId = "data_statblocks_monsters_wolf",
            Ordinal = 2,
            Name = "Wolf 2",
            CurrentHp = 3,
            MaxHp = 11,
            ArmourClass = 13,
            PassivePerception = 13,
            Conditions = new[] { ConditionKind.Frightened },
            Concentrating = true,
            ConcentrationNote = "hold person",
            Notes = "guarding the stairs",
            SpentSlots = new[] { new SpentSlot { Level = 1, Spent = 2 } },
        };

        Combatant restored = RoundTrip(Table(combatant)).Battle.Combatants.Single();

        IEnumerable<PropertyInfo> settable = typeof(Combatant)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null);

        // Guards the guard: if the reflection above ever selects nothing, the test would pass vacuously.
        Assert.True(settable.Count() >= 14);
        Assert.All(settable, property => AssertRoundTripped(property, combatant, restored));
    }

    [Fact]
    public void A_document_missing_its_sections_restores_as_an_empty_table()
    {
        // Marten hands back whatever is in the column. A hand-edited or partial document must not throw
        // on the way in — the app has to come up, even if it comes up with an empty table.
        TableSnapshot restored = Read("{\"sessionId\":\"table\"}");

        Assert.Equal("table", restored.SessionId);
        Assert.NotNull(restored.Reveals);
        Assert.NotNull(restored.Battle);
        Assert.NotNull(restored.Campaign);
        Assert.False(restored.Battle.HasBattle());
        Assert.Empty(restored.Reveals.RevealedRegionIds);
        Assert.Empty(restored.Campaign.Quests);
    }

    // Compared as JSON so one assertion covers scalars, enums, and arrays of POCOs alike (a restored
    // SpentSlot is a different instance, so reference equality would report a false failure). This is
    // only the comparison mechanism — the storage contract itself is what RoundTrip exercised.
    private static void AssertRoundTripped(PropertyInfo property, Combatant original, Combatant restored)
    {
        string expected = JsonSerializer.Serialize(property.GetValue(original));
        string actual = JsonSerializer.Serialize(property.GetValue(restored));

        Assert.Equal(expected, actual);
    }

    private static TableSnapshot RoundTrip(TableSnapshot stored) =>
        Read(SessionDocuments.Serializer().ToJson(stored));

    // Marten's ISerializer reads from a stream (that is how it reads a jsonb column), so a test that
    // starts from a string hands it one.
    private static TableSnapshot Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return SessionDocuments.Serializer().FromJson<TableSnapshot>(stream);
    }

    private static TableSnapshot Table(Combatant combatant) => new TableSnapshot
    {
        SessionId = "table",
        Version = TableSnapshot.CurrentVersion,
        Battle = new BattleSnapshot
        {
            Id = "b1",
            Combatants = new[] { combatant },
        },
    };

    private static TableSnapshot Sample() => new TableSnapshot
    {
        SessionId = "table",
        Version = TableSnapshot.CurrentVersion,
        SavedAt = new DateTimeOffset(2026, 7, 30, 20, 31, 0, TimeSpan.Zero),
        Reveals = new RevealSnapshot
        {
            CurrentMapId = "level-1-undercroft",
            SeededMapId = "level-1-undercroft",
            RevealedRegionIds = new[] { "region-entrywell" },
            RevealedFeatureIds = new[] { "trap-pit-1" },
            OpenDoorIds = new[] { "door-7" },
            FogCells = new[] { new FogCell { Col = 4, Row = 9 } },
            PlayerViewport = new ViewportSnapshot { MinX = 100, MinY = 200, MaxX = 1_100, MaxY = 762.5 },
            PlayerAspect = 16.0 / 10.0,
            ShownArt = new ShownArtSnapshot
            {
                ImageId = "mm-vampire-spawn",
                Visible = true,
                Size = ArtSize.Large,
                TrayImageIds = new[] { "mm-vampire-spawn", "mm-bandit-captain" },
            },
        },
        Battle = new BattleSnapshot
        {
            Id = "b1",
            AreaNodeId = "data_dossiers_level_1_area_3a",
            AreaTitle = "Nave",
            Round = 3,
            TurnIndex = 0,
            Started = true,
            LastIdNumber = 12,
            NextSequence = 4,
            IssuedOrdinals = new[] { new IssuedOrdinal { TypeKey = "data_statblocks_monsters_bugbear", Issued = 3 } },
            Entries = new[]
            {
                new InitiativeEntry
                {
                    Id = "e2",
                    Sequence = 1,
                    Initiative = 17,
                    HasInitiative = true,
                    InitiativeModifier = 2,
                    Label = "Bugbears",
                    MemberIds = new[] { "c5" },
                    Kind = CombatantKind.Monster,
                    StatBlockNodeId = "data_statblocks_monsters_bugbear",
                },
            },
            Combatants = new[]
            {
                new Combatant
                {
                    Id = "c5",
                    Kind = CombatantKind.Monster,
                    StatBlockNodeId = "data_statblocks_monsters_bugbear",
                    Ordinal = 1,
                    Name = "Bugbear 1",
                    CurrentHp = 7,
                    MaxHp = 27,
                    ArmourClass = 16,
                    PassivePerception = 10,
                    Conditions = new[] { ConditionKind.Prone },
                    Notes = "fled east",
                    SpentSlots = new[] { new SpentSlot { Level = 2, Spent = 1 } },
                },
            },
        },
        Campaign = new CampaignSnapshot
        {
            Quests = new[]
            {
                new QuestProgress
                {
                    QuestId = "the-millers-son",
                    Status = QuestStatus.Active,
                    CompletedBeatIds = new[] { "esvele-asks" },
                },
            },
            Decks = new[]
            {
                new DeckProgress { DeckId = "village-rumours", DrawnCardIds = new[] { "the-millers-debt" } },
            },
            Areas = new[]
            {
                new AreaProgress
                {
                    AreaNodeId = "data_dossiers_level_1_area_1",
                    ClearedCreatureIds = new[] { "data_statblocks_monsters_bugbear" },
                    Note = "They spiked the door open.",
                },
            },
        },
    };
}
