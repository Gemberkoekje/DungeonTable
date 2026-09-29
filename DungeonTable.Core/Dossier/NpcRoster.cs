namespace DungeonTable.Core.Dossier;

/// <summary>
/// Every named non-player character the campaign has accumulated, in the order they should be
/// shown. The sixth dossier tier, beside the per-area, per-floor, shared-rules, campaign and party
/// ones.
/// </summary>
/// <remarks>
/// Campaign-scoped, like the party and the quest log: a contact in the village is equally relevant on
/// floor one and floor twenty, so this loads once per circuit rather than on every navigation.
/// </remarks>
public sealed class NpcRoster
{
    /// <summary>The roster's display title ("The people they know").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>What this roster is, in a sentence.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>The NPCs, in the order they should be shown.</summary>
    public IReadOnlyList<NpcDossier> Npcs { get; init; } = Array.Empty<NpcDossier>();
}
