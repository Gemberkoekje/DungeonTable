using System.Linq;

namespace DungeonTable.Core.Dossier;

/// <summary>
/// The floor-level dossier for a dungeon level: the "What Dwells Here" overview a DM reads once
/// per session (design tier, resident factions, wandering-monster encounters), distinct from the
/// per-area dossiers.
/// </summary>
public sealed class LevelDossier
{
    /// <summary>Node id of the level ("data_dossiers_level_1").</summary>
    public string LevelNodeId { get; init; } = string.Empty;

    /// <summary>The level title ("Level 1: The Undercroft").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Party tier the level targets ("Four 5th-level characters; clearing it advances them to 6th.").</summary>
    public string DesignedFor { get; init; } = string.Empty;

    /// <summary>The overview paragraph summarising who and what inhabits the level.</summary>
    public string WhatDwellsHere { get; init; } = string.Empty;

    /// <summary>The resident factions and their write-ups (Grask's crew, the Wickfoot goblins).</summary>
    public IReadOnlyList<DossierBlock> Factions { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>The wandering-monster encounters offered for the level.</summary>
    public IReadOnlyList<DossierBlock> WanderingMonsters { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>
    /// What changes on this floor once the party has cleared it — the book's "Aftermath" section at
    /// the end of every level chapter. Empty when none is authored.
    /// </summary>
    public string Aftermath { get; init; } = string.Empty;

    /// <summary>
    /// The individuals, factions and items that belong to this floor, beyond its factions (see
    /// <see cref="AllEntities"/>): the ones prose links to and areas list among their creatures.
    /// </summary>
    public IReadOnlyList<Entity> Entities { get; init; } = Array.Empty<Entity>();

    /// <summary>
    /// The cross-connections on this floor: who is at odds with whom, who serves or fears whom. The
    /// campaign-wide ones live in <see cref="RelationList"/> instead.
    /// </summary>
    public IReadOnlyList<Relation> Relations { get; init; } = Array.Empty<Relation>();

    /// <summary>
    /// Every entity this floor declares: its <see cref="Entities"/>, then each faction block that has
    /// an id, as a faction whose name is the block's heading and whose description is its body.
    /// </summary>
    /// <remarks>
    /// A faction's write-up already is its description, so giving the block an id is all it takes to
    /// make the faction linkable. Declaring it a second time in <see cref="Entities"/> would leave two
    /// copies to keep in step.
    /// </remarks>
    /// <returns>The entities, authored ones first (possibly empty).</returns>
    public IReadOnlyList<Entity> AllEntities()
    {
        IEnumerable<Entity> factions = Factions
            .Where(block => block is not null && !string.IsNullOrWhiteSpace(block.Id))
            .Select(block => new Entity
            {
                Id = block.Id.Trim(),
                Kind = EntityKind.Faction,
                Name = block.Heading,
                Description = block.Body,
                Secret = block.Secret,
            });

        return Entities.Where(entity => entity is not null).Concat(factions).ToArray();
    }
}
