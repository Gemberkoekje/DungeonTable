namespace DungeonTable.Web.Services;

/// <summary>What a click did to a region outline being drawn corner by corner.</summary>
public enum RegionDraftStep
{
    /// <summary>Nothing was asked of the draft.</summary>
    None,

    /// <summary>The click became the outline's next corner.</summary>
    Added,

    /// <summary>
    /// The click repeated a corner of an outline that cannot close yet, or was not a point at all,
    /// and changed nothing.
    /// </summary>
    Ignored,

    /// <summary>
    /// The click landed on the first corner, or on the last one again, of an outline that encloses
    /// something: the outline is finished and ready to become a region.
    /// </summary>
    Closed,
}
