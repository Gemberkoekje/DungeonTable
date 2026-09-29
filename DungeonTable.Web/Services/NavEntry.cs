using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// One stop in the DM's reference history: an area (which drives the briefing panel) or an entity —
/// monster, spell, item, NPC, faction — (which opens the reference drawer over the area it was
/// reached from).
/// </summary>
/// <param name="NodeId">The node id of the target.</param>
/// <param name="Kind">What the target is; <see cref="CrossRefKind.Area"/> drives the panel.</param>
/// <param name="Label">The display label, shown on the breadcrumb chip.</param>
public readonly record struct NavEntry(string NodeId, CrossRefKind Kind, string Label);
