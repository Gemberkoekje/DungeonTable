namespace DungeonTable.Core.Dossier;

/// <summary>
/// One individual, faction or item that matters on a floor: something the prose can link to
/// (<c>[[bell-warden]]</c>), an area can list among its creatures, and a card can describe. Authored
/// in the level file that owns it, beside the areas.
/// </summary>
/// <remarks>
/// <para>
/// An individual <em>uses</em> a stat block and never <em>is</em> one. The gargoyle that guards the
/// nave is <c>bell-warden</c>, whose <see cref="StatBlock"/> is <c>gargoyle</c>: it shares the numbers
/// every gargoyle has, but not their story, so nothing said about it can leak onto another floor's
/// gargoyles.
/// </para>
/// <para>
/// A campaign-wide person lives in the NPC roster or the party instead, which already give ids. A
/// faction may equally be declared by giving one of the level's faction blocks an id; see
/// <see cref="LevelDossier.AllEntities"/>.
/// </para>
/// </remarks>
public sealed class Entity
{
    /// <summary>Stable slug, unique among everything a link can name ("bell-warden", "wickfoot-goblins").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>What the entity is.</summary>
    public EntityKind Kind { get; init; } = EntityKind.None;

    /// <summary>The name to show ("the Bell-Warden").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The stat block the entity uses, named the way a link names one: the slug of its name
    /// (<c>gargoyle</c>). Empty when it fights with none, as a faction never does.
    /// </summary>
    public string StatBlock { get; init; } = string.Empty;

    /// <summary>What the DM needs to know about it, in prose; links in it resolve on the entity's floor.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True when the players must not learn of it, as for a secret dossier block.</summary>
    public bool Secret { get; init; }
}
