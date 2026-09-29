namespace DungeonTable.Core.Briefing;

/// <summary>
/// The DM-facing briefing for a single room: who is in it and where it leads, as the area's authored
/// creatures and exits list them, and who those occupants are involved with.
/// </summary>
/// <remarks>
/// <para>
/// It carries no read-aloud text. That comes from the room's authored dossier
/// (<see cref="Dossier.AreaDossier.ReadAloud"/>) and nowhere else.
/// </para>
/// <para>
/// Both lists are the area's own, so an empty one means nobody is there, or no way out is worth
/// naming, rather than that nobody has written it down yet.
/// </para>
/// </remarks>
public sealed class RoomBriefing
{
    /// <summary>Node id of the room this briefing describes ("data_dossiers_level_1_area_6").</summary>
    public string RoomId { get; init; } = string.Empty;

    /// <summary>Human-readable room label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>The creatures and people the area lists, in the order it lists them.</summary>
    public IReadOnlyList<OccupantEntry> Occupants { get; init; } = Array.Empty<OccupantEntry>();

    /// <summary>The rooms and areas the area's exits lead to, in the order it lists them.</summary>
    public IReadOnlyList<ConnectionEntry> Connections { get; init; } = Array.Empty<ConnectionEntry>();

    /// <summary>
    /// Who the room's occupants are involved with, one entry per occupant that is an end of any
    /// relation. A kind of creature (four bandits) is never an end: relations are between particular
    /// ones.
    /// </summary>
    public IReadOnlyList<Involvement> Involvements { get; init; } = Array.Empty<Involvement>();
}
