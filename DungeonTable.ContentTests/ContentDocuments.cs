using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Infrastructure.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests;

/// <summary>
/// Every file in a content root that the app reads, found under the names the app's own stores use,
/// and a way to read each one exactly as its store does.
/// </summary>
/// <remarks>
/// The stores are fail-soft: a document that does not parse is skipped, and an entry with no id is
/// dropped, silently, so that one slip cannot take the table down. Reading each document again here
/// is how the rules see what the stores left out.
/// </remarks>
internal static class ContentDocuments
{
    // The names each store reads its documents under, taken from the store itself. A name matches the
    // way Linux, where the app is deployed, matches it: exactly, casing and all.
    private static readonly (string Pattern, DocumentKind Kind)[] DossierNames =
    {
        (FileSystemDossierStore.LevelFilePattern, DocumentKind.Level),
        (FileSystemDossierStore.ReferenceFileName, DocumentKind.Reference),
        (FileSystemDossierStore.CampaignFileName, DocumentKind.Campaign),
        (FileSystemDossierStore.CharactersFileName, DocumentKind.Characters),
        (FileSystemDossierStore.NpcsFileName, DocumentKind.Npcs),
        (FileSystemDossierStore.StoryFileName, DocumentKind.Story),
        (FileSystemDossierStore.SessionsFileName, DocumentKind.Sessions),
        (FileSystemDossierStore.RelationsFileName, DocumentKind.Relations),
        (FileSystemDossierStore.QuestFilePattern, DocumentKind.Quests),
        (FileSystemDossierStore.DeckFilePattern, DocumentKind.Deck),
    };

    private static readonly (string Pattern, DocumentKind Kind)[] StatNames =
    {
        (FileSystemStatLibrary.MonstersFilePattern, DocumentKind.Monsters),
        (FileSystemStatLibrary.SpellsFilePattern, DocumentKind.Spells),
    };

    private static readonly (string Pattern, DocumentKind Kind)[] BookNames =
    {
        ("*.json", DocumentKind.BookIndex),
    };

    private static readonly (string Pattern, DocumentKind Kind)[] ArtNames =
    {
        (FileSystemArtCatalogue.CatalogueFileName, DocumentKind.ArtCatalogue),
    };

    private static readonly (string Pattern, DocumentKind Kind)[] RosterNames =
    {
        (FileSystemPartyRoster.FileName, DocumentKind.PartyRoster),
        (FileSystemAllyRoster.FileName, DocumentKind.AllyRoster),
    };

    private static readonly Lazy<DocumentListing> Listed = new(() => List(ContentRoot.Folder, ContentRoot.StartFolder));

    /// <summary>Every document in the content under test, in a stable order.</summary>
    internal static IReadOnlyList<ContentDocument> All => Listed.Value.Documents;

    /// <summary>Every <c>.json</c> file in its folders of documents that the app reads under no name.</summary>
    internal static IReadOnlyList<UnreadFile> Unread => Listed.Value.Unread;

    /// <summary>The documents of one kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Those documents, in a stable order.</returns>
    internal static IReadOnlyList<ContentDocument> Of(DocumentKind kind) =>
        All.Where(document => document.Kind == kind).ToArray();

    /// <summary>The names of the documents of some kinds, as a theory's rows.</summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>One row per document.</returns>
    internal static TheoryData<string> Names(params DocumentKind[] kinds) =>
        Rows(All.Where(document => kinds.Contains(document.Kind)));

    /// <summary>Every document of one kind that loads, with its name, for a rule that looks across them.</summary>
    /// <typeparam name="T">What the documents are read as.</typeparam>
    /// <param name="kind">The kind.</param>
    /// <returns>The documents that load, by name.</returns>
    internal static IReadOnlyList<(string File, T Content)> LoadedOf<T>(DocumentKind kind)
        where T : class =>
        Of(kind)
            .Select(document => (document.Name, Read: Read<T>(document)))
            .Where(document => document.Read.IsValid)
            .Select(document => (document.Name, document.Read.Value))
            .ToList();

    /// <summary>The names of every document, as a theory's rows.</summary>
    /// <returns>One row per document.</returns>
    internal static TheoryData<string> AllNames() => Rows(All);

