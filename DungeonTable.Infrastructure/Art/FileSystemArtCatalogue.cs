using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Art;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Dossiers;
using Qowaiv.Validation.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;

namespace DungeonTable.Infrastructure.Art;

/// <summary>
/// File-system adapter for <see cref="IArtCatalogue"/>. Reads the committed
/// <c>catalogue.json</c> in the art root (the curated book art) and <c>uploads/catalogue.json</c>
/// beside it (whatever the DM has uploaded), and indexes both by id and by the node ids they depict.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Stats.FileSystemStatLibrary"/> this store is <em>mutable</em>: the upload
/// screen adds to it while the app is running. The in-memory index is therefore rebuilt under a
/// lock on every change and every read takes a snapshot under the same lock, so a DM uploading a
/// picture cannot tear the picker another circuit is rendering.
/// </para>
/// <para>
/// <b>The encode deliberately runs outside that lock.</b> Every read takes it — including the
/// player overlay asking for the picture currently on the projector — so holding it across a
/// multi-second decode/resize/WebP-encode would freeze the beamer at exactly the wrong moment.
/// <see cref="Upload"/> instead claims an id under the lock (see <c>reservedIds</c>), encodes
/// outside it, then commits under it again.
/// </para>
/// <para>
/// Uploads are never stored as they arrived. They are decoded — which is the validation that
/// matters — downscaled to <see cref="ArtUploadRules.MaxEdgePixels"/> and written back out as
/// WebP, which drops EXIF along the way. The two catalogues stay separate files on purpose: the
/// book art is under version control and the uploads live in the bind mount, and a single
/// rewritten file would drag one into the other.
/// </para>
/// </remarks>
public sealed class FileSystemArtCatalogue : IArtCatalogue
{
    // The file and folder names and the reading options below are internal so the content tests can
    // read the committed catalogue the way this store does, and report what it would skip.
    internal const string CatalogueFileName = "catalogue.json";

    // What a save failure is reported against; there is no method parameter to point at.
    private const string UploadsProperty = "uploads";
    internal const string UploadsDirectoryName = "uploads";

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,

