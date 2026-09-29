using System.Collections.Generic;
using System.IO;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Art;
using DungeonTable.Infrastructure.Art;
using SixLabors.ImageSharp;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for the committed art catalogue: every entry is kept, points at a picture the art route
/// serves, carries that picture's size, links only stat blocks that exist, and can be found; and no
/// committed picture is left out of it. Uploads are not checked: the app writes their entries itself.
/// </summary>
/// <remarks>
/// A file is compared by its exact name, casing and all. A Windows checkout finds a picture whatever the
/// case, but the server is Linux, where a wrong case is a blank frame on the projector.
/// </remarks>
internal static class ArtAudit
{
    // What Program.cs allows the /art route to serve. A picture of any other kind would 404.
    private static readonly HashSet<string> ServedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webp", ".png", ".jpg", ".jpeg",
    };

    /// <summary>Every picture file under the art folder, as a catalogue names it, uploads aside.</summary>
    /// <param name="artFolder">The art folder.</param>
    /// <returns>Their paths relative to the art folder, with forward slashes.</returns>
    internal static IReadOnlySet<string> PicturesOnDisk(string artFolder) =>
        Directory.EnumerateFiles(artFolder, "*", SearchOption.AllDirectories)
            .Where(path => ServedExtensions.Contains(Path.GetExtension(path)))
            .Select(path => Path.GetRelativePath(artFolder, path).Replace('\\', '/'))
            .Where(file => !file.StartsWith(FileSystemArtCatalogue.UploadsDirectoryName + "/", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Every entry the catalogue drops for having no id or no file, and every id given twice.</summary>
    /// <param name="catalogue">The committed catalogue, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Dropped(ArtCatalogue catalogue)
    {
        var faults = catalogue.Images
            .Select((image, i) => (image, i))
            .Where(entry => string.IsNullOrWhiteSpace(entry.image?.Id) || string.IsNullOrWhiteSpace(entry.image?.File))
            .Select(entry => $"images[{entry.i}] ('{entry.image?.Title}') has no id or no file, so the catalogue drops it.")
            .ToList();

        faults.AddRange(Check.Repeated(catalogue.Images.Select(image => image?.Id))
            .Select(id => $"Two pictures have the id '{id}': the catalogue keeps only the last."));
        return faults;
    }

    /// <summary>Every entry whose file is not a picture the art route would serve.</summary>
    /// <param name="catalogue">The committed catalogue, as written.</param>
    /// <param name="onDisk">The pictures under the art folder.</param>
    /// <returns>One sentence per entry (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unserved(ArtCatalogue catalogue, IReadOnlySet<string> onDisk) =>
        Kept(catalogue)
            .Select(image => UnservedFault(image, onDisk))
            .Where(fault => fault.Length > 0)
            .ToList();

    /// <summary>
    /// Every entry whose recorded size is not its picture's. The projector sizes its frame from these
    /// before the picture loads, so a stale size is a visible jump.
    /// </summary>
    /// <param name="catalogue">The committed catalogue, as written.</param>
    /// <param name="artFolder">The art folder.</param>
    /// <param name="onDisk">The pictures under the art folder.</param>
    /// <returns>One sentence per entry (empty when there are none).</returns>
    internal static IReadOnlyList<string> WrongSizes(ArtCatalogue catalogue, string artFolder, IReadOnlySet<string> onDisk)
    {
        var faults = new List<string>();
        foreach (ArtImage image in Kept(catalogue).Where(image => onDisk.Contains(image.File)))
        {
            string path = Path.Combine(artFolder, image.File.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                ImageInfo info = Image.Identify(path);
                if (info.Width != image.Width || info.Height != image.Height)
                {
                    faults.Add($"'{image.Id}' says {image.Width}x{image.Height}, and {image.File} is {info.Width}x{info.Height}.");
                }
            }
            catch (UnknownImageFormatException error)
            {
                faults.Add($"'{image.Id}': {image.File} cannot be read as a picture: {error.Message}");
            }
            catch (InvalidImageContentException error)
            {
                faults.Add($"'{image.Id}': {image.File} is damaged: {error.Message}");
            }
        }

        return faults;
    }

    /// <summary>
    /// Every node an entry links that is no stat block the library has. The links are what stage the
    /// art for the creatures in a fight, so a typo is a silent miss at the table.
    /// </summary>
    /// <param name="catalogue">The committed catalogue, as written.</param>
    /// <param name="stats">The loaded library.</param>
    /// <returns>One sentence per link (empty when there are none).</returns>
    internal static IReadOnlyList<string> UnknownNodes(ArtCatalogue catalogue, IStatLibrary stats) =>
        Kept(catalogue)
            .SelectMany(image => (image.NodeIds ?? Array.Empty<string>())
                .Where(node => !stats.HasMonster(node))
                .Select(node => $"'{image.Id}' links '{node}', which is no stat block the library has."))
            .ToList();

    /// <summary>Every entry the picker could not show or find: no title, no subject, no kind, no tags.</summary>
    /// <param name="catalogue">The committed catalogue, as written.</param>
    /// <returns>One sentence per entry (empty when there are none).</returns>
    internal static IReadOnlyList<string> Thin(ArtCatalogue catalogue) =>
        Kept(catalogue)
            .Where(image => string.IsNullOrWhiteSpace(image.Title) || string.IsNullOrWhiteSpace(image.Subject)
                || image.Kind == ArtKind.None || (image.Tags ?? Array.Empty<string>()).Count == 0)
            .Select(image => $"'{image.Id}' needs a title, a subject, a kind and at least one tag to be shown and found.")
            .ToList();

    /// <summary>Every committed picture no entry names: nobody can reach it through the picker.</summary>
    /// <param name="catalogue">The committed catalogue, as written.</param>
    /// <param name="onDisk">The pictures under the art folder.</param>
    /// <returns>One sentence per picture (empty when there are none).</returns>
    internal static IReadOnlyList<string> Orphans(ArtCatalogue catalogue, IReadOnlySet<string> onDisk)
    {
        var named = Kept(catalogue).Select(image => image.File).ToHashSet(StringComparer.Ordinal);
        return onDisk
            .Where(file => !named.Contains(file))
            .Order(StringComparer.Ordinal)
            .Select(file => $"{file} is in no catalogue entry, so nobody can show it.")
            .ToList();
    }

    private static string UnservedFault(ArtImage image, IReadOnlySet<string> onDisk)
    {
        if (!ServedExtensions.Contains(Path.GetExtension(image.File)))
        {
            return $"'{image.Id}' is {image.File}, and the art route does not serve {Path.GetExtension(image.File)} files.";
        }

        return onDisk.Contains(image.File)
            ? string.Empty
            : $"'{image.Id}' is {image.File}, and no picture has that name, casing and all.";
    }

    // The entries the catalogue keeps: the ones with an id and a file.
    private static IEnumerable<ArtImage> Kept(ArtCatalogue catalogue) =>
        catalogue.Images.Where(image => !string.IsNullOrWhiteSpace(image?.Id) && !string.IsNullOrWhiteSpace(image.File));
}
