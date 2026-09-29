namespace DungeonTable.Core.Briefing;

/// <summary>
/// Classifies a revealable map feature marker. Features are drawn on the DM view always but only
/// appear on the player view once individually revealed.
/// </summary>
public enum FeatureKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>A mechanical or magical trap.</summary>
    Trap = 1,

    /// <summary>A hidden door that must be discovered.</summary>
    HiddenDoor = 2,

    /// <summary>A secret passage or concealed area.</summary>
    Secret = 3,

    /// <summary>An environmental hazard (pit, ooze, gas, etc.).</summary>
    Hazard = 4,
}
