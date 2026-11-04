using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Session;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// How far the campaign has got: which quest beats have been ticked, what each quest's status is,
/// which cards each deck has already dealt out, and what has been done in each area. Unlike the
/// dossiers, none of this is derivable from committed data — it is what happened at this table.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a <b>singleton</b> and persisted through the same coordinator as
/// <see cref="SessionState"/> and <see cref="BattleState"/>, whose concurrency discipline it copies
/// exactly: one private lock, accessors that hand out immutable copies rather than live objects, and
/// <see cref="Changed"/> always raised <em>outside</em> the lock (raising it inside would re-enter
/// through the DM circuit's re-render and deadlock).
/// </para>
/// <para>
/// It never blocks the DM. Prerequisites are <em>reported</em>, not enforced: an unmet prerequisite
/// styles a chip and nothing more, and every beat stays tickable. The party's real level lives on
/// character sheets this app has never seen, and a DM who has decided a quest is on the table is
/// right by definition.
/// </para>
/// </remarks>
public sealed class CampaignState
{
    private readonly object gate = new object();

    // Quest id -> its progress. Absent means "never touched", which is not the same as Available:
    // availability is computed from the authored prerequisites, progress is what the DM recorded.
    private readonly Dictionary<string, QuestProgress> progress =
        new Dictionary<string, QuestProgress>(StringComparer.Ordinal);

    // Deck id -> the cards dealt from it, in the order they were drawn. Only a deck that keeps its
    // drawn cards ever has an entry, and one reset back to full has none.
    private readonly Dictionary<string, List<string>> drawnCards =
        new Dictionary<string, List<string>>(StringComparer.Ordinal);

    // Area node id -> what has been done there. Absent means "never touched", which is why an area
    // with an empty note and nothing ticked is not stored at all.
    private readonly Dictionary<string, AreaProgress> areas =
        new Dictionary<string, AreaProgress>(StringComparer.Ordinal);

    // What the DM pinned along the foot of the dock, in the order they pinned it (see PinKeys).
    private readonly List<string> pins = new List<string>();

    // The creatures the DM moved from one area to another, oldest first (see CreatureMove).
    private readonly List<CreatureMove> moves = new List<CreatureMove>();

    private readonly IPartyRoster party;

    /// <summary>Creates the campaign state over the roster its level prerequisites read.</summary>
    /// <param name="party">The saved party roster, for character-level prerequisites.</param>
    public CampaignState(IPartyRoster party)
    {
        this.party = party;
    }

    /// <summary>Raised whenever quest progress, a deck's drawn cards, an area's progress, the moves or the pins change.</summary>
    public event Action Changed;

    // ---- Reading ----------------------------------------------------------------------------

    /// <summary>Where a quest stands, or <see cref="QuestStatus.None"/> when it has never been touched.</summary>
    /// <param name="questId">The quest's id.</param>
    /// <returns>The recorded status.</returns>
    public QuestStatus Status(string questId)
    {
        lock (gate)
        {
            return progress.TryGetValue(questId ?? string.Empty, out QuestProgress found)
                ? found.Status
                : QuestStatus.None;
        }
    }

    /// <summary>True when a beat has been ticked off.</summary>
    /// <param name="questId">The quest's id.</param>
    /// <param name="beatId">The beat's id, unique within its quest.</param>
    /// <returns><c>true</c> when the beat is done.</returns>
    public bool IsBeatDone(string questId, string beatId)
    {
        lock (gate)
        {
            return progress.TryGetValue(questId ?? string.Empty, out QuestProgress found)
                && found.CompletedBeatIds.Contains(beatId, StringComparer.Ordinal);
        }
    }

    /// <summary>How many of a quest's beats are ticked, for the card's progress line.</summary>
    /// <param name="quest">The quest to count.</param>
    /// <returns>The number of its beats that are done.</returns>
    public int CompletedBeats(Quest quest)
    {
        if (quest is null)
        {
            return 0;
        }

        lock (gate)
        {
            if (!progress.TryGetValue(quest.Id, out QuestProgress found))
            {
                return 0;
            }

            var done = new HashSet<string>(found.CompletedBeatIds, StringComparer.Ordinal);
            return quest.Beats.Count(beat => done.Contains(beat.Id));
        }
    }

    /// <summary>The cards already dealt from a deck, in the order they were drawn.</summary>
    /// <param name="deckId">The deck's id.</param>
    /// <returns>The drawn card ids; empty when nothing has been drawn from it.</returns>
    public IReadOnlyList<string> DrawnCardIds(string deckId)
    {
        lock (gate)
        {
            return drawnCards.TryGetValue(deckId ?? string.Empty, out List<string> ids)
                ? ids.ToArray()
                : Array.Empty<string>();
        }
    }

