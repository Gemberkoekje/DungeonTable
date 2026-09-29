using System.Linq;
using System.Text.Json.Serialization;

namespace DungeonTable.Core.Battle;

/// <summary>
/// One creature in a running fight — "Bugbear 1", "Rurik", a druid's wolf. A monster group shares a
/// single <see cref="InitiativeEntry"/> but every member is still its own combatant, because HP,
/// conditions and concentration are tracked per creature.
/// </summary>
/// <remarks>
/// Unlike most of <c>DungeonTable.Core</c> this is a <b>mutable</b> POCO: a combatant changes
/// constantly during a fight, and rebuilding an immutable one per point of damage would be noise.
/// It is owned exclusively by <c>BattleState</c>, which mutates it under its lock and hands callers
/// <see cref="Copy"/>-ed snapshots — so nothing outside that service ever mutates a live combatant.
/// Collection properties are swapped wholesale (never mutated in place) to keep a snapshot's copy
/// safe to read while the original changes.
/// </remarks>
public sealed class Combatant
{
    /// <summary>Stable id within the battle.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>What this combatant is (player / monster / ally).</summary>
    public CombatantKind Kind { get; init; } = CombatantKind.None;

    /// <summary>
    /// The stat-library node id backing this combatant ("data_statblocks_monsters_bugbear"), or an empty string when it
    /// has none — a fight must never be blocked by a missing extraction, so an unstatted combatant
    /// simply carries whatever HP and AC the DM types in.
    /// </summary>
    public string StatBlockNodeId { get; init; } = string.Empty;

    /// <summary>
    /// The "1" in "Bugbear 1": this combatant's number within its type for this battle. Numbering is
    /// stable — removing Bugbear 2 never renumbers the rest. 0 for a combatant that is not numbered
    /// (a player, a named ally).
    /// </summary>
    public int Ordinal { get; init; }

    /// <summary>Display name, editable by the DM ("Bugbear 2 (captain)").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Current hit points; clamped to 0..<see cref="MaxHp"/> by the battle service.</summary>
    public int CurrentHp { get; set; }

    /// <summary>Maximum hit points; 0 when unknown (no stat block and nothing typed in).</summary>
    public int MaxHp { get; set; }

    /// <summary>Armour class; 0 when unknown.</summary>
    public int ArmourClass { get; set; }

    /// <summary>Passive Perception; 0 when unknown.</summary>
    public int PassivePerception { get; set; }

    /// <summary>The conditions currently on this combatant.</summary>
    public IReadOnlyList<ConditionKind> Conditions { get; set; } = Array.Empty<ConditionKind>();

    /// <summary>True while the combatant is concentrating on a spell.</summary>
    public bool Concentrating { get; set; }

    /// <summary>What it is concentrating on ("hold person"), or an empty string.</summary>
    public string ConcentrationNote { get; set; } = string.Empty;

    /// <summary>A free-text DM note for the row ("fled through the east door").</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Spell slots burned this fight, per level.</summary>
    public IReadOnlyList<SpentSlot> SpentSlots { get; set; } = Array.Empty<SpentSlot>();

    /// <summary>
    /// True when the combatant is at 0 hit points: it is skipped in the turn order but stays in the
    /// list, so it can be healed back up or removed. Derived rather than stored, so healing a downed
    /// creature can never leave a stale "down" flag behind. A combatant whose maximum is unknown
    /// (<see cref="MaxHp"/> 0) is never down — its blank HP means "untracked", not "dead".
    /// </summary>
    /// <remarks>
    /// Not persisted: this type is stored as-is in the session document (see <c>BattleSnapshot</c>),
    /// and writing a derived flag into it would leave a "down" in the database that disagrees with the
    /// hit points beside it the moment the rule changes.
    /// </remarks>
    [JsonIgnore]
    public bool Down => MaxHp > 0 && CurrentHp <= 0;

    /// <summary>
    /// True when the combatant is at or below half its maximum hit points and still up — the
    /// "bloodied" state the initiative list colour-codes.
    /// </summary>
    /// <remarks>Derived, and not persisted — see <see cref="Down"/>.</remarks>
    [JsonIgnore]
    public bool Bloodied => MaxHp > 0 && CurrentHp > 0 && (CurrentHp * 2) <= MaxHp;

    /// <summary>
    /// Takes a snapshot of this combatant, so a caller can read it while the battle carries on
    /// changing the original.
    /// </summary>
    /// <returns>An independent copy.</returns>
    public Combatant Copy() => new Combatant
    {
        Id = Id,
        Kind = Kind,
        StatBlockNodeId = StatBlockNodeId,
        Ordinal = Ordinal,
        Name = Name,
        CurrentHp = CurrentHp,
        MaxHp = MaxHp,
        ArmourClass = ArmourClass,
        PassivePerception = PassivePerception,
        Conditions = Conditions.ToArray(),
        Concentrating = Concentrating,
        ConcentrationNote = ConcentrationNote,
        Notes = Notes,
        SpentSlots = SpentSlots.ToArray(),
    };
}
