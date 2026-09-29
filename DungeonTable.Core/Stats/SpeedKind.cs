namespace DungeonTable.Core.Stats;

/// <summary>Classifies one of a creature's movement modes.</summary>
public enum SpeedKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>Ordinary ground speed.</summary>
    Walk = 1,

    /// <summary>Flying speed.</summary>
    Fly = 2,

    /// <summary>Swimming speed.</summary>
    Swim = 3,

    /// <summary>Climbing speed.</summary>
    Climb = 4,

    /// <summary>Burrowing speed.</summary>
    Burrow = 5,
}
