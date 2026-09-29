namespace DungeonTable.Core.Dossier;

/// <summary>
/// Classifies a block of a room dossier, so the DM panel can style and order it (an
/// at-a-glance exit line, a feature bullet, a loot list, a secret-only note, or flavour lore).
/// </summary>
public enum DossierBlockKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>How to leave the area (exits, adjacent tunnels).</summary>
    Exits = 1,

    /// <summary>A notable feature of the area (furnishing, terrain, decoration).</summary>
    Feature = 2,

    /// <summary>Treasure, salvage, or other findable items.</summary>
    Loot = 3,

    /// <summary>DM-only information the players must not be told outright (secret doors, ambushes).</summary>
    Secret = 4,

    /// <summary>Background flavour, history, or read-aloud-adjacent colour.</summary>
    Lore = 5,
}
