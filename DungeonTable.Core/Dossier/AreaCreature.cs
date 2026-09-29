namespace DungeonTable.Core.Dossier;

/// <summary>
/// One entry of an area's authored creature list: who is in the room, and how many. It is what the
/// encounter strip and the briefing's occupant chips are built from, instead of from names found in
/// the prose.
/// </summary>
/// <remarks>
/// A mention in text is not an occupant. "Disguised as vampires" names the vampire stat block without
/// putting a vampire in the room, and a count read off the word before a name gets "two of the
/// bugbears" wrong. Listing the creatures is how an author says what the prose only implies.
/// </remarks>
public sealed class AreaCreature
{
    /// <summary>
    /// Who: a stat block by the slug of its name (<c>bandit</c>), or an entity, NPC or player character
    /// by id (<c>bell-warden</c>, <c>brother-aldous</c>). Written the way a link target is.
    /// </summary>
    public string Ref { get; init; } = string.Empty;

    /// <summary>
    /// How many, as written: a number ("4") or a dice expression the DM rolls at the table ("1d4+1").
    /// Empty means one. In the JSON it may be a bare number or a string.
    /// </summary>
    public string Count { get; init; } = string.Empty;

    /// <summary>What to remember about them here ("chained to the anvil"), in prose.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>True when the players must not know they are here yet: an ambush, something invisible.</summary>
    public bool Secret { get; init; }
}
