using DungeonTable.Infrastructure.Dossiers;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Rosters;

/// <summary>
/// Shared read/write plumbing for the two roster documents (<c>party.json</c>, <c>allies.json</c>).
/// Both are small, in-app-edited JSON files with identical needs, so the fail-soft read and the
/// crash-safe write live here once rather than twice.
/// </summary>
internal static class RosterDocument
{
    /// <summary>
    /// The JSON contract both roster documents are written with: camelCase, string enums. A seed file
    /// is hand-edited, so a <c>null</c> reads as if the field were left out and a <c>null</c> member is
    /// dropped, as in every other document; none of it changes a byte the editors write.
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        TypeInfoResolver = NullMeansLeftOut.Resolver(),
        Converters = { new NullTolerantValueConverter(), new LenientListConverter(), new LenientStringConverter() },
    };

    /// <summary>
    /// Reads a roster document. A file that has never been written yields <paramref name="empty"/>
    /// as a <em>valid</em> result — a fresh install has no roster yet, and that is not an error. A
    /// file that exists but cannot be read or parsed yields an error result, so a corrupt roster is
    /// reported rather than silently replaced by an empty one on the next save.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="path">Full path to the document.</param>
    /// <param name="empty">The value to return when the document does not exist.</param>
    /// <param name="what">What the document holds, for the error message ("party roster").</param>
    /// <returns>The parsed document, the empty value, or an error result.</returns>
    internal static Result<T> Read<T>(string path, T empty, string what)
        where T : class
    {
        if (!File.Exists(path))
        {
            return Result.For(empty);
        }

        try
        {
            string json = File.ReadAllText(path);
            T parsed = JsonSerializer.Deserialize<T>(json, Options);
            return parsed is null
                ? Result.WithMessages<T>(ValidationMessage.Error($"The {what} at '{path}' is empty.", nameof(path)))
                : Result.For(parsed);
        }
        catch (JsonException error)
        {
            return Result.WithMessages<T>(
                ValidationMessage.Error($"The {what} at '{path}' could not be read: {error.Message}", nameof(path)));
        }
        catch (IOException error)
        {
            return Result.WithMessages<T>(
                ValidationMessage.Error($"The {what} at '{path}' could not be read: {error.Message}", nameof(path)));
        }
        catch (UnauthorizedAccessException error)
        {
            return Result.WithMessages<T>(
                ValidationMessage.Error($"The {what} at '{path}' could not be read: {error.Message}", nameof(path)));
        }
    }

    /// <summary>
    /// Writes a roster document atomically: serialize to a uniquely named temporary file beside the
    /// target, then move it over the target. A crash mid-write therefore leaves the previous roster
    /// intact instead of a half-written file — this is the DM's own party, and losing it to a
    /// power cut mid-save would be a real loss.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="path">Full path to the document.</param>
    /// <param name="value">The document to write.</param>
    /// <param name="what">What the document holds, for the error message ("party roster").</param>
    /// <returns><see cref="Result.OK"/> on success, or an error result.</returns>
    internal static Result Write<T>(string path, T value, string what)
        where T : class
    {
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            File.Move(temp, path, overwrite: true);
            return Result.OK;
        }
        catch (JsonException error)
        {
            return Fail(temp, what, path, error.Message);
        }
        catch (IOException error)
        {
            return Fail(temp, what, path, error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            return Fail(temp, what, path, error.Message);
        }
    }

    // Drop the half-written temp file (best effort - a leftover .tmp is harmless but untidy) and
    // report why the save failed.
    private static Result Fail(string temp, string what, string path, string reason)
    {
        try
        {
            File.Delete(temp);
        }
        catch (IOException)
        {
            // The temp file is locked or already gone; nothing useful to do about it here.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }

        return Result.WithMessages(
            ValidationMessage.Error($"The {what} could not be saved to '{path}': {reason}", nameof(path)));
    }
}
