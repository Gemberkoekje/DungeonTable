using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;

namespace DungeonTable.Web.Services;

/// <summary>
/// Everything a card shows, built for one id (<see cref="DmWorkspace.CardFor"/>): the card itself, what
/// the book adds under the same id, the stat block or spell beneath it, and the person when it is one of
/// the campaign's own. The card sheet shows the card the history is on and a pin shows its own, so a card
/// is worked out the same way wherever it is read.
/// </summary>
public sealed class CardContent
{
    /// <summary>The card; its <see cref="ReferenceCard.NodeId"/> is blank when nothing has the id.</summary>
    public ReferenceCard Card { get; init; } = new ReferenceCard();

    /// <summary>What the book index says under the same id, when the card is the campaign's own; else empty.</summary>
    public ReferenceCard BookCard { get; init; } = new ReferenceCard();

    /// <summary>The stat block shown on or beneath the card; empty when none.</summary>
    public StatBlock StatBlock { get; init; } = new StatBlock();

    /// <summary>The spell the card is; empty when it is not a spell.</summary>
    public SpellEntry SpellEntry { get; init; } = new SpellEntry();

    /// <summary>Where <see cref="StatBlock"/> is printed; empty when no block.</summary>
    public string StatBlockCitation { get; init; } = string.Empty;

    /// <summary>Where <see cref="SpellEntry"/> is printed; empty when no spell.</summary>
    public string SpellCitation { get; init; } = string.Empty;

    /// <summary>The NPC the card is; empty when it is not one of the roster's.</summary>
    public NpcDossier Npc { get; init; } = new NpcDossier();

    /// <summary>The player character the card is; empty when it is not one of the party.</summary>
    public CharacterDossier Character { get; init; } = new CharacterDossier();
}
