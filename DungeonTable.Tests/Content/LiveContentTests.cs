using System.Collections.Generic;
using System.IO;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Content;

namespace DungeonTable.Tests.Content;

/// <summary>
/// The content read again while the app runs: a new snapshot reaches every store the pages use at
/// once, and a document that stops parsing (a half-saved file, a typo) does not take what the table
/// had away with it.
/// </summary>
public sealed class LiveContentTests : IDisposable
{
    // What a change raised, hoisted out of the assertions (CA1861).
    private static readonly ContentChanges[] OnlyDocuments = { ContentChanges.Documents };
    private static readonly ContentChanges[] OnlyMaps = { ContentChanges.Maps };

    private readonly PackCopy pack = new PackCopy();

    public void Dispose() => pack.Dispose();

    [Fact]
    public void A_changed_document_reaches_every_store_the_pages_use()
    {
        LiveContent live = Live();
        var dossiers = new LiveDossierStore(live);
        var projection = new LiveContentProjection(live);
        var resolver = new LiveCrossRefResolver(live);
        var raised = new List<ContentChanges>();
        live.Changed += raised.Add;
        Assert.DoesNotContain(dossiers.GetNpcs().Value.Npcs, npc => npc.Name == "Wenna Mill");

        Edit("data/dossiers/npcs.json", "Wenna Brask", "Wenna Mill");
        ContentReload reload = live.ReloadDocuments();

        Assert.True(reload.Applied);
        Assert.Contains(dossiers.GetNpcs().Value.Npcs, npc => npc.Name == "Wenna Mill");
        Assert.Contains(projection.SearchNodes("Wenna Mill", 5), match => match.Id == "wenna-brask");
        CrossRef link = Assert.Single(resolver.Resolve("Ask [[wenna-brask]]."));
        Assert.Equal("Wenna Mill", link.Text);
        Assert.Equal(OnlyDocuments, raised);
    }

    [Fact]
    public void A_document_that_stops_parsing_keeps_the_content_the_table_had()
    {
        LiveContent live = Live();
        var dossiers = new LiveDossierStore(live);
        int npcs = dossiers.GetNpcs().Value.Npcs.Count;
        var raised = new List<ContentChanges>();
        live.Changed += raised.Add;

        Truncate("data/dossiers/npcs.json");
        ContentReload reload = live.ReloadDocuments();

        Assert.False(reload.Applied);
        ContentProblem blocking = Assert.Single(reload.Blocking);
        Assert.EndsWith("npcs.json", blocking.File, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, blocking.Reason);
        Assert.Equal(npcs, dossiers.GetNpcs().Value.Npcs.Count);
        Assert.Empty(raised);
    }

    [Fact]
    public void Fixing_the_document_lets_the_next_reload_through()
    {
        LiveContent live = Live();
        string npcs = Path.Combine(pack.Root, "data", "dossiers", "npcs.json");
        string whole = File.ReadAllText(npcs);
        Truncate("data/dossiers/npcs.json");
        Assert.False(live.ReloadDocuments().Applied);

        File.WriteAllText(npcs, whole.Replace("Wenna Brask", "Wenna Mill", StringComparison.Ordinal));
        ContentReload reload = live.ReloadDocuments();

        Assert.True(reload.Applied);
        Assert.Contains(new LiveDossierStore(live).GetNpcs().Value.Npcs, npc => npc.Name == "Wenna Mill");
    }

    [Fact]
    public void A_document_that_never_parsed_does_not_hold_a_reload_up()
    {
        // Broken before the app started, so the table never had it to lose.
        File.WriteAllText(Path.Combine(pack.Root, "data", "dossiers", "quests-side.json"), "{ \"quests\": [ ");
        LiveContent live = Live();

        Edit("data/dossiers/npcs.json", "Wenna Brask", "Wenna Mill");
        ContentReload reload = live.ReloadDocuments();

        Assert.True(reload.Applied);
        Assert.Contains(reload.Problems, problem => problem.DocumentSkipped && problem.File.EndsWith("quests-side.json", StringComparison.Ordinal));
    }

    [Fact]
    public void A_stat_block_edit_reaches_the_library_and_a_new_book_the_index()
    {
        LiveContent live = Live();
        var stats = new LiveStatLibrary(live);
        var books = new LiveBookIndexStore(live);
        int bookCount = books.AllBooks().Count;

        File.WriteAllText(Path.Combine(pack.Root, "data", "statblocks", "monsters.json"), """
            [ { "nodeId": "grask", "name": "Grask", "armourClass": 17, "challengeRating": "3" } ]
            """);
        File.WriteAllText(Path.Combine(pack.Root, "data", "book-index", "ledger.json"), """
            { "book": "ledger", "title": "The Mill Ledger", "entries": [ { "id": "the-debt", "name": "The Debt" } ] }
            """);
        live.ReloadDocuments();

        Assert.Equal(17, stats.GetMonster("grask").Value.ArmourClass);
        Assert.Equal(bookCount + 1, books.AllBooks().Count);
    }

    [Fact]
    public void A_map_or_the_art_is_announced_as_what_it_is()
    {
        LiveContent live = Live();
        var raised = new List<ContentChanges>();
        live.Changed += raised.Add;

        live.Announce(ContentChanges.Maps);
        live.Announce(ContentChanges.None);

        Assert.Equal(OnlyMaps, raised);
    }

    private LiveContent Live() => new LiveContent(
        Path.Combine(pack.Root, "data", "dossiers"),
        Path.Combine(pack.Root, "data", "statblocks"),
        Path.Combine(pack.Root, "data", "book-index"));

    private void Edit(string relative, string from, string to)
    {
        string path = Path.Combine(pack.Root, relative);
        File.WriteAllText(path, File.ReadAllText(path).Replace(from, to, StringComparison.Ordinal));
    }

    private void Truncate(string relative)
    {
        string path = Path.Combine(pack.Root, relative);
        File.WriteAllText(path, File.ReadAllText(path)[..200]);
    }
}