    /// <summary>True when a card has been dealt from a deck and not yet put back.</summary>
    /// <param name="deckId">The deck's id.</param>
    /// <param name="cardId">The card's id.</param>
    /// <returns><c>true</c> when it is spent.</returns>
    public bool IsCardDrawn(string deckId, string cardId)
    {
        lock (gate)
        {
            return drawnCards.TryGetValue(deckId ?? string.Empty, out List<string> ids)
                && ids.Contains(cardId ?? string.Empty, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The cards a draw can turn up: every card of a deck drawn fresh each time, and for a deck that
    /// keeps its drawn cards, the ones not dealt out yet.
    /// </summary>
    /// <param name="deck">The deck.</param>
    /// <returns>The cards still in the deck, in printed order.</returns>
    public IReadOnlyList<DeckCard> Undrawn(CardDeck deck)
    {
        if (deck is null)
        {
            return Array.Empty<DeckCard>();
        }

        lock (gate)
        {
            return UndrawnLocked(deck);
        }
    }

    // ---- Areas: what has been dealt with, and what the DM wrote down --------------------------

    /// <summary>True when a creature in an area has been ticked off as dealt with.</summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <returns><c>true</c> when it is ticked.</returns>
    public bool IsCreatureCleared(string areaNodeId, string creatureNodeId)
    {
        lock (gate)
        {
            return areas.TryGetValue(areaNodeId ?? string.Empty, out AreaProgress found)
                && found.ClearedCreatureIds.Contains(creatureNodeId, StringComparer.Ordinal);
        }
    }

    /// <summary>Ticks a creature in an area off, or puts it back.</summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeId">The creature's node id.</param>
    public void ToggleCreatureCleared(string areaNodeId, string creatureNodeId)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId) || string.IsNullOrWhiteSpace(creatureNodeId))
        {
            return;
        }

        lock (gate)
        {
            // Either way the one-at-a-time counts go: ticked, every one is dealt with; put back,
            // none is.
            AreaProgress entry = AreaEntry(areaNodeId);
            entry.ClearedCreatureIds = entry.ClearedCreatureIds.Contains(creatureNodeId, StringComparer.Ordinal)
                ? entry.ClearedCreatureIds.Where(id => !string.Equals(id, creatureNodeId, StringComparison.Ordinal)).ToArray()
                : entry.ClearedCreatureIds.Append(creatureNodeId).ToArray();
            entry.CreatureCounts = Without(entry.CreatureCounts, creatureNodeId);

            Prune(areaNodeId, entry);
        }

        Notify();
    }

