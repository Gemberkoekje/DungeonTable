using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Art;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Maps;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules about documents as files: every document loads, no file in a folder of documents is
/// one the app never reads, and no list holds a <c>null</c>. All three are silent in the app, which
/// skips what it cannot read so the table stays up; a typo in a hand-edited file would otherwise just
/// empty a tab.
/// </summary>
internal static class DocumentAudit
{
    /// <summary>Why a document does not load the way the app reads it, or an empty string when it does.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The reason, or an empty string.</returns>
    internal static async Task<string> LoadFaultAsync(ContentDocument document)
    {
        if (document.Kind == DocumentKind.MapDrawing)
        {
            Result<VectorMap> drawing = await new DungeonScrawlMapReader().ReadFileAsync(document.FullPath, CancellationToken.None);
            return drawing.IsValid ? string.Empty : Check.Messages(drawing);
        }

        return document.Kind switch
        {
            DocumentKind.Level => Fault(ContentDocuments.Read<LevelDossierSet>(document)),
            DocumentKind.Reference => Fault(ContentDocuments.Read<ReferenceLibrary>(document)),
            DocumentKind.Campaign => Fault(ContentDocuments.Read<CampaignDossier>(document)),
            DocumentKind.Characters => Fault(ContentDocuments.Read<PartyDossier>(document)),
            DocumentKind.Npcs => Fault(ContentDocuments.Read<NpcRoster>(document)),
            DocumentKind.Story => Fault(ContentDocuments.Read<StoryLibrary>(document)),
            DocumentKind.Sessions => Fault(ContentDocuments.Read<SessionLog>(document)),
            DocumentKind.Relations => Fault(ContentDocuments.Read<RelationList>(document)),
            DocumentKind.Quests => Fault(ContentDocuments.Read<QuestLog>(document)),
            DocumentKind.Deck => Fault(ContentDocuments.Read<CardDeck>(document)),
            DocumentKind.Monsters => Fault(ContentDocuments.Read<List<StatBlock>>(document)),
            DocumentKind.Spells => Fault(ContentDocuments.Read<List<SpellEntry>>(document)),
            DocumentKind.BookIndex => Fault(ContentDocuments.Read<BookIndex>(document)),
            DocumentKind.ArtCatalogue => Fault(ContentDocuments.Read<ArtCatalogue>(document)),
            DocumentKind.MapRegions => Fault(ContentDocuments.Read<MapDefinition>(document)),
            DocumentKind.PartyRoster => Fault(ContentDocuments.Read<Party>(document)),
            DocumentKind.AllyRoster => Fault(ContentDocuments.Read<AllyRoster>(document)),
            _ => "it is no kind of document the app reads.",
        };
    }

    /// <summary>
    /// Every <c>.json</c> file in a folder of documents that the app reads under no name: most likely a
    /// document meant for it, under a name that is slightly off.
    /// </summary>
    /// <param name="unread">The files.</param>
    /// <returns>One sentence per file (empty when there are none).</returns>
    internal static IReadOnlyList<string> UnreadFiles(IEnumerable<UnreadFile> unread) =>
        unread.Select(file => $"{file.Name}: the app reads no file by this name.{CaseHint(file)} The documents in its folder are named {string.Join(", ", file.Patterns)}.").ToList();

    /// <summary>
    /// Every <c>null</c> entry in a list, named by where it is in the document ("areas[0]",
    /// "quests[1].beats[0]", "[3]" in a document that is a list). The stores drop one without a word,
    /// since it holds nothing to show, so an entry meant to be written there is silently missing. A
    /// <c>null</c> list is not reported: it reads as an empty one, as <c>[]</c> would.
    /// </summary>
    /// <param name="json">A document that loads, as written.</param>
    /// <returns>One sentence per entry (empty when there are none).</returns>
    internal static IReadOnlyList<string> NullEntries(string json) =>
        Infrastructure.Content.NullEntries.Described(json);

    // A name that matches once case is ignored is read on a Windows machine and not on the Linux server.
    private static string CaseHint(UnreadFile file)
    {
        string name = file.Name[(file.Name.LastIndexOf('/') + 1)..];
        return file.Patterns.Any(pattern => ContentDocuments.Matches(name.ToLowerInvariant(), pattern))
            ? " Names are matched exactly, casing and all, on the Linux server, so write it in lower case."
            : string.Empty;
    }

    private static string Fault<T>(Result<T> read) => read.IsValid ? string.Empty : Check.Messages(read);
}