        // The committed catalogue is hand-edited, so a null reads as if the field were left out, as in
        // the dossiers: search reads every title. Writing is unchanged by all of these.
        TypeInfoResolver = NullMeansLeftOut.Resolver(),
        Converters =
        {
            new NullTolerantValueConverter(),

            // A null list is an empty one and a null entry is dropped: search reads every tag.
            new LenientListConverter(),
            new LenientStringConverter(),
        },
    };

    // The one name read at the top of the art folder, for the report of a .json nothing reads.
    private static readonly string[] CatalogueNames = { CatalogueFileName };

    private readonly object gate = new object();
    private readonly string artRoot;
    private readonly string uploadsRoot;
    private readonly List<ArtImage> book = new List<ArtImage>();
    private readonly List<ArtImage> uploads = new List<ArtImage>();

    // Ids claimed by an upload that is still encoding. The encode deliberately runs outside the
    // lock, so without this two uploads started together would both see the id as free and the
    // second would overwrite the first's file.
    private readonly HashSet<string> reservedIds = new HashSet<string>(StringComparer.Ordinal);

    // What reading the committed catalogue and the uploads' skipped or dropped; replaced whole by a
    // reload of the committed one.
    private List<ContentProblem> problems = new List<ContentProblem>();

    // Rebuilt from book + uploads whenever either changes; never handed out by reference.
    private Dictionary<string, ArtImage> byId = new Dictionary<string, ArtImage>(StringComparer.Ordinal);
    private List<ArtImage> ordered = new List<ArtImage>();
    private Dictionary<string, List<ArtImage>> byNode = new Dictionary<string, List<ArtImage>>(StringComparer.Ordinal);

    /// <summary>Creates a catalogue over an art root directory, loading both documents.</summary>
    /// <param name="artRoot">Absolute path to the directory holding <c>catalogue.json</c>.</param>
    public FileSystemArtCatalogue(string artRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artRoot);
        this.artRoot = artRoot;
        uploadsRoot = Path.Combine(artRoot, UploadsDirectoryName);

        Load(Path.Combine(artRoot, CatalogueFileName), ArtOrigin.Book, book, problems);
        Load(Path.Combine(uploadsRoot, CatalogueFileName), ArtOrigin.Upload, uploads, problems);
        Reindex();
        problems.AddRange(DocumentNames.Unread(artRoot, CatalogueNames));
    }

    /// <summary>
    /// Reads the committed <c>catalogue.json</c> again, after it changed on disk, and raises
    /// <see cref="Changed"/>. The uploads are the app's own and stay as they are. A catalogue that was
    /// read before and cannot be read now is not taken: the pictures it listed stay until it is fixed,
    /// rather than vanish from the picker mid-session over a half-saved file.
    /// </summary>
    /// <returns>What the new reading skipped or dropped. When the catalogue could not be read, its
    /// problem has <see cref="ContentProblem.DocumentSkipped"/> set and the old list was kept.</returns>
    public IReadOnlyList<ContentProblem> ReloadCommitted()
    {
        string committed = Path.Combine(artRoot, CatalogueFileName);
        var reread = new List<ArtImage>();
        var found = new List<ContentProblem>();
        Load(committed, ArtOrigin.Book, reread, found);

        lock (gate)
        {
            bool unreadableNow = found.Exists(problem => problem.DocumentSkipped);
            bool readableBefore = !problems.Exists(problem => problem.DocumentSkipped && string.Equals(problem.File, committed, StringComparison.Ordinal));
            if (unreadableNow && readableBefore)
            {
                return found;
            }

            book.Clear();
            book.AddRange(reread);
            // The uploads' own problems stand; the committed catalogue's and the files beside it are read
            // afresh.
            problems = found
                .Concat(problems.Where(problem => problem.File.StartsWith(uploadsRoot, StringComparison.Ordinal)))
                .Concat(DocumentNames.Unread(artRoot, CatalogueNames))
                .ToList();
            Reindex();
        }

        Changed?.Invoke();
        return found;
    }

    /// <summary>
    /// What the catalogue skipped or dropped while reading, file by file: a catalogue that does not
    /// parse, a picture with no id or no file, an id listed twice, a <c>null</c> entry, and a
    /// <c>.json</c> beside the catalogue that nothing reads. Empty when everything loaded.
    /// </summary>
    public IReadOnlyList<ContentProblem> Problems
    {
        get
        {
            lock (gate)
            {
                return problems.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public event Action Changed;

    /// <inheritdoc />
    public Result<ArtImage> GetImage(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
        {
            return Result.WithMessages<ArtImage>(
                ValidationMessage.Error("An image id is required.", nameof(imageId)));
        }

        lock (gate)
        {
            return byId.TryGetValue(imageId, out ArtImage image)
                ? Result.For(image)
                : Result.WithMessages<ArtImage>(
                    ValidationMessage.Error($"No catalogued image has the id '{imageId}'.", nameof(imageId)));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ArtImage> All()
    {
        lock (gate) { return ordered.ToArray(); }
    }

    /// <inheritdoc />
    public IReadOnlyList<ArtImage> Search(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Array.Empty<ArtImage>();
        }

        string needle = query.Trim().ToLowerInvariant();
        lock (gate)
        {
            return ordered
                .Select(image => (image, rank: Rank(image, needle)))
                .Where(match => match.rank >= 0)
                .OrderBy(match => match.rank)
                .ThenBy(match => match.image.Title, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(match => match.image)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ArtImage> ForNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Array.Empty<ArtImage>();
        }

        lock (gate)
        {
            return byNode.TryGetValue(nodeId, out List<ArtImage> images)
                ? images.ToArray()
                : Array.Empty<ArtImage>();
        }
    }

    /// <inheritdoc />
    public Result<ArtImage> Upload(string title, byte[] content)
    {
        if (content is null || content.Length == 0)
        {
            return Refused("The file is empty.");
        }

        if (content.Length > ArtUploadRules.MaxBytes)
        {
            return Refused(
                $"The file is {content.Length / (1024 * 1024)} MB; the limit is " +
                $"{ArtUploadRules.MaxBytes / (1024 * 1024)} MB.");
        }

        if (!ArtUploadRules.IsAcceptedRaster(content))
        {
            // Deliberately says what is accepted rather than what was sent: the DM's next move is
            // to convert the file, and an SVG being named here as "not an image" would be a lie.
            return Refused($"That is not a {ArtUploadRules.AcceptedFormats} image.");
        }

        // Read the declared dimensions from the header WITHOUT decoding. The byte cap above bounds
        // the download, not the decode: a near-solid-colour PNG declaring 30000x30000 is a few KB
        // and still asks for gigabytes of pixel buffer, which on this one shared process would
        // take the whole table down with it. Identify parses the header only.
        Result<ArtImage> measured = Measure(content, out int sourceWidth, out int sourceHeight);
        if (measured is not null)
        {
            return measured;
        }

        if ((long)sourceWidth * sourceHeight > ArtUploadRules.MaxPixels)
        {
            return Refused(
                $"That image is {sourceWidth}x{sourceHeight} pixels; the limit is " +
                $"{ArtUploadRules.MaxPixels / 1_000_000} megapixels. Scale it down and try again.");
        }

        // Reserve the id under the lock, then do the slow work OUTSIDE it. Decoding, resizing and
        // WebP-encoding a large scan takes long enough to be visible, and every read on this store
        // (including the player overlay's own re-render, which asks for the picture currently on
        // the projector) takes the same lock — holding it across the encode freezes the beamer.
        string cleaned = (title ?? string.Empty).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = "Untitled";
        }

        string id;
        lock (gate)
        {
            // Reservations count towards the cap: two uploads in flight must not both slip under it.
            if (uploads.Count + reservedIds.Count >= ArtUploadRules.MaxUploads)
            {
                return Refused(
                    $"There are already {ArtUploadRules.MaxUploads} uploaded pictures. " +
                    "Remove one before adding another.");
            }

            id = UniqueId(cleaned);
            reservedIds.Add(id);
        }

        string relative = $"{UploadsDirectoryName}/{id}.webp";
        int width;
        int height;
        try
        {
            Directory.CreateDirectory(uploadsRoot);
            using Image decoded = Image.Load(content);
            decoded.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(ArtUploadRules.MaxEdgePixels, ArtUploadRules.MaxEdgePixels),
                Mode = ResizeMode.Max,
            }));

            // Metadata is dropped rather than carried over: an upload can be a phone photo, and
            // the projector has no use for its GPS tag.
            decoded.Metadata.ExifProfile = null;
            decoded.Metadata.XmpProfile = null;
            decoded.Metadata.IptcProfile = null;

            decoded.Save(Path.Combine(artRoot, relative.Replace('/', Path.DirectorySeparatorChar)),
                new WebpEncoder());
            width = decoded.Width;
            height = decoded.Height;
        }
        catch (UnknownImageFormatException)
        {
            return ReleaseAndRefuse(id, $"That file could not be read as a {ArtUploadRules.AcceptedFormats} image.");
        }
        catch (InvalidImageContentException)
        {
            return ReleaseAndRefuse(id, "That image is damaged and could not be read.");
        }
        catch (InvalidMemoryOperationException)
        {
            // ImageSharp refused the allocation the decode asked for. The dimension check above
            // makes this unlikely, but an unhandled throw here would kill the DM's circuit rather
            // than tell them the file is unusable.
            return ReleaseAndRefuse(id, "That image is too large to process.");
        }
        catch (IOException error)
        {
            return ReleaseAndRefuse(id, $"The picture could not be saved: {error.Message}");
        }
        catch (UnauthorizedAccessException error)
        {
            return ReleaseAndRefuse(id, $"The picture could not be saved: {error.Message}");
        }

        ArtImage image = new ArtImage
        {
            Id = id,
            File = relative,
            Title = cleaned,
            Subject = string.Empty,
            Kind = ArtKind.Handout,
            Origin = ArtOrigin.Upload,
            Source = string.Empty,
            SourceLocation = string.Empty,
            Tags = new[] { "upload" },
            NodeIds = Array.Empty<string>(),
            Width = width,
            Height = height,
        };

        lock (gate)
        {
            reservedIds.Remove(id);
            uploads.Add(image);
            Result saved = SaveUploads();
            if (!saved.IsValid)
            {
                // The bytes are on disk but the index that finds them is not, so the picture would
                // vanish on the next restart. Undo rather than report a success that half happened.
                uploads.Remove(image);
                DeleteFile(image);
                return Result.WithMessages<ArtImage>(saved.Messages.ToArray());
            }

            Reindex();
        }

        Changed?.Invoke();
        return Result.For(image);
    }

    /// <inheritdoc />
    public Result Remove(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
        {
            return Result.WithMessages(ValidationMessage.Error("An image id is required.", nameof(imageId)));
        }

        lock (gate)
        {
            ArtImage image = uploads.Find(candidate => string.Equals(candidate.Id, imageId, StringComparison.Ordinal));
            if (image is null)
            {
                // Covers both "no such id" and "that is committed book art": either way the answer
                // is that this is not something the app is allowed to delete.
                return Result.WithMessages(ValidationMessage.Error(
                    $"'{imageId}' is not an uploaded picture, so it cannot be removed.", nameof(imageId)));
            }

            uploads.Remove(image);
            Result saved = SaveUploads();
            if (!saved.IsValid)
            {
                uploads.Add(image);
                return saved;
            }

            DeleteFile(image);
            Reindex();
        }

        // Outside the lock, like SessionState does: a handler that re-renders must not run while
        // this store is still holding the lock its render will need.
        Changed?.Invoke();
        return Result.OK;
    }

    // ---- Loading and indexing ----------------------------------------------------------------

    // A document that will not parse is skipped rather than thrown: a hand-edited catalogue must
    // not stop the app serving the map, exactly as for the dossier and stat stores. What is skipped or
    // dropped is reported.
    private static void Load(string path, ArtOrigin origin, List<ArtImage> into, List<ContentProblem> problems)
    {
        if (!File.Exists(path))
        {
            return;
        }

        ArtCatalogue catalogue;
        string reason = string.Empty;
        try
        {
            string json = File.ReadAllText(path);
            catalogue = JsonSerializer.Deserialize<ArtCatalogue>(json, Options);
            problems.AddRange(NullEntries.Described(json).Select(entry => new ContentProblem(path, entry)));
        }
        catch (JsonException error)
        {
            catalogue = null;
            reason = error.Message;
        }
        catch (IOException error)
        {
            catalogue = null;
            reason = error.Message;
        }
        catch (UnauthorizedAccessException error)
        {
            catalogue = null;
            reason = error.Message;
        }

        if (catalogue is null)
        {
            problems.Add(ContentProblem.Unreadable(path, reason.Length > 0 ? reason : "it holds nothing but null"));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < catalogue.Images.Count; i++)
        {
            ArtImage image = catalogue.Images[i];
            if (string.IsNullOrEmpty(image.Id) || string.IsNullOrEmpty(image.File))
            {
                problems.Add(new ContentProblem(path, $"images[{i}] ('{image.Title}') has no id or no file, so the app dropped it."));
                continue;
            }

            if (!seen.Add(image.Id))
            {
                problems.Add(new ContentProblem(path, $"the picture '{image.Id}' is listed twice; the app keeps the later one."));
            }

            // The document's own origin is not trusted: which file it was read from decides, so an
            // upload cannot claim to be committed book art and dodge Remove's guard.
            into.Add(origin == image.Origin ? image : Retag(image, origin));
        }
    }

    private static ArtImage Retag(ArtImage image, ArtOrigin origin) => new ArtImage
    {
        Id = image.Id,
        File = image.File,
        Title = image.Title,
        Subject = image.Subject,
        Kind = image.Kind,
        Origin = origin,
        Source = image.Source,
        SourceLocation = image.SourceLocation,
        Tags = image.Tags,
        NodeIds = image.NodeIds,
        Width = image.Width,
        Height = image.Height,
    };

    // Caller holds gate.
    private void Reindex()
    {
        Dictionary<string, ArtImage> ids = new Dictionary<string, ArtImage>(StringComparer.Ordinal);
        Dictionary<string, List<ArtImage>> nodes = new Dictionary<string, List<ArtImage>>(StringComparer.Ordinal);

        foreach (ArtImage image in book.Concat(uploads))
        {
            // Last one wins, matching the dossier store: an upload may deliberately shadow a
            // catalogued id, and a duplicate inside one document is an authoring slip either way.
            ids[image.Id] = image;
        }

        List<ArtImage> all = ids.Values
            .OrderBy(image => image.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(image => image.Id, StringComparer.Ordinal)
            .ToList();

        foreach (ArtImage image in all)
        {
            foreach (string node in image.NodeIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(node))
                {
                    continue;
                }

                if (!nodes.TryGetValue(node, out List<ArtImage> images))
                {
                    images = new List<ArtImage>();
                    nodes[node] = images;
                }

                images.Add(image);
            }
        }

        byId = ids;
        ordered = all;
        byNode = nodes;
    }

    // Lower rank sorts first; -1 means no match. Mirrors the dossier/stat search convention.
    private static int Rank(ArtImage image, string needle)
    {
        string title = image.Title.ToLowerInvariant();
        if (string.Equals(title, needle, StringComparison.Ordinal))
        {
            return 0;
        }

        if (title.StartsWith(needle, StringComparison.Ordinal))
        {
            return 1;
        }

        if (title.Contains(needle, StringComparison.Ordinal))
        {
            return 2;
        }

        if ((image.Tags ?? Array.Empty<string>()).Any(tag => tag.Contains(needle, StringComparison.OrdinalIgnoreCase)))
        {
            return 3;
        }

        return image.Subject.Contains(needle, StringComparison.OrdinalIgnoreCase) ? 4 : -1;
    }

    // ---- Upload plumbing ---------------------------------------------------------------------

    // Caller holds gate.
    private string UniqueId(string title)
    {
        string stem = Slug(title);
        string candidate = stem;
        int suffix = 2;
        while (byId.ContainsKey(candidate)
            || uploads.Exists(image => image.Id == candidate)
            || reservedIds.Contains(candidate))
        {
            candidate = $"{stem}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    // The id becomes a file name under the art root, so it is built from an allowlist rather than
    // by removing what looks dangerous: no separators, no dots, nothing a traversal could use.
    private static string Slug(string title)
    {
        var slug = new System.Text.StringBuilder("upload-");
        bool lastWasDash = true;
        foreach (char character in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(character);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                slug.Append('-');
                lastWasDash = true;
            }
        }

        string cleaned = slug.ToString().TrimEnd('-');
        return cleaned.Length > "upload-".Length ? cleaned[..Math.Min(cleaned.Length, 60)] : "upload-untitled";
    }

    // Caller holds gate.
    private Result SaveUploads()
    {
        string path = Path.Combine(uploadsRoot, CatalogueFileName);
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(uploadsRoot);
            File.WriteAllText(temp, JsonSerializer.Serialize(new ArtCatalogue { Images = uploads.ToArray() }, Options));
            File.Move(temp, path, overwrite: true);
            return Result.OK;
        }
        catch (IOException error)
        {
            return Result.WithMessages(ValidationMessage.Error(
                $"The uploaded-art catalogue could not be saved: {error.Message}", UploadsProperty));
        }
        catch (UnauthorizedAccessException error)
        {
            return Result.WithMessages(ValidationMessage.Error(
                $"The uploaded-art catalogue could not be saved: {error.Message}", UploadsProperty));
        }
    }

    private void DeleteFile(ArtImage image)
    {
        try
        {
            File.Delete(Path.Combine(artRoot, image.File.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (IOException)
        {
            // The file is locked or already gone; the catalogue is what the app reads from, and it
            // no longer lists this picture.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }

    private static Result<ArtImage> Refused(string why) =>
        Result.WithMessages<ArtImage>(ValidationMessage.Error(why, "content"));

    // Gives back an id claimed before the encode, so a failed upload does not permanently consume
    // a slot against MaxUploads or block that title from being used again.
    private Result<ArtImage> ReleaseAndRefuse(string id, string why)
    {
        lock (gate)
        {
            reservedIds.Remove(id);
        }

        return Refused(why);
    }

    /// <summary>
    /// Reads the declared pixel dimensions from the image header without decoding it.
    /// </summary>
    /// <param name="content">The uploaded bytes.</param>
    /// <param name="width">The declared width, or zero when the header could not be read.</param>
    /// <param name="height">The declared height, or zero when the header could not be read.</param>
    /// <returns><c>null</c> when the header was read, otherwise the refusal to return.</returns>
    private static Result<ArtImage> Measure(byte[] content, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            ImageInfo info = Image.Identify(content);
            width = info.Width;
            height = info.Height;
            return null;
        }
        catch (UnknownImageFormatException)
        {
            return Refused($"That file could not be read as a {ArtUploadRules.AcceptedFormats} image.");
        }
        catch (InvalidImageContentException)
        {
            return Refused("That image is damaged and could not be read.");
        }
    }
}
