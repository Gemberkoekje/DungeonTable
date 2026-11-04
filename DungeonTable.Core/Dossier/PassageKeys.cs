using System.Globalization;
using System.Text;

namespace DungeonTable.Core.Dossier;

/// <summary>
/// Builds the stable keys passages are found, opened and pinned by. A key is made of where the block
/// sits and its heading (or its id, when it has one), never of its place in the list, so inserting a
/// rules section above another does not move a pin onto the wrong one. Both the text index and the DM
/// screen's pin buttons build keys here, so they cannot drift apart.
/// </summary>
public static class PassageKeys
{
    /// <summary>The key of a floor's "what dwells here" overview.</summary>
    /// <param name="levelNodeId">The level's node id.</param>
    /// <returns>The key.</returns>
    public static string LevelOverview(string levelNodeId) => $"level/{levelNodeId}/what-dwells-here";

    /// <summary>The key of a floor's aftermath.</summary>
    /// <param name="levelNodeId">The level's node id.</param>
    /// <returns>The key.</returns>
    public static string LevelAftermath(string levelNodeId) => $"level/{levelNodeId}/aftermath";

    /// <summary>The keys of the rules reference's sections, in their order.</summary>
    /// <param name="sections">The sections.</param>
    /// <returns>One key per section.</returns>
    public static IReadOnlyList<string> Rules(IReadOnlyList<DossierBlock> sections) => Blocks("rules", sections);

    /// <summary>The keys of a floor's factions, in their order.</summary>
    /// <param name="levelNodeId">The level's node id.</param>
    /// <param name="factions">The level's faction blocks.</param>
    /// <returns>One key per faction.</returns>
    public static IReadOnlyList<string> Factions(string levelNodeId, IReadOnlyList<DossierBlock> factions) =>
        Blocks($"level/{levelNodeId}/factions", factions);

    /// <summary>The keys of a floor's wandering-monster blocks, in their order.</summary>
    /// <param name="levelNodeId">The level's node id.</param>
    /// <param name="blocks">The level's wandering-monster blocks.</param>
    /// <returns>One key per block.</returns>
    public static IReadOnlyList<string> WanderingMonsters(string levelNodeId, IReadOnlyList<DossierBlock> blocks) =>
        Blocks($"level/{levelNodeId}/wandering", blocks);

    /// <summary>
    /// The keys of a list of blocks under one scope: the scope, then the block's id or else its heading
    /// as a slug. Two blocks that would share a key get "-2", "-3" and so on, in their order.
    /// </summary>
    /// <param name="scope">Where the blocks sit ("rules", "area/data_dossiers_level_1_area_2/detail").</param>
    /// <param name="blocks">The blocks.</param>
    /// <returns>One key per block, in the same order.</returns>
    public static IReadOnlyList<string> Blocks(string scope, IReadOnlyList<DossierBlock> blocks)
    {
        IReadOnlyList<DossierBlock> list = blocks ?? Array.Empty<DossierBlock>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var keys = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            keys[i] = Unique($"{scope}/{Slug(NameOf(list[i]))}", used);
        }

        return keys;
    }

    /// <summary>
    /// A key that is not in <paramref name="used"/> yet: <paramref name="key"/> itself, or it with "-2",
    /// "-3" and so on. The key returned is added to the set.
    /// </summary>
    /// <param name="key">The key wanted.</param>
    /// <param name="used">The keys already given out.</param>
    /// <returns>The key to use.</returns>
    public static string Unique(string key, ISet<string> used)
    {
        ArgumentNullException.ThrowIfNull(used);
        string candidate = key;
        int n = 1;
        while (!used.Add(candidate))
        {
            n++;
            candidate = $"{key}-{n.ToString(CultureInfo.InvariantCulture)}";
        }

        return candidate;
    }

    // What names a block in its key: its id when it has one, else its heading.
    private static string NameOf(DossierBlock block)
    {
        if (block is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(block.Id) ? block.Heading : block.Id;
    }

    /// <summary>
    /// A heading as a key part: lower case, letters and digits kept, an apostrophe dropped, anything else
    /// a single dash ("Grask's crew" → "grasks-crew", "1-4: Goblin Fetchers" → "1-4-goblin-fetchers").
    /// </summary>
    /// <param name="text">The heading or id.</param>
    /// <returns>The slug; "untitled" when nothing is left.</returns>
    public static string Slug(string text)
    {
        var slug = new StringBuilder();
        bool dash = false;
        foreach (char c in text ?? string.Empty)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (dash && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(char.ToLowerInvariant(c));
                dash = false;
            }
            else if (c != '\'' && c != '’')
            {
                dash = true;
            }
        }

        return slug.Length > 0 ? slug.ToString() : "untitled";
    }
}
