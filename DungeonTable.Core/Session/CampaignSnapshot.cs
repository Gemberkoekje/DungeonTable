namespace DungeonTable.Core.Session;

/// <summary>
/// The campaign-scoped state of the one live table: how far each quest has got, which cards have
/// already been dealt from each deck, what has been done in each area, and what the DM pinned. None of
/// it is derivable from the committed data — this is what the table has done.
/// </summary>
/// <remarks>
/// One snapshot type covers them all because they persist together and none is big enough to earn
/// its own document. <see cref="Decks"/> replaced a single list of one deck's drawn card ids when
/// decks became content; a document that still carries that list restores with nothing drawn.
/// </remarks>
public sealed class CampaignSnapshot
{
    /// <summary>Per-quest progress; quests with no entry have never been touched.</summary>
    public IReadOnlyList<QuestProgress> Quests { get; set; } = Array.Empty<QuestProgress>();

    /// <summary>
    /// Per-deck drawn cards, for the decks that keep what they deal out. A kept deck is a set of
    /// handouts the players keep, so "already drawn" is real table state and a redraw would hand out
    /// a duplicate. Decks with no entry have had nothing drawn.
    /// </summary>
    public IReadOnlyList<DeckProgress> Decks { get; set; } = Array.Empty<DeckProgress>();

    /// <summary>
    /// Per-area progress: which creatures have been dealt with and what the DM wrote down. Areas
    /// with no entry have never been touched, which is not the same as an entry with nothing ticked.
    /// </summary>
    public IReadOnlyList<AreaProgress> Areas { get; set; } = Array.Empty<AreaProgress>();

    /// <summary>
    /// What the DM pinned along the foot of the dock, oldest first: <c>card:</c> and a card's id, or
    /// <c>text:</c> and a passage's key. Added after the rest, with an empty default, so an older build
    /// reads the document and loses only the pins.
    /// </summary>
    public IReadOnlyList<string> Pins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The creatures the DM moved from one area to another, in the order they were moved. Added after the
    /// rest, with an empty default, so an older build reads the document and loses only the moves.
    /// </summary>
    public IReadOnlyList<CreatureMove> Moves { get; set; } = Array.Empty<CreatureMove>();
}
