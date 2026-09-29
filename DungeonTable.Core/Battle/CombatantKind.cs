namespace DungeonTable.Core.Battle;

/// <summary>
/// What a combatant is, which decides where its numbers come from and how the Battle tab groups
/// and colours it: players are seeded from the saved party, monsters from the stat library, allies
/// from the saved ally roster (or typed in as a one-off).
/// </summary>
public enum CombatantKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>A player character, seeded from the party roster.</summary>
    Player = 1,

    /// <summary>A monster or statted NPC the party is fighting.</summary>
    Monster = 2,

    /// <summary>A companion, familiar, hireling or summon fighting alongside the party.</summary>
    Ally = 3,
}