    /// <summary>The document a theory row names.</summary>
    /// <param name="name">Its name, relative to the content root.</param>
    /// <returns>The document.</returns>
    internal static ContentDocument Named(string name) =>
        All.Single(document => string.Equals(document.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// The document a theory row names, read as the app reads it. The test is skipped when the
    /// document does not load, since the rule that every document loads reports that already.
    /// </summary>
    /// <typeparam name="T">What the document is read as.</typeparam>
    /// <param name="name">Its name, relative to the content root.</param>
    /// <returns>The document's content.</returns>
    internal static T Loaded<T>(string name)
        where T : class
    {
        Result<T> read = Read<T>(Named(name));
        Assert.SkipUnless(read.IsValid, $"{name} does not load, which Every_document_loads reports.");
        return read.Value;
    }

    /// <summary>Reads a document with the options of the store that reads it.</summary>
    /// <typeparam name="T">What the document is read as.</typeparam>
    /// <param name="document">The document.</param>
    /// <returns>Its content, or why it cannot be read.</returns>
    internal static Result<T> Read<T>(ContentDocument document)
        where T : class =>
        Read<T>(document.FullPath, OptionsFor(document.Kind));

    /// <summary>Reads a file the way the stores do, but keeps the reason when it cannot be read.</summary>
    /// <typeparam name="T">What the file is read as.</typeparam>
    /// <param name="path">The file.</param>
    /// <param name="options">The reading store's options.</param>
    /// <returns>Its content, or why it cannot be read.</returns>
    internal static Result<T> Read<T>(string path, JsonSerializerOptions options)
        where T : class
    {
        try
        {
            T value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);
            return value is null
                ? Failed<T>("it holds nothing but null.")
                : Result.For(value);
        }
        catch (JsonException error)
        {
            return Failed<T>(error.Message);
        }
        catch (IOException error)
        {
            return Failed<T>(error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            return Failed<T>(error.Message);
        }
    }

    /// <summary>
    /// Lists every document in a content root, found the way the app finds each kind of content under
    /// <c>Content:Root</c>. A kind the content root lacks has no documents here; its own rule reports it.
    /// </summary>
    /// <param name="contentRoot">The content root.</param>
    /// <param name="start">Where the locators' walk-up fallback starts: an empty folder.</param>
    /// <returns>The documents, and the <c>.json</c> files the app would not read.</returns>
    internal static DocumentListing List(string contentRoot, string start)
    {
        var documents = new List<ContentDocument>();
        var unread = new List<UnreadFile>();
        AddFolder(contentRoot, Located(() => DossierFileLocator.Locate(start, configuredPath: null, contentRoot)), DossierNames, documents, unread);
        AddFolder(contentRoot, Located(() => StatFileLocator.Locate(start, configuredPath: null, contentRoot)), StatNames, documents, unread);
        AddFolder(contentRoot, BookIndexFileLocator.Locate(start, configuredPath: null, contentRoot), BookNames, documents, unread);
        AddFolder(contentRoot, Located(() => ArtFileLocator.Locate(start, configuredPath: null, contentRoot)), ArtNames, documents, unread);
        AddFolder(contentRoot, Located(() => RosterFileLocator.Locate(start, configuredPath: null, contentRoot)), RosterNames, documents, unread);
        AddMaps(contentRoot, Located(() => MapFileLocator.Locate(start, configuredPath: null, contentRoot)), documents);
        return new DocumentListing(documents, unread);
    }

    /// <summary>Whether a file name matches a store's pattern, exactly as Linux would match it.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="pattern">A name, or a pattern with one <c>*</c> ("level-*.json").</param>
    /// <returns>True when it matches.</returns>
    internal static bool Matches(string fileName, string pattern) => DocumentNames.Matches(fileName, pattern);

    private static void AddFolder(
        string contentRoot,
        string folder,
        (string Pattern, DocumentKind Kind)[] names,
        List<ContentDocument> documents,
        List<UnreadFile> unread)
    {
        if (folder.Length == 0)
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(folder).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(path);
            DocumentKind kind = names.FirstOrDefault(entry => Matches(name, entry.Pattern)).Kind;
            if (kind != DocumentKind.None)
            {
                documents.Add(new ContentDocument(Relative(contentRoot, path), path, kind));
            }
            else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                unread.Add(new UnreadFile(Relative(contentRoot, path), names.Select(entry => entry.Pattern).ToArray()));
            }
        }
    }

    // A map's annotations are matched ignoring case, as the map store matches them; its drawing
    // exactly, as the drawing store's file search does on Linux. Everything else in the maps folder,
    // such as a raster map kept to trace from, is no document.
    private static void AddMaps(string contentRoot, string folder, List<ContentDocument> documents)
    {
        if (folder.Length == 0)
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (path.EndsWith(FileSystemMapStore.DefinitionSuffix, StringComparison.OrdinalIgnoreCase))
            {
                documents.Add(new ContentDocument(Relative(contentRoot, path), path, DocumentKind.MapRegions));
            }
            else if (path.EndsWith(FileSystemVectorMapStore.SourceSuffix, StringComparison.Ordinal))
            {
                documents.Add(new ContentDocument(Relative(contentRoot, path), path, DocumentKind.MapDrawing));
            }
        }
    }

    // A folder the content root lacks: its own rule says so, and here it has no documents.
    private static string Located(Func<string> locate)
    {
        try
        {
            return locate();
        }
        catch (DirectoryNotFoundException)
        {
            return string.Empty;
        }
    }

    private static string Relative(string contentRoot, string path) =>
        Path.GetRelativePath(contentRoot, path).Replace('\\', '/');

    private static TheoryData<string> Rows(IEnumerable<ContentDocument> documents)
    {
        var rows = new TheoryData<string>();
        foreach (ContentDocument document in documents)
        {
            rows.Add(document.Name);
        }

        return rows;
    }

    private static JsonSerializerOptions OptionsFor(DocumentKind kind) => kind switch
    {
        DocumentKind.Monsters or DocumentKind.Spells => FileSystemStatLibrary.Options,
        DocumentKind.BookIndex => FileSystemBookIndexStore.Options,
        DocumentKind.ArtCatalogue => FileSystemArtCatalogue.Options,
        DocumentKind.MapRegions => FileSystemMapStore.Options,
        DocumentKind.PartyRoster or DocumentKind.AllyRoster => RosterDocument.Options,
        _ => FileSystemDossierStore.Options,
    };

    private static Result<T> Failed<T>(string why) =>
        Result.WithMessages<T>(ValidationMessage.Error(why, "document"));
}