    /// <summary>
    /// Ticks every creature in an area off at once, or puts them all back — the "we cleared the
    /// room" button, and the way out of it.
    /// </summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeIds">Every creature the area's encounter lists.</param>
    /// <param name="cleared">True to tick them all, false to put them all back.</param>
    public void SetAreaCleared(string areaNodeId, IEnumerable<string> creatureNodeIds, bool cleared)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId))
        {
            return;
        }

        string[] ids = (creatureNodeIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        lock (gate)
        {
            // Unticking removes only what this area actually lists. A creature ticked here that the
            // encounter no longer names is left alone rather than quietly dropped: the prose may have
            // been re-scanned since, and losing the record of a fight that happened is worse than
            // carrying a tick nothing displays.
            AreaProgress entry = AreaEntry(areaNodeId);
            entry.ClearedCreatureIds = cleared
                ? entry.ClearedCreatureIds.Union(ids, StringComparer.Ordinal).ToArray()
                : entry.ClearedCreatureIds.Except(ids, StringComparer.Ordinal).ToArray();
            entry.CreatureCounts = entry.CreatureCounts
                .Where(count => !ids.Contains(count.CreatureId, StringComparer.Ordinal))
                .ToArray();

            Prune(areaNodeId, entry);
        }

        Notify();
    }

    /// <summary>
    /// How many of an area's creatures are ticked off. Counted against the ids the caller passes
    /// rather than against everything stored, so a tick left over from a creature the prose no longer
    /// names cannot make a room read as more cleared than it is.
    /// </summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeIds">Every creature the area's encounter lists.</param>
    /// <returns>How many of them are ticked.</returns>
    public int ClearedCount(string areaNodeId, IEnumerable<string> creatureNodeIds)
    {
        if (creatureNodeIds is null)
        {
            return 0;
        }

        lock (gate)
        {
            if (!areas.TryGetValue(areaNodeId ?? string.Empty, out AreaProgress found))
            {
                return 0;
            }

            var done = new HashSet<string>(found.ClearedCreatureIds, StringComparer.Ordinal);
            return creatureNodeIds.Distinct(StringComparer.Ordinal).Count(done.Contains);
        }
    }

    /// <summary>What the table recorded of one creature in one area.</summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <returns>The record; <see cref="CreatureRecord.None"/> when nothing is recorded.</returns>
    public CreatureRecord RecordOf(string areaNodeId, string creatureNodeId)
    {
        lock (gate)
        {
            if (!areas.TryGetValue(areaNodeId ?? string.Empty, out AreaProgress found))
            {
                return CreatureRecord.None;
            }

            CreatureCount count = CountOf(found, creatureNodeId);
            return new CreatureRecord(
                found.ClearedCreatureIds.Contains(creatureNodeId, StringComparer.Ordinal),
                count is null ? 0 : count.Dealt,
                count is null ? 0 : count.Fled);
        }
    }

    /// <summary>
    /// Where some of a creature have been dealt with, and how many in each area: what one somebody or
    /// one band listed in several rooms has lost, wherever it happened. A whole listing ticked off at
    /// once counts as one, which it is for one somebody; a band is always counted one at a time.
    /// </summary>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <returns>The areas with how many were dealt with in each, in no particular order.</returns>
    public IReadOnlyList<KeyValuePair<string, int>> DealtByArea(string creatureNodeId)
    {
        if (string.IsNullOrWhiteSpace(creatureNodeId))
        {
            return Array.Empty<KeyValuePair<string, int>>();
        }

        lock (gate)
        {
            var dealt = new List<KeyValuePair<string, int>>();
            foreach (AreaProgress entry in areas.Values)
            {
                int here = entry.ClearedCreatureIds.Contains(creatureNodeId, StringComparer.Ordinal)
                    ? 1
                    : CountOf(entry, creatureNodeId)?.Dealt ?? 0;
                if (here > 0)
                {
                    dealt.Add(new KeyValuePair<string, int>(entry.AreaNodeId, here));
                }
            }

            return dealt;
        }
    }

    /// <summary>
    /// Counts one creature in an area down or back up: dealt with, or fled. Neither count goes below
    /// nothing. Counting a listing that was ticked off whole does nothing: it is all dealt with
    /// already. To take one back from such a listing, use <see cref="TakeBackOne"/>.
    /// </summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <param name="dealt">How many more were dealt with; negative to take some back.</param>
    /// <param name="fled">How many more fled; negative to bring some back.</param>
    public void Count(string areaNodeId, string creatureNodeId, int dealt, int fled)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId) || string.IsNullOrWhiteSpace(creatureNodeId) || (dealt == 0 && fled == 0))
        {
            return;
        }

        lock (gate)
        {
            AreaProgress entry = AreaEntry(areaNodeId);
            if (!entry.ClearedCreatureIds.Contains(creatureNodeId, StringComparer.Ordinal))
            {
                CreatureCount count = CountOf(entry, creatureNodeId) ?? new CreatureCount { CreatureId = creatureNodeId };
                SetCount(entry, creatureNodeId, Math.Max(0, count.Dealt + dealt), Math.Max(0, count.Fled + fled));
            }

            Prune(areaNodeId, entry);
        }

        Notify();
    }

    /// <summary>
    /// Takes one creature dealt with in an area back: one fewer counted down, or, from a listing
    /// ticked off whole, every one of it but one.
    /// </summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <param name="listed">How many the area lists, for a listing ticked off whole.</param>
    public void TakeBackOne(string areaNodeId, string creatureNodeId, int listed)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId) || string.IsNullOrWhiteSpace(creatureNodeId))
        {
            return;
        }

        lock (gate)
        {
            AreaProgress entry = AreaEntry(areaNodeId);
            if (entry.ClearedCreatureIds.Contains(creatureNodeId, StringComparer.Ordinal))
            {
                entry.ClearedCreatureIds = entry.ClearedCreatureIds
                    .Where(id => !string.Equals(id, creatureNodeId, StringComparison.Ordinal))
                    .ToArray();
                SetCount(entry, creatureNodeId, Math.Max(0, listed - 1), 0);
            }
            else
            {
                CreatureCount count = CountOf(entry, creatureNodeId);
                if (count is not null)
                {
                    SetCount(entry, creatureNodeId, Math.Max(0, count.Dealt - 1), count.Fled);
                }
            }

            Prune(areaNodeId, entry);
        }

        Notify();
    }

    /// <summary>Puts a creature in an area back as the book lists it: nothing ticked, counted down or fled.</summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="creatureNodeId">The creature's node id.</param>
    public void PutBack(string areaNodeId, string creatureNodeId)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId) || string.IsNullOrWhiteSpace(creatureNodeId))
        {
            return;
        }

        bool changed = false;
        lock (gate)
        {
            if (areas.TryGetValue(areaNodeId, out AreaProgress entry))
            {
                changed = Forget(entry, creatureNodeId, keepFled: false);
                Prune(areaNodeId, entry);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// Brings one somebody or one band back on the board everywhere: what was dealt with of it in
    /// every area is forgotten. Who fled from where is kept, since that still happened.
    /// </summary>
    /// <param name="creatureNodeId">The creature's node id.</param>
    public void PutBackEverywhere(string creatureNodeId)
    {
        if (string.IsNullOrWhiteSpace(creatureNodeId))
        {
            return;
        }

        bool changed = false;
        lock (gate)
        {
            foreach (AreaProgress entry in areas.Values.ToArray())
            {
                changed |= Forget(entry, creatureNodeId, keepFled: true);
                Prune(entry.AreaNodeId, entry);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    // ---- Moves: creatures the DM put somewhere the book does not ---------------------------------

    /// <summary>Every move of one creature, oldest first.</summary>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <returns>Copies of its moves; none when it was never moved.</returns>
    public IReadOnlyList<CreatureMove> MovesOf(string creatureNodeId)
    {
        lock (gate)
        {
            return moves.Where(move => Is(move, creatureNodeId)).Select(Copy).ToArray();
        }
    }

    /// <summary>Every move into one area, of any creature, oldest first.</summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <returns>Copies of the moves into it; none when nothing was moved there.</returns>
    public IReadOnlyList<CreatureMove> MovesInto(string areaNodeId)
    {
        lock (gate)
        {
            return moves.Where(move => Into(move, areaNodeId)).Select(Copy).ToArray();
        }
    }

    /// <summary>
    /// Moves some of a creature from one area to another. Where the area they go to had its listing of
    /// that creature ticked off whole, the tick becomes a count of the ones that were there, so the
    /// newcomers are up and those already dealt with stay dealt with. One somebody moved into a room he
    /// fled from is back in it.
    /// </summary>
    /// <param name="move">What moved where; a move with a blank id, to the area it came from, or of none is ignored.</param>
    /// <param name="listedThere">How many of the creature the area they go to lists itself; zero when it lists none.</param>
    /// <param name="oneSomebody">True for one somebody, who is in one place, rather than a kind of creature or a band.</param>
    public void Move(CreatureMove move, int listedThere, bool oneSomebody)
    {
        if (!Usable(move))
        {
            return;
        }

        CreatureMove made = Copy(move);
        made.Count = oneSomebody ? 1 : Math.Min(made.Count, EncounterCount.MaxCount);
        if (made.ListedInAreaNodeId.Length == 0)
        {
            made.ListedInAreaNodeId = made.FromAreaNodeId;
        }

        lock (gate)
        {
            string id = made.CreatureId;
            AreaProgress there = AreaEntry(made.ToAreaNodeId);
            if (oneSomebody)
            {
                CreatureCount count = CountOf(there, id);
                if (count is not null)
                {
                    SetCount(there, id, count.Dealt, 0);
                }
            }
            else if (there.ClearedCreatureIds.Contains(id, StringComparer.Ordinal))
            {
                // Every one that was there is dealt with: as many as it lists, and what moved in and out.
                int wasThere = listedThere
                    + moves.Where(earlier => Is(earlier, id) && Into(earlier, made.ToAreaNodeId)).Sum(earlier => earlier.Count)
                    - moves.Where(earlier => Is(earlier, id) && OutOf(earlier, made.ToAreaNodeId)).Sum(earlier => earlier.Count);
                there.ClearedCreatureIds = there.ClearedCreatureIds
                    .Where(cleared => !string.Equals(cleared, id, StringComparison.Ordinal))
                    .ToArray();
                SetCount(there, id, Math.Max(0, wasThere), 0);
            }

            Prune(made.ToAreaNodeId, there);
            moves.Add(made);
        }

        Notify();
    }

    /// <summary>
    /// Takes back the last move of a creature into an area, or out of one: they are where they were
    /// before it.
    /// </summary>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <param name="areaNodeId">The area the move went into, or came out of.</param>
    /// <param name="into">True for the last move into the area, false for the last move out of it.</param>
    public void UndoMove(string creatureNodeId, string areaNodeId, bool into)
    {
        bool changed;
        lock (gate)
        {
            int last = moves.FindLastIndex(move =>
                Is(move, creatureNodeId) && (into ? Into(move, areaNodeId) : OutOf(move, areaNodeId)));
            changed = last >= 0;
            if (changed)
            {
                moves.RemoveAt(last);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// Forgets the moves of a creature: every one of them for one somebody, who is then where the book
    /// lists him, or, for a kind of creature or a band, the ones out of an area, which brings them back
    /// to it.
    /// </summary>
    /// <param name="creatureNodeId">The creature's node id.</param>
    /// <param name="areaNodeId">The area they are brought back to; ignored for one somebody.</param>
    /// <param name="oneSomebody">True for one somebody.</param>
    public void ForgetMoves(string creatureNodeId, string areaNodeId, bool oneSomebody)
    {
        int removed;
        lock (gate)
        {
            removed = moves.RemoveAll(move => Is(move, creatureNodeId) && (oneSomebody || OutOf(move, areaNodeId)));
        }

        if (removed > 0)
        {
            Notify();
        }
    }

    /// <summary>The DM's note for an area, or an empty string when none is written.</summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <returns>The note.</returns>
    public string NoteFor(string areaNodeId)
    {
        lock (gate)
        {
            return areas.TryGetValue(areaNodeId ?? string.Empty, out AreaProgress found)
                ? found.Note
                : string.Empty;
        }
    }

    /// <summary>
    /// Records the DM's note for an area. Blanking it is how a note is deleted, and an area left
    /// with neither a note nor a tick stops being stored at all.
    /// </summary>
    /// <param name="areaNodeId">The area's node id.</param>
    /// <param name="note">The note to record; null is read as blank.</param>
    public void SetNote(string areaNodeId, string note)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId))
        {
            return;
        }

        string text = (note ?? string.Empty).Trim();
        bool changed;
        lock (gate)
        {
            AreaProgress entry = AreaEntry(areaNodeId);
            changed = !string.Equals(entry.Note, text, StringComparison.Ordinal);
            entry.Note = text;
            Prune(areaNodeId, entry);
        }

        // Notes are saved on blur, so re-focusing and tabbing away without typing anything is the
        // common case. Notifying on that would wake both circuits and the persistence debounce for
        // no change at all.
        if (changed)
        {
            Notify();
        }
    }

    // ---- Pins -------------------------------------------------------------------------------

    /// <summary>
    /// What the DM pinned along the foot of the dock, oldest first: rules sections, a floor's facts, cards,
    /// any passage (see <see cref="PinKeys"/>). The table's, not one DM window's, so a second window and
    /// a restart show the same pins.
    /// </summary>
    /// <returns>The pins, as a copy.</returns>
    public IReadOnlyList<string> Pins()
    {
        lock (gate)
        {
            return pins.ToArray();
        }
    }

    /// <summary>True when something is pinned.</summary>
    /// <param name="pin">The pin (see <see cref="PinKeys"/>).</param>
    /// <returns><c>true</c> when it is among the pins.</returns>
    public bool IsPinned(string pin)
    {
        lock (gate)
        {
            return pins.Contains(pin ?? string.Empty, StringComparer.Ordinal);
        }
    }

    /// <summary>Pins something at the end of the pins, or takes its pin away when it is pinned already.</summary>
    /// <param name="pin">The pin (see <see cref="PinKeys"/>); a blank one is ignored.</param>
    public void TogglePin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return;
        }

        lock (gate)
        {
            if (!pins.Remove(pin))
            {
                pins.Add(pin);
            }
        }

        Notify();
    }

    // ---- Prerequisites ----------------------------------------------------------------------

    /// <summary>
    /// True when every one of a quest's prerequisites is met, so the log can show it as offerable.
    /// A quest with no prerequisites is always available.
    /// </summary>
    /// <param name="quest">The quest to test.</param>
    /// <returns><c>true</c> when the quest can be offered.</returns>
    public bool IsOfferable(Quest quest) =>
        quest is null || quest.Prerequisites.All(IsMet);

    /// <summary>
    /// True when one prerequisite is satisfied. A quest prerequisite is met when <em>any</em> of its
    /// alternatives is complete; a character-level prerequisite is met when the roster's highest
    /// recorded level reaches it — and, when the roster records no level at all, it is reported as
    /// unmet rather than guessed at, so the DM reads the requirement and decides.
    /// </summary>
    /// <param name="prerequisite">The prerequisite to test.</param>
    /// <returns><c>true</c> when it is satisfied.</returns>
    public bool IsMet(QuestPrerequisite prerequisite)
    {
        if (prerequisite is null)
        {
            return true;
        }

        return prerequisite.Kind switch
        {
            QuestPrerequisiteKind.QuestComplete =>
                prerequisite.AnyOfQuestIds.Any(id => Status(id) == QuestStatus.Complete),
            QuestPrerequisiteKind.CharacterLevel =>
                PartyLevel() >= prerequisite.CharacterLevel && prerequisite.CharacterLevel > 0,
            _ => true,
        };
    }

    /// <summary>
    /// The party's level as the roster records it — the highest of its members' levels, or 0 when
    /// none is recorded. Levels live on the players' character sheets, so this is a convenience for
    /// colouring a chip, never a gate.
    /// </summary>
    /// <returns>The highest recorded character level, or 0.</returns>
    public int PartyLevel()
    {
        Result<Party> saved = party.GetParty();
        if (!saved.IsValid)
        {
            return 0;
        }

        return saved.Value.Members.Count == 0 ? 0 : saved.Value.Members.Max(member => member.Level);
    }

    // ---- Writing ----------------------------------------------------------------------------

    /// <summary>
    /// Ticks a beat on or off. Ticking the last beat of a quest does <b>not</b> silently complete
    /// it: the DM says when a quest is done, because a quest's completion condition is often a
    /// hand-over back where it started that the beat chain deliberately ends on.
    /// </summary>
    /// <param name="questId">The quest's id.</param>
    /// <param name="beatId">The beat's id.</param>
    public void ToggleBeat(string questId, string beatId)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(beatId))
        {
            return;
        }

        lock (gate)
        {
            QuestProgress entry = Entry(questId);
            entry.CompletedBeatIds = entry.CompletedBeatIds.Contains(beatId, StringComparer.Ordinal)
                ? entry.CompletedBeatIds.Where(id => !string.Equals(id, beatId, StringComparison.Ordinal)).ToArray()
                : entry.CompletedBeatIds.Append(beatId).ToArray();

            // A first tick on an untouched quest means the party is on it. Any later status the DM
            // sets (Complete, Abandoned) is left alone.
            if (entry.Status is QuestStatus.None or QuestStatus.Available && entry.CompletedBeatIds.Count > 0)
            {
                entry.Status = QuestStatus.Active;
            }
        }

        Notify();
    }

    /// <summary>Sets a quest's status directly (the card's Active / Complete / Abandoned control).</summary>
    /// <param name="questId">The quest's id.</param>
    /// <param name="status">The status to record.</param>
    public void SetStatus(string questId, QuestStatus status)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            return;
        }

        lock (gate)
        {
            Entry(questId).Status = status;
        }

        Notify();
    }

    /// <summary>Clears a quest's status and every ticked beat, back to never-touched.</summary>
    /// <param name="questId">The quest's id.</param>
    public void ResetQuest(string questId)
    {
        bool changed;
        lock (gate)
        {
            changed = progress.Remove(questId ?? string.Empty);
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// Draws a card at random from what <see cref="Undrawn"/> leaves. A deck that keeps its drawn
    /// cards records this one, so it is not offered again until the deck is reset; a deck drawn fresh
    /// every time records nothing.
    /// </summary>
    /// <param name="deck">The deck to draw from.</param>
    /// <returns>The card drawn, or an empty card when the deck has none left.</returns>
    public DeckCard Draw(CardDeck deck) => Draw(deck, Random.Shared);

    /// <summary>Resets a deck: every card dealt from it goes back in.</summary>
    /// <param name="deckId">The deck's id.</param>
    public void ResetDeck(string deckId)
    {
        bool changed;
        lock (gate)
        {
            changed = drawnCards.Remove(deckId ?? string.Empty);
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// <see cref="Draw(CardDeck)"/> with the random source handed in, so a test can say which card
    /// the draw lands on.
    /// </summary>
    /// <param name="deck">The deck to draw from.</param>
    /// <param name="random">Picks the card; a card draw, not a security primitive.</param>
    /// <returns>The card drawn, or an empty card when the deck has none left.</returns>
    internal DeckCard Draw(CardDeck deck, Random random)
    {
        if (deck is null)
        {
            return new DeckCard();
        }

        DeckCard card;
        bool recorded = false;

        // One lock round the pick and the record, so two DM windows drawing at once cannot deal the
        // same kept card twice.
        lock (gate)
        {
            IReadOnlyList<DeckCard> pool = UndrawnLocked(deck);
            if (pool.Count == 0)
            {
                return new DeckCard();
            }

            card = pool[random.Next(pool.Count)];
            if (Keeps(deck) && !string.IsNullOrWhiteSpace(card.Id))
            {
                if (!drawnCards.TryGetValue(deck.Id, out List<string> ids))
                {
                    ids = new List<string>();
                    drawnCards[deck.Id] = ids;
                }

                ids.Add(card.Id);
                recorded = true;
            }
        }

        if (recorded)
        {
            Notify();
        }

        return card;
    }

    // ---- Persistence ------------------------------------------------------------------------

    /// <summary>Captures the campaign for storage.</summary>
    /// <returns>The campaign as it should be stored.</returns>
    public CampaignSnapshot Capture()
    {
        lock (gate)
        {
            return new CampaignSnapshot
            {
                Quests = progress.Values.Select(Copy).ToArray(),
                Decks = drawnCards
                    .Select(pair => new DeckProgress { DeckId = pair.Key, DrawnCardIds = pair.Value.ToArray() })
                    .ToArray(),
                Areas = areas.Values.Select(Copy).ToArray(),
                Pins = pins.ToArray(),
                Moves = moves.Select(Copy).ToArray(),
            };
        }
    }

    /// <summary>
    /// Puts a stored campaign back, replacing whatever is held. Called once at startup, before any
    /// circuit connects; an empty or absent snapshot simply leaves the campaign untouched-by-anyone.
    /// </summary>
    /// <param name="snapshot">The stored campaign; a null snapshot is ignored.</param>
    public void Restore(CampaignSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        lock (gate)
        {
            progress.Clear();
            drawnCards.Clear();
            areas.Clear();
            pins.Clear();
            moves.Clear();

            // Only moves that say what moved where, and of how many: a hand-edited document may hold
            // one that does not, and it would put creatures in no room at all.
            foreach (CreatureMove move in (snapshot.Moves ?? Array.Empty<CreatureMove>()).Where(Usable))
            {
                CreatureMove restored = Copy(move);
                restored.Count = Math.Min(restored.Count, EncounterCount.MaxCount);
                if (restored.ListedInAreaNodeId.Length == 0)
                {
                    restored.ListedInAreaNodeId = restored.FromAreaNodeId;
                }

                moves.Add(restored);
            }

            // Each pin once, in the order it was pinned, as a hand-edited document might repeat one.
            pins.AddRange((snapshot.Pins ?? Array.Empty<string>())
                .Where(pin => !string.IsNullOrWhiteSpace(pin))
                .Distinct(StringComparer.Ordinal));

            IEnumerable<AreaProgress> storedAreas = (snapshot.Areas ?? Array.Empty<AreaProgress>())
                .Where(entry => entry is not null && !string.IsNullOrEmpty(entry.AreaNodeId));

            foreach (AreaProgress entry in storedAreas)
            {
                AreaProgress restored = Copy(entry);

                // A repeated id would be counted twice and could make "3 of 2 dealt with" appear on
                // an area card, so de-duplicate on the way in as the quest beats do.
                restored.ClearedCreatureIds = restored.ClearedCreatureIds.Distinct(StringComparer.Ordinal).ToArray();

                // The same goes for the counts: one entry per creature (the first), none below
                // nothing, and none that records nothing.
                restored.CreatureCounts = restored.CreatureCounts
                    .Where(count => !string.IsNullOrWhiteSpace(count.CreatureId))
                    .GroupBy(count => count.CreatureId, StringComparer.Ordinal)
                    .Select(counts => new CreatureCount
                    {
                        CreatureId = counts.Key,
                        Dealt = Math.Max(0, counts.First().Dealt),
                        Fled = Math.Max(0, counts.First().Fled),
                    })
                    .Where(count => count.Dealt > 0 || count.Fled > 0)
                    .ToArray();
                areas[restored.AreaNodeId] = restored;
            }

            IEnumerable<QuestProgress> stored = (snapshot.Quests ?? Array.Empty<QuestProgress>())
                .Where(entry => entry is not null && !string.IsNullOrEmpty(entry.QuestId));

            foreach (QuestProgress entry in stored)
            {
                QuestProgress restored = Copy(entry);

                // A duplicated or repeated id would tick a beat twice over and inflate the card's
                // "3 of 4 done" line, so both are de-duplicated on the way in.
                restored.CompletedBeatIds = restored.CompletedBeatIds.Distinct(StringComparer.Ordinal).ToArray();
                progress[restored.QuestId] = restored;
            }

            IEnumerable<DeckProgress> storedDecks = (snapshot.Decks ?? Array.Empty<DeckProgress>())
                .Where(entry => entry is not null && !string.IsNullOrEmpty(entry.DeckId));

            foreach (DeckProgress entry in storedDecks)
            {
                // A deck listed twice, as a hand-edited document could, keeps what either entry drew,
                // once each, so "3 / 18 left" cannot count a card twice.
                IEnumerable<string> before = drawnCards.TryGetValue(entry.DeckId, out List<string> found)
                    ? found
                    : Enumerable.Empty<string>();
                List<string> ids = before
                    .Concat(entry.DrawnCardIds ?? Array.Empty<string>())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (ids.Count > 0)
                {
                    drawnCards[entry.DeckId] = ids;
                }
            }
        }

        Notify();
    }

    // ---- Internals --------------------------------------------------------------------------

    // Only a deck that says so remembers what it dealt, and it needs an id to remember it under. A
    // deck with no rule is drawn fresh, which can never withhold a card by mistake.
    private static bool Keeps(CardDeck deck) =>
        deck.DrawRule == DeckDrawRule.Kept && !string.IsNullOrWhiteSpace(deck.Id);

    // What a draw can turn up. Caller holds gate.
    private IReadOnlyList<DeckCard> UndrawnLocked(CardDeck deck)
    {
        if (!Keeps(deck) || !drawnCards.TryGetValue(deck.Id, out List<string> drawn))
        {
            return deck.Cards;
        }

        return deck.Cards.Where(card => !drawn.Contains(card.Id, StringComparer.Ordinal)).ToArray();
    }

    // The progress row for a quest, created on first write. Caller holds gate.
    private QuestProgress Entry(string questId)
    {
        if (!progress.TryGetValue(questId, out QuestProgress found))
        {
            found = new QuestProgress { QuestId = questId };
            progress[questId] = found;
        }

        return found;
    }

    private static QuestProgress Copy(QuestProgress entry) => new QuestProgress
    {
        QuestId = entry.QuestId,
        Status = entry.Status,
        CompletedBeatIds = (entry.CompletedBeatIds ?? Array.Empty<string>()).ToArray(),
    };

    private static AreaProgress Copy(AreaProgress entry) => new AreaProgress
    {
        AreaNodeId = entry.AreaNodeId,
        ClearedCreatureIds = (entry.ClearedCreatureIds ?? Array.Empty<string>()).ToArray(),
        CreatureCounts = (entry.CreatureCounts ?? Array.Empty<CreatureCount>())
            .Where(count => count is not null)
            .Select(count => new CreatureCount { CreatureId = count.CreatureId ?? string.Empty, Dealt = count.Dealt, Fled = count.Fled })
            .ToArray(),
        Note = entry.Note ?? string.Empty,
    };

    private static CreatureMove Copy(CreatureMove move) => new CreatureMove
    {
        CreatureId = move.CreatureId ?? string.Empty,
        FromAreaNodeId = move.FromAreaNodeId ?? string.Empty,
        ToAreaNodeId = move.ToAreaNodeId ?? string.Empty,
        Count = move.Count,
        ListedInAreaNodeId = move.ListedInAreaNodeId ?? string.Empty,
    };

    // A move that says what moved, from where to somewhere else, and of how many.
    private static bool Usable(CreatureMove move) =>
        move is not null
        && !string.IsNullOrWhiteSpace(move.CreatureId)
        && !string.IsNullOrWhiteSpace(move.FromAreaNodeId)
        && !string.IsNullOrWhiteSpace(move.ToAreaNodeId)
        && !string.Equals(move.FromAreaNodeId, move.ToAreaNodeId, StringComparison.Ordinal)
        && move.Count > 0;

    private static bool Is(CreatureMove move, string creatureNodeId) =>
        string.Equals(move.CreatureId, creatureNodeId, StringComparison.Ordinal);

    private static bool Into(CreatureMove move, string areaNodeId) =>
        string.Equals(move.ToAreaNodeId, areaNodeId, StringComparison.Ordinal);

    private static bool OutOf(CreatureMove move, string areaNodeId) =>
        string.Equals(move.FromAreaNodeId, areaNodeId, StringComparison.Ordinal);

    // One creature's counts in an area, or null when it has none. Caller holds gate.
    private static CreatureCount CountOf(AreaProgress entry, string creatureNodeId) =>
        entry.CreatureCounts.FirstOrDefault(count => string.Equals(count.CreatureId, creatureNodeId, StringComparison.Ordinal));

    // Sets one creature's counts in an area, dropping its entry when both are nothing. Caller holds gate.
    private static void SetCount(AreaProgress entry, string creatureNodeId, int dealt, int fled)
    {
        CreatureCount[] others = Without(entry.CreatureCounts, creatureNodeId);
        entry.CreatureCounts = dealt > 0 || fled > 0
            ? others.Append(new CreatureCount { CreatureId = creatureNodeId, Dealt = dealt, Fled = fled }).ToArray()
            : others;
    }

    private static CreatureCount[] Without(IEnumerable<CreatureCount> counts, string creatureNodeId) =>
        counts.Where(count => !string.Equals(count.CreatureId, creatureNodeId, StringComparison.Ordinal)).ToArray();

    // Forgets what an area recorded of one creature: its tick and its counts, or only what was dealt
    // with when keepFled is set. True when anything was forgotten. Caller holds gate.
    private static bool Forget(AreaProgress entry, string creatureNodeId, bool keepFled)
    {
        bool ticked = entry.ClearedCreatureIds.Contains(creatureNodeId, StringComparer.Ordinal);
        CreatureCount count = CountOf(entry, creatureNodeId);
        int fled = keepFled && count is not null ? count.Fled : 0;
        bool changed = ticked || (count is not null && (count.Dealt > 0 || count.Fled != fled));

        entry.ClearedCreatureIds = entry.ClearedCreatureIds
            .Where(id => !string.Equals(id, creatureNodeId, StringComparison.Ordinal))
            .ToArray();
        SetCount(entry, creatureNodeId, 0, fled);
        return changed;
    }

    // The progress row for an area, created on first write. Caller holds gate.
    private AreaProgress AreaEntry(string areaNodeId)
    {
        if (!areas.TryGetValue(areaNodeId, out AreaProgress found))
        {
            found = new AreaProgress { AreaNodeId = areaNodeId };
            areas[areaNodeId] = found;
        }

        return found;
    }

    // Drops an area row that records nothing, so untick-everything-and-blank-the-note leaves the
    // area genuinely untouched rather than a permanent empty row in every future snapshot. Caller
    // holds gate.
    private void Prune(string areaNodeId, AreaProgress entry)
    {
        if (entry.ClearedCreatureIds.Count == 0 && entry.CreatureCounts.Count == 0 && entry.Note.Length == 0)
        {
            areas.Remove(areaNodeId);
        }
    }

    private void Notify() => Changed?.Invoke();
}
