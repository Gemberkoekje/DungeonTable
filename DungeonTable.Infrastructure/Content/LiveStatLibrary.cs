using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The <see cref="IStatLibrary"/> the pages see: every call is answered by the stat library of the
/// content showing now (<see cref="LiveContent.Current"/>).
/// </summary>
public sealed class LiveStatLibrary : IStatLibrary
{
    private readonly LiveContent live;

    /// <summary>Creates the library over the live content.</summary>
    /// <param name="live">The live content.</param>
    public LiveStatLibrary(LiveContent live)
    {
        ArgumentNullException.ThrowIfNull(live);
        this.live = live;
    }

    private FileSystemStatLibrary Now => live.Current.Stats;

    /// <inheritdoc />
    public Result<StatBlock> GetMonster(string nodeId) => Now.GetMonster(nodeId);

    /// <inheritdoc />
    public Result<SpellEntry> GetSpell(string nodeId) => Now.GetSpell(nodeId);

    /// <inheritdoc />
    public IReadOnlyList<StatBlock> AllMonsters() => Now.AllMonsters();

    /// <inheritdoc />
    public IReadOnlyList<SpellEntry> AllSpells() => Now.AllSpells();

    /// <inheritdoc />
    public bool HasMonster(string nodeId) => Now.HasMonster(nodeId);

    /// <inheritdoc />
    public IReadOnlyList<SearchMatch> SearchMonsters(string query, int limit) => Now.SearchMonsters(query, limit);
}
