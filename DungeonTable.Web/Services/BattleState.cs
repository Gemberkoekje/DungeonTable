using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Session;
using DungeonTable.Core.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// The one live fight: its initiative order, every combatant's hit points and conditions, and whose
/// turn it is. Mutated only from the DM circuit and never sent to the player projector — the
/// players see miniatures and dice on the table, not this.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a <b>singleton</b>, deliberately: a browser refresh mid-fight must not lose the
/// fight. It survives an app restart too — <see cref="Capture"/> and <see cref="Restore"/>
/// are driven by the persistence coordinator, alongside <see cref="SessionState"/>.
/// </para>
/// <para>
/// Copies <see cref="SessionState"/>'s concurrency discipline exactly: one private lock, accessors
/// that hand out immutable snapshots rather than live objects, and <see cref="Changed"/> always
/// raised <em>outside</em> the lock (raising it inside would re-enter through the DM circuit's
/// re-render and deadlock).
/// </para>
/// </remarks>
public sealed class BattleState
{
    // The largest die roll the in-app initiative convenience makes; always overwritable by the DM.
    private const int DieFaces = 20;

    private readonly object gate = new object();
    private readonly List<InitiativeEntry> entries = new List<InitiativeEntry>();
    private readonly Dictionary<string, Combatant> combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal);

    // Per-type running count behind "Bugbear 1", "Bugbear 2", ... It only ever increases within a
    // battle, so removing Bugbear 2 leaves 1, 3 and 4 alone instead of silently renaming them
    // underneath the DM's notes.
    private readonly Dictionary<string, int> issuedOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);

    private readonly IPartyRoster party;
    private readonly IStatLibrary stats;

    private string battleId = string.Empty;
    private string areaNodeId = string.Empty;
    private string areaTitle = string.Empty;
    private int round = 1;
    private int turnIndex;
    private bool started;
    private int nextSequence;
    private int nextId;

    /// <summary>Creates the battle state over the rosters and stat blocks it seeds fights from.</summary>
    /// <param name="party">The saved party roster, seeded into every new battle.</param>
    /// <param name="stats">The stat library that supplies monster HP, AC and initiative modifiers.</param>
    public BattleState(IPartyRoster party, IStatLibrary stats)
    {
        this.party = party;
        this.stats = stats;
    }

    /// <summary>Raised whenever the fight changes, so the Battle tab re-renders.</summary>
    public event Action Changed;

    /// <summary>True while a fight is set up or running.</summary>
    public bool IsActive
    {
        get { lock (gate) { return battleId.Length > 0; } }
    }

    /// <summary>The node id of the area the running fight started in, or an empty string.</summary>
    public string AreaNodeId
    {
        get { lock (gate) { return areaNodeId; } }
    }

    /// <summary>
    /// An immutable snapshot of the whole fight, safe to render from while it carries on changing.
    /// An inactive battle yields an empty snapshot rather than null.
    /// </summary>
    /// <returns>The current battle.</returns>
    public Battle Snapshot()
    {
        lock (gate)
        {
            return new Battle
            {
                Id = battleId,
                AreaNodeId = areaNodeId,
                AreaTitle = areaTitle,
                Round = round,
                TurnIndex = turnIndex,
                Started = started,
                Entries = entries.Select(entry => entry.Copy()).ToArray(),
                Combatants = combatants.Values.Select(combatant => combatant.Copy()).ToArray(),
            };
        }
    }

    // ---- Persistence ------------------------------------------------------------------------

    /// <summary>
    /// Captures the whole fight for storage, including the counters the render snapshot has no reason
    /// to carry: the last issued id number, the next insertion sequence, and how many of each creature
    /// type have been numbered.
    /// </summary>
    /// <remarks>
    /// This is <em>not</em> <see cref="Snapshot"/>. That one is what the Battle tab renders; this one
    /// is what a restart has to be rebuilt from, which is a strictly larger thing.
    /// </remarks>
    /// <returns>The fight as it should be stored; an empty snapshot when none is running.</returns>
    public BattleSnapshot Capture()
    {
        lock (gate)
        {
            return new BattleSnapshot
            {
                Id = battleId,
                AreaNodeId = areaNodeId,
                AreaTitle = areaTitle,
                Round = round,
                TurnIndex = turnIndex,
                Started = started,
                LastIdNumber = nextId,
                NextSequence = nextSequence,
                IssuedOrdinals = issuedOrdinals
                    .Select(pair => new IssuedOrdinal { TypeKey = pair.Key, Issued = pair.Value })
                    .ToArray(),
                Entries = entries.Select(entry => entry.Copy()).ToArray(),
                Combatants = combatants.Values.Select(combatant => combatant.Copy()).ToArray(),
            };
        }
    }

    /// <summary>
    /// Puts a stored fight back, replacing whatever is held. Called once at startup, before any
    /// circuit connects; an empty or absent snapshot simply leaves no fight running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The restore is defensive about the document, because a fight is the one thing here that cannot
    /// be re-derived from committed data: rows referring to combatants that are not in the document are
    /// dropped (and a row left with no members goes with them), the turn marker is clamped back into
    /// the order, and the round never restores below 1.
    /// </para>
    /// <para>
    /// Both counters are raised to at least what the restored ids and sequences already use, so even a
    /// document written by a different build — or edited by hand — cannot make the next creature added
    /// collide with one already in the fight.
    /// </para>
    /// </remarks>
    /// <param name="snapshot">The stored fight; a null snapshot is ignored.</param>
    public void Restore(BattleSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        lock (gate)
        {
            // Teardown first, then rebuild: restoring an empty snapshot is a legitimate "no fight was
            // running", and it must still clear whatever this process had. The notification below fires
            // either way, so a view bound to an emptied battle re-renders.
            Teardown();
            if (snapshot.HasBattle())
            {
                RestoreBattle(snapshot);
            }
        }

        Notify();
    }

    // Rebuilds the fight from a stored snapshot. Caller holds gate and has just torn down.
    private void RestoreBattle(BattleSnapshot snapshot)
    {
        battleId = snapshot.Id;
        areaNodeId = snapshot.AreaNodeId ?? string.Empty;
        areaTitle = snapshot.AreaTitle ?? string.Empty;
        round = Math.Max(1, snapshot.Round);
        started = snapshot.Started;

        IEnumerable<Combatant> storedCombatants = (snapshot.Combatants ?? Array.Empty<Combatant>())
            .Where(combatant => combatant is not null && !string.IsNullOrEmpty(combatant.Id));

        foreach (Combatant combatant in storedCombatants)
        {
            combatants[combatant.Id] = combatant.Copy();
        }

        // A row whose members are all missing from the document is dropped rather than restored as an
        // empty row: an empty row would take a turn in the order with nobody to act on it.
        IEnumerable<InitiativeEntry> storedEntries = (snapshot.Entries ?? Array.Empty<InitiativeEntry>())
            .Where(entry => entry is not null && !string.IsNullOrEmpty(entry.Id))
            .Select(entry =>
            {
                InitiativeEntry restored = entry.Copy();
                restored.MemberIds = restored.MemberIds.Where(combatants.ContainsKey).ToArray();
                return restored;
            })
            .Where(entry => entry.MemberIds.Count > 0);

        entries.AddRange(storedEntries);

        IEnumerable<IssuedOrdinal> storedOrdinals = (snapshot.IssuedOrdinals ?? Array.Empty<IssuedOrdinal>())
            .Where(issued => issued is not null && !string.IsNullOrEmpty(issued.TypeKey));

        foreach (IssuedOrdinal issued in storedOrdinals)
        {
            issuedOrdinals[issued.TypeKey] = issued.Issued;
        }

        nextId = Math.Max(snapshot.LastIdNumber, HighestIdNumber());
        nextSequence = Math.Max(
            snapshot.NextSequence,
            entries.Count == 0 ? 0 : entries.Max(entry => entry.Sequence) + 1);

        turnIndex = snapshot.TurnIndex;
        ClampTurn();
    }

    // The largest number any restored id ends in, so NextId can never re-issue one. Ids are minted as
    // a one-letter prefix plus a running number ("c12"), and every id in the fight came from that. A
    // suffix that does not parse is ignored rather than trusted. Caller holds gate.
    private int HighestIdNumber()
    {
        int highest = 0;
        foreach (string id in combatants.Keys.Concat(entries.Select(entry => entry.Id)))
        {
            if (id.Length > 1
                && int.TryParse(
                    id.AsSpan(1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int number))
            {
                highest = Math.Max(highest, number);
            }
        }

        return highest;
    }

    // ---- Lifecycle --------------------------------------------------------------------------

    /// <summary>
    /// Starts a fresh fight in an area, seeding one player row per saved party member with their AC,
    /// maximum HP and passive Perception filled in and initiative left blank — the DM types what the
    /// players call out.
    /// </summary>
    /// <remarks>
    /// This <b>replaces</b> any running fight without asking. Confirming that with the DM is the
    /// caller's job: check <see cref="IsActive"/> and prompt first. Keeping
    /// the prompt in the UI rather than here means the service has one unambiguous behaviour and the
    /// confirm can be worded for wherever it was triggered from.
    /// </remarks>
    /// <param name="area">The node id of the area the fight happens in.</param>
    /// <param name="title">The area's display title, for the Battle tab header.</param>
    public void StartBattle(string area, string title)
    {
        lock (gate)
        {
            Teardown();
            battleId = NextId("b");
            areaNodeId = area ?? string.Empty;
            areaTitle = title ?? string.Empty;
            SeedParty();
        }

        Notify();
    }

    /// <summary>
    /// Tears the fight down completely: the initiative order, every combatant, the round and turn
    /// counters, and all per-combatant transient state (conditions, concentration, spent slots). The
    /// next <see cref="StartBattle"/> begins from a clean sheet. There is no undo, so the caller
    /// confirms first.
    /// </summary>
    public void EndBattle()
    {
        bool changed;
        lock (gate)
        {
            changed = battleId.Length > 0;
            Teardown();
        }

        if (changed)
        {
            Notify();
        }
    }

    // ---- Building the fight -----------------------------------------------------------------

    /// <summary>
    /// Adds a group of identical monsters as one initiative row with individually numbered members
    /// ("Bugbear 1".."Bugbear 4"), pre-filled from the stat library when a block is extracted for
    /// them. Adding more of a type that is already in the fight extends the existing row and
    /// continues its numbering; a type whose row has been split gets a new row instead.
    /// </summary>
    /// <param name="statBlockNodeId">The stat-library node id, or an empty string when it has none.</param>
    /// <param name="label">The group's name ("Bugbear"); members are numbered from it.</param>
    /// <param name="count">How many to add; values below 1 add nothing.</param>
    public void AddGroup(string statBlockNodeId, string label, int count)
    {
        if (count < 1)
        {
            return;
        }

        CombatantSeed seed = SeedFor(statBlockNodeId, label);
        lock (gate)
        {
            EnsureBattle();

            string key = TypeKey(seed);
            InitiativeEntry row = SoleEntryFor(key);
            var members = new List<string>(row is null ? Array.Empty<string>() : row.MemberIds);

            for (int i = 0; i < count; i++)
            {
                int ordinal = TakeOrdinal(key);
                Combatant member = NewCombatant(seed, ordinal, $"{seed.Name} {ordinal}");
                combatants[member.Id] = member;
                members.Add(member.Id);
            }

            if (row is null)
            {
                entries.Add(NewEntry(seed, PluralLabel(seed.Name), members));
            }
            else
            {
                row.MemberIds = members.ToArray();
            }
        }

        Notify();
    }

    /// <summary>
    /// Adds a single combatant on its own initiative row — a player, an ally from the roster, or a
    /// one-off guest. Unlike <see cref="AddGroup"/> the name is used as given, with no numbering.
    /// </summary>
    /// <param name="seed">The combatant's starting numbers.</param>
    /// <returns>The new combatant's id, or an empty string when the seed had no name.</returns>
    public string AddCombatant(CombatantSeed seed)
    {
        if (seed is null || string.IsNullOrWhiteSpace(seed.Name))
        {
            return string.Empty;
        }

        string id;
        lock (gate)
        {
            EnsureBattle();
            Combatant member = NewCombatant(seed, 0, seed.Name.Trim());
            combatants[member.Id] = member;
            entries.Add(NewEntry(seed, member.Name, new[] { member.Id }));
            id = member.Id;
        }

        Notify();
        return id;
    }

    /// <summary>
    /// Adds one named individual on its own row: an authored creature or person that uses a stat
    /// block without being it, such as Grask, who fights as a bugbear. The numbers
    /// come from the block, as <see cref="AddGroup"/> would fill them; the name stays the
    /// individual's, where a group takes the block's.
    /// </summary>
    /// <param name="statBlockNodeId">The stat-library node id of the block it uses, or an empty string.</param>
    /// <param name="name">The individual's name ("Grask").</param>
    /// <returns>The new combatant's id, or an empty string when no name was given.</returns>
    public string AddIndividual(string statBlockNodeId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        CombatantSeed block = SeedFor(statBlockNodeId, name);
        return AddCombatant(new CombatantSeed
        {
            Kind = block.Kind,
            Name = name.Trim(),
            StatBlockNodeId = block.StatBlockNodeId,
            MaxHp = block.MaxHp,
            ArmourClass = block.ArmourClass,
            PassivePerception = block.PassivePerception,
            InitiativeModifier = block.InitiativeModifier,
        });
    }

    /// <summary>
    /// Adds what one row of the encounter strip stands for: a kind of creature as a numbered group
    /// named after its stat block, or a named individual (Grask, the Bell-Warden) under its
    /// own name, once per <paramref name="count"/>.
    /// </summary>
    /// <param name="monster">The encounter row.</param>
    /// <param name="count">How many to add; values below 1 add nothing.</param>
    public void AddEncounter(EncounterMonster monster, int count)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if (!monster.Individual)
        {
            AddGroup(monster.StatBlockNodeId, monster.Name, count);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            AddIndividual(monster.StatBlockNodeId, monster.Name);
        }
    }

    /// <summary>
    /// Adds one monster from the stat library as its own row (the hover "+" beside a monster link in
    /// the prose), pre-filled exactly as <see cref="AddGroup"/> would fill it.
    /// </summary>
    /// <param name="statBlockNodeId">The stat-library node id, or an empty string.</param>
    /// <param name="label">The creature's name.</param>
    public void AddMonster(string statBlockNodeId, string label) => AddGroup(statBlockNodeId, label, 1);

    /// <summary>Removes one combatant, dropping its initiative row when that empties it.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    public void RemoveCombatant(string combatantId)
    {
        bool changed;
        lock (gate)
        {
            changed = combatants.Remove(combatantId);
            if (changed)
            {
                string current = CurrentEntryId();
                foreach (InitiativeEntry entry in entries)
                {
                    entry.MemberIds = entry.MemberIds
                        .Where(id => !string.Equals(id, combatantId, StringComparison.Ordinal))
                        .ToArray();
                }

                entries.RemoveAll(entry => entry.MemberIds.Count == 0);
                KeepTurnAfterRemoval(current);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>Removes a whole initiative row and every combatant on it.</summary>
    /// <param name="entryId">The row's id.</param>
    public void RemoveEntry(string entryId)
    {
        bool changed;
        lock (gate)
        {
            InitiativeEntry entry = Entry(entryId);
            changed = entry is not null;
            if (changed)
            {
                string current = CurrentEntryId();
                foreach (string id in entry.MemberIds)
                {
                    combatants.Remove(id);
                }

                entries.Remove(entry);
                KeepTurnAfterRemoval(current);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    // ---- Initiative -------------------------------------------------------------------------

    /// <summary>Sets a row's initiative to what the DM typed. The list does not re-sort until asked.</summary>
    /// <param name="entryId">The row's id.</param>
    /// <param name="value">The initiative rolled.</param>
    public void SetInitiative(string entryId, int value) => MutateEntry(entryId, entry =>
    {
        entry.Initiative = value;
        entry.HasInitiative = true;
    });

    /// <summary>Blanks a row's initiative again (not the same as rolling a 0 — a blank row sorts last).</summary>
    /// <param name="entryId">The row's id.</param>
    public void ClearInitiative(string entryId) => MutateEntry(entryId, entry =>
    {
        entry.Initiative = 0;
        entry.HasInitiative = false;
    });

    /// <summary>Rolls d20 + the row's initiative modifier. A convenience only — always overwritable.</summary>
    /// <param name="entryId">The row's id.</param>
    public void RollInitiative(string entryId) => MutateEntry(entryId, Roll);

    /// <summary>Rolls initiative for every monster row at once, leaving players and allies alone.</summary>
    public void RollAllMonsterInitiative()
    {
        bool changed;
        lock (gate)
        {
            List<InitiativeEntry> monsters = entries.Where(entry => entry.Kind == CombatantKind.Monster).ToList();
            changed = monsters.Count > 0;
            monsters.ForEach(Roll);
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// Sorts the initiative order: highest first, blanks last, ties broken by initiative modifier
    /// and then by the order rows were added. Runs only when asked (a button, or committing a typed
    /// value) — never per keystroke, which would yank rows out from under the DM mid-entry. Whoever's
    /// turn it is keeps it.
    /// </summary>
    public void Sort()
    {
        lock (gate)
        {
            string current = CurrentEntryId();
            List<InitiativeEntry> sorted = entries
                .OrderByDescending(entry => entry.HasInitiative)
                .ThenByDescending(entry => entry.Initiative)
                .ThenByDescending(entry => entry.InitiativeModifier)
                .ThenBy(entry => entry.Sequence)
                .ToList();

            entries.Clear();
            entries.AddRange(sorted);
            RestoreTurn(current);
        }

        Notify();
    }

    /// <summary>
    /// Moves one row to another position in the order, leaving its initiative alone. This is how a
    /// tie gets settled the way the table actually played it: two rows on 12 sort next to each other,
    /// and the DM drags one above the other rather than inventing a tie-break number. Whoever's turn
    /// it is keeps it. A subsequent <see cref="Sort"/> will of course undo the arrangement — sorting
    /// is the DM's own explicit action, so that is the expected trade.
    /// </summary>
    /// <param name="entryId">The row to move.</param>
    /// <param name="toIndex">
    /// Where to put it, counted in the list <em>after</em> the row has been lifted out — so dragging
    /// a row onto the index below it lands it below that row, which is what a drag looks like.
    /// Values outside the list are clamped.
    /// </param>
    public void MoveEntry(string entryId, int toIndex)
    {
        bool changed;
        lock (gate)
        {
            int from = entries.FindIndex(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
            int to = entries.Count == 0 ? 0 : Math.Clamp(toIndex, 0, entries.Count - 1);
            changed = from >= 0 && from != to;
            if (changed)
            {
                string current = CurrentEntryId();
                InitiativeEntry moved = entries[from];
                entries.RemoveAt(from);
                entries.Insert(to, moved);
                RestoreTurn(current);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// Promotes each member of a group to its own initiative row, carrying the group's initiative
    /// across so nothing has to be re-entered, after which they roll and sort independently.
    /// </summary>
    /// <param name="entryId">The group row's id.</param>
    public void SplitGroup(string entryId)
    {
        bool changed;
        lock (gate)
        {
            InitiativeEntry group = Entry(entryId);
            changed = group is not null && group.MemberIds.Count > 1;
            if (changed)
            {
                string current = CurrentEntryId();
                int at = entries.IndexOf(group);
                entries.RemoveAt(at);
                entries.InsertRange(at, group.MemberIds.Select(id => new InitiativeEntry
                {
                    Id = NextId("e"),
                    Sequence = nextSequence++,
                    Initiative = group.Initiative,
                    HasInitiative = group.HasInitiative,
                    InitiativeModifier = group.InitiativeModifier,
                    Label = Named(id),
                    MemberIds = new[] { id },
                    Kind = group.Kind,
                    StatBlockNodeId = group.StatBlockNodeId,
                }));

                // The split row itself is gone, so a turn that was on it lands on the first of its
                // members rather than drifting to whoever happened to shuffle into that index.
                RestoreTurn(string.Equals(current, entryId, StringComparison.Ordinal) ? entries[at].Id : current);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// Merges every row sharing this one's stat block back into a single group row, undoing a split.
    /// The surviving row keeps the earliest-added row's initiative. Does nothing for a row with no
    /// stat block, where there is no reliable notion of "the same creature", and never takes in a
    /// named individual or an ally who happens to use the same block.
    /// </summary>
    /// <param name="entryId">Any row of the group to merge.</param>
    public void MergeGroup(string entryId)
    {
        bool changed;
        lock (gate)
        {
            List<InitiativeEntry> siblings = SplitRowsOf(Entry(entryId));

            changed = siblings.Count > 1;
            if (changed)
            {
                InitiativeEntry keep = siblings[0];
                keep.MemberIds = siblings.SelectMany(entry => entry.MemberIds).ToArray();
                keep.Label = PluralLabel(BaseName(keep.MemberIds));
                entries.RemoveAll(entry => siblings.Contains(entry) && entry != keep);
                ClampTurn();
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    /// <summary>
    /// Indicates whether <see cref="MergeGroup"/> would do anything for this row: whether it is one
    /// of several rows a group was split into. The Battle tab offers Merge only then.
    /// </summary>
    /// <param name="entryId">The row's id.</param>
    /// <returns><c>true</c> when the row has split siblings to merge with.</returns>
    public bool CanMerge(string entryId)
    {
        lock (gate)
        {
            return SplitRowsOf(Entry(entryId)).Count > 1;
        }
    }

    // ---- Turn order -------------------------------------------------------------------------

    /// <summary>
    /// Moves to the next turn, wrapping to the top of the order and bumping the round when it does.
    /// The first call on a fresh fight starts the tracking on the top row rather than advancing past
    /// it. Rows whose combatants are all down are skipped — they stay in the list to be healed or
    /// removed, they just do not get turns.
    /// </summary>
    public void NextTurn() => Step(1);

    /// <summary>Steps back a turn, unwinding the round counter when it wraps past the top.</summary>
    public void PreviousTurn() => Step(-1);

    // ---- Combatant state --------------------------------------------------------------------

    /// <summary>Applies damage, clamped so a combatant never drops below 0 hit points.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="amount">Damage taken; values below 1 are ignored.</param>
    public void ApplyDamage(string combatantId, int amount)
    {
        if (amount > 0)
        {
            MutateCombatant(combatantId, combatant => combatant.CurrentHp = ClampHp(combatant, combatant.CurrentHp - amount));
        }
    }

    /// <summary>Heals, clamped at the combatant's maximum hit points.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="amount">Hit points restored; values below 1 are ignored.</param>
    public void Heal(string combatantId, int amount)
    {
        if (amount > 0)
        {
            MutateCombatant(combatantId, combatant => combatant.CurrentHp = ClampHp(combatant, combatant.CurrentHp + amount));
        }
    }

    /// <summary>Sets current hit points directly, clamped to the legal range.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="value">The new current hit points.</param>
    public void SetHp(string combatantId, int value) =>
        MutateCombatant(combatantId, combatant => combatant.CurrentHp = ClampHp(combatant, value));

    /// <summary>
    /// Sets maximum hit points (a DM correction, or filling in a creature that had no stat block),
    /// pulling current hit points down with it when they would otherwise exceed the new maximum.
    /// </summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="value">The new maximum; negatives are treated as 0 ("untracked").</param>
    public void SetMaxHp(string combatantId, int value) => MutateCombatant(combatantId, combatant =>
    {
        combatant.MaxHp = Math.Max(0, value);
        combatant.CurrentHp = ClampHp(combatant, combatant.CurrentHp);
    });

    /// <summary>Sets armour class.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="value">The new armour class; negatives are treated as 0 ("unknown").</param>
    public void SetArmourClass(string combatantId, int value) =>
        MutateCombatant(combatantId, combatant => combatant.ArmourClass = Math.Max(0, value));

    /// <summary>Renames a combatant ("Bugbear 2 (captain)"), leaving its numbering untouched.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="name">The new display name; a blank name is ignored.</param>
    public void SetName(string combatantId, string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            MutateCombatant(combatantId, combatant => combatant.Name = name.Trim());
        }
    }

    /// <summary>Sets the row's free-text DM note.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="note">The note; may be blank to clear it.</param>
    public void SetNotes(string combatantId, string note) =>
        MutateCombatant(combatantId, combatant => combatant.Notes = note ?? string.Empty);

    /// <summary>Turns one condition on or off.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="condition">The condition to toggle.</param>
    public void ToggleCondition(string combatantId, ConditionKind condition)
    {
        if (condition == ConditionKind.None)
        {
            return;
        }

        MutateCombatant(combatantId, combatant => combatant.Conditions = combatant.Conditions.Contains(condition)
            ? combatant.Conditions.Where(kind => kind != condition).ToArray()
            : combatant.Conditions.Append(condition).ToArray());
    }

    /// <summary>Marks a combatant as concentrating (or not) and records what on.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="concentrating">True while concentration holds.</param>
    /// <param name="note">What it is concentrating on; ignored when <paramref name="concentrating"/> is false.</param>
    public void SetConcentration(string combatantId, bool concentrating, string note) =>
        MutateCombatant(combatantId, combatant =>
        {
            combatant.Concentrating = concentrating;
            combatant.ConcentrationNote = concentrating ? note ?? string.Empty : string.Empty;
        });

    /// <summary>Records one spell slot of a level as spent.</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="level">The spell level (1-9).</param>
    public void SpendSlot(string combatantId, int level) => AdjustSlot(combatantId, level, 1);

    /// <summary>Gives a spent spell slot back (a mis-click, or a short rest).</summary>
    /// <param name="combatantId">The combatant's id.</param>
    /// <param name="level">The spell level (1-9).</param>
    public void RestoreSlot(string combatantId, int level) => AdjustSlot(combatantId, level, -1);

    // ---- Internals --------------------------------------------------------------------------

    // Wipes every trace of the current fight. Caller holds gate.
    private void Teardown()
    {
        entries.Clear();
        combatants.Clear();
        issuedOrdinals.Clear();
        battleId = string.Empty;
        areaNodeId = string.Empty;
        areaTitle = string.Empty;
        round = 1;
        turnIndex = 0;
        started = false;
    }

    // Adding a combatant outside a started battle implicitly opens one, so "add these two bugbears"
    // from the Info tab never fails with "no battle". Caller holds gate.
    private void EnsureBattle()
    {
        if (battleId.Length == 0)
        {
            battleId = NextId("b");
        }
    }

    // One player row per saved party member. A roster that cannot be read seeds nothing rather than
    // failing the whole start - the DM can still add rows by hand. Caller holds gate.
    private void SeedParty()
    {
        Result<Party> saved = party.GetParty();
        if (!saved.IsValid)
        {
            return;
        }

        foreach (PartyMember member in saved.Value.Members.Where(member => !string.IsNullOrWhiteSpace(member.Name)))
        {
            var seed = new CombatantSeed
            {
                Kind = CombatantKind.Player,
                Name = member.Name.Trim(),
                MaxHp = member.MaxHp,
                ArmourClass = member.ArmourClass,
                PassivePerception = member.PassivePerception,
                InitiativeModifier = member.InitiativeModifier,
            };

            Combatant combatant = NewCombatant(seed, 0, seed.Name);
            combatants[combatant.Id] = combatant;
            entries.Add(NewEntry(seed, combatant.Name, new[] { combatant.Id }));
        }
    }

    // Fill a monster's numbers in from its stat block where one is extracted; fall back to a bare
    // name so a missing extraction still adds a usable row.
    private CombatantSeed SeedFor(string statBlockNodeId, string label)
    {
        Result<StatBlock> found = string.IsNullOrWhiteSpace(statBlockNodeId)
            ? Result.WithMessages<StatBlock>(ValidationMessage.Error("No stat block id.", nameof(statBlockNodeId)))
            : stats.GetMonster(statBlockNodeId);

        if (!found.IsValid)
        {
            return new CombatantSeed
            {
                Kind = CombatantKind.Monster,
                Name = string.IsNullOrWhiteSpace(label) ? statBlockNodeId : label.Trim(),
                StatBlockNodeId = statBlockNodeId ?? string.Empty,
            };
        }

        StatBlock block = found.Value;
        return new CombatantSeed
        {
            Kind = CombatantKind.Monster,
            Name = block.Name.Length > 0 ? block.Name : label,
            StatBlockNodeId = block.NodeId,
            MaxHp = block.AverageHitPoints,
            ArmourClass = block.ArmourClass,
            PassivePerception = block.PassivePerception,
            InitiativeModifier = StatBlockMath.AbilityModifier(block.Abilities.Dex),
        };
    }

    private Combatant NewCombatant(CombatantSeed seed, int ordinal, string name) => new Combatant
    {
        Id = NextId("c"),
        Kind = seed.Kind,
        StatBlockNodeId = seed.StatBlockNodeId,
        Ordinal = ordinal,
        Name = name,
        MaxHp = Math.Max(0, seed.MaxHp),
        CurrentHp = Math.Max(0, seed.MaxHp),
        ArmourClass = Math.Max(0, seed.ArmourClass),
        PassivePerception = Math.Max(0, seed.PassivePerception),
    };

    private InitiativeEntry NewEntry(CombatantSeed seed, string label, IReadOnlyList<string> memberIds) => new InitiativeEntry
    {
        Id = NextId("e"),
        Sequence = nextSequence++,
        InitiativeModifier = seed.InitiativeModifier,
        Label = label,
        MemberIds = memberIds,
        Kind = seed.Kind,
        StatBlockNodeId = seed.StatBlockNodeId,
    };

    // The identity monsters are numbered and grouped under: the stat block when there is one, else
    // the typed name, so two unstatted "Cultist" adds still share a group.
    private static string TypeKey(CombatantSeed seed) => seed.StatBlockNodeId.Length > 0
        ? seed.StatBlockNodeId
        : $"name:{seed.Name.ToLowerInvariant()}";

    // The single row holding this type, or null when there is none or the group has been split (in
    // which case a further add opens a new row rather than guessing which split row it belongs to).
    // Only numbered rows count: Nib fights as a goblin, but goblins added after him get a row of
    // their own rather than joining his.
    private InitiativeEntry SoleEntryFor(string key)
    {
        List<InitiativeEntry> matches = entries
            .Where(entry => IsNumbered(entry) && string.Equals(TypeKeyOf(entry), key, StringComparison.Ordinal))
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    // Whether a row holds numbered members of a type ("Bugbear 1"): a group, or one of the rows it
    // was split into. A player, an ally, a one-off or a named individual is never numbered, even
    // when it uses the same stat block as a group in the fight. Caller holds gate.
    private bool IsNumbered(InitiativeEntry entry) =>
        entry.MemberIds.Count > 0
        && entry.MemberIds.All(id => combatants.TryGetValue(id, out Combatant member) && member.Ordinal > 0);

    // The rows one group was split into, earliest first: every numbered row sharing this one's stat
    // block. Empty for a row that is not numbered, or has no stat block. Caller holds gate.
    private List<InitiativeEntry> SplitRowsOf(InitiativeEntry row) =>
        row is null || row.StatBlockNodeId.Length == 0 || !IsNumbered(row)
            ? new List<InitiativeEntry>()
            : entries
                .Where(entry => IsNumbered(entry) && string.Equals(entry.StatBlockNodeId, row.StatBlockNodeId, StringComparison.Ordinal))
                .OrderBy(entry => entry.Sequence)
                .ToList();

    private string TypeKeyOf(InitiativeEntry entry) => entry.StatBlockNodeId.Length > 0
        ? entry.StatBlockNodeId
        : $"name:{BaseName(entry.MemberIds).ToLowerInvariant()}";

    private int TakeOrdinal(string key)
    {
        issuedOrdinals.TryGetValue(key, out int issued);
        issued++;
        issuedOrdinals[key] = issued;
        return issued;
    }

    // "Bugbear" -> "Bugbears" for a group row label. Crude on purpose: it is a label the DM can
    // rename, not data, and a wrong plural is a cosmetic nit rather than a mistake worth a
    // pluralisation library.
    private static string PluralLabel(string name)
    {
        if (name.Length == 0 || name.EndsWith('s'))
        {
            return name;
        }

        return name + "s";
    }

    // The name a numbered member was built from ("Bugbear 3" -> "Bugbear"), used to relabel a merged
    // group. Caller holds gate.
    private string BaseName(IReadOnlyList<string> memberIds)
    {
        if (memberIds.Count == 0 || !combatants.TryGetValue(memberIds[0], out Combatant first))
        {
            return string.Empty;
        }

        string suffix = " " + first.Ordinal.ToString(CultureInfo.InvariantCulture);
        return first.Ordinal > 0 && first.Name.EndsWith(suffix, StringComparison.Ordinal)
            ? first.Name[..^suffix.Length]
            : first.Name;
    }

    private string Named(string combatantId) =>
        combatants.TryGetValue(combatantId, out Combatant combatant) ? combatant.Name : combatantId;

    private InitiativeEntry Entry(string entryId) =>
        entries.Find(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));

    private string NextId(string prefix) => prefix + (++nextId).ToString(CultureInfo.InvariantCulture);

    private static void Roll(InitiativeEntry entry)
    {
        // A game die, not a security primitive: Random.Shared is exactly right here, and the DM can
        // overwrite whatever it produces.
        entry.Initiative = Random.Shared.Next(1, DieFaces + 1) + entry.InitiativeModifier;
        entry.HasInitiative = true;
    }

    private static int ClampHp(Combatant combatant, int value)
    {
        int floored = Math.Max(0, value);
        return combatant.MaxHp > 0 ? Math.Min(combatant.MaxHp, floored) : floored;
    }

    private void MutateEntry(string entryId, Action<InitiativeEntry> mutate)
    {
        bool changed;
        lock (gate)
        {
            InitiativeEntry entry = Entry(entryId);
            changed = entry is not null;
            if (changed)
            {
                mutate(entry);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    private void MutateCombatant(string combatantId, Action<Combatant> mutate)
    {
        bool changed;
        lock (gate)
        {
            changed = combatants.TryGetValue(combatantId, out Combatant combatant);
            if (changed)
            {
                mutate(combatant);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    private void AdjustSlot(string combatantId, int level, int delta)
    {
        if (level < 1)
        {
            return;
        }

        MutateCombatant(combatantId, combatant =>
        {
            SpentSlot existing = combatant.SpentSlots.FirstOrDefault(slot => slot.Level == level);
            int spent = Math.Max(0, (existing is null ? 0 : existing.Spent) + delta);
            combatant.SpentSlots = combatant.SpentSlots
                .Where(slot => slot.Level != level)
                .Append(new SpentSlot { Level = level, Spent = spent })
                .Where(slot => slot.Spent > 0)
                .OrderBy(slot => slot.Level)
                .ToArray();
        });
    }

    // Advance or rewind the turn marker over the rows that can still act. Caller does not hold gate.
    private void Step(int direction)
    {
        bool changed;
        lock (gate)
        {
            changed = entries.Count > 0;
            if (changed && !started)
            {
                // The first press starts the fight on the top row instead of skipping past it.
                started = true;
                turnIndex = 0;
                turnIndex = SkipDown(0, 1);
            }
            else if (changed)
            {
                int next = turnIndex + direction;
                if (next >= entries.Count)
                {
                    next = 0;
                    round++;
                }
                else if (next < 0)
                {
                    next = entries.Count - 1;
                    round = Math.Max(1, round - 1);
                }

                turnIndex = SkipDown(next, direction);
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    // Walk past rows whose every member is down, giving up after a full lap so an all-down field
    // still leaves the marker somewhere valid instead of spinning. Caller holds gate.
    private int SkipDown(int from, int direction)
    {
        int index = from;
        for (int steps = 0; steps < entries.Count; steps++)
        {
            if (!AllDown(entries[index]))
            {
                return index;
            }

            index += direction;
            if (index >= entries.Count)
            {
                index = 0;
            }
            else if (index < 0)
            {
                index = entries.Count - 1;
            }
        }

        return from;
    }

    private bool AllDown(InitiativeEntry entry) =>
        entry.MemberIds.Count > 0
        && entry.MemberIds.All(id => combatants.TryGetValue(id, out Combatant combatant) && combatant.Down);

    private string CurrentEntryId() =>
        started && turnIndex >= 0 && turnIndex < entries.Count ? entries[turnIndex].Id : string.Empty;

    // Put the turn marker back on the row it was on before the order changed. Caller holds gate.
    private void RestoreTurn(string entryId)
    {
        if (entryId.Length == 0)
        {
            ClampTurn();
            return;
        }

        int at = entries.FindIndex(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
        turnIndex = at >= 0 ? at : 0;
        ClampTurn();
    }

    // Put the turn marker back on the row it was on after a removal shuffled the list under it. A
    // row vanishing from above the marker must not silently hand the turn to that row's neighbour —
    // the DM would carry on playing whoever is highlighted. When the removed row is the one whose
    // turn it was, there is nothing to go back to, so the marker stays put and the turn lands on
    // whatever followed it (which is what removing the acting creature should do). Caller holds gate.
    private void KeepTurnAfterRemoval(string entryId)
    {
        int at = entries.FindIndex(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));
        if (at >= 0)
        {
            turnIndex = at;
        }

        ClampTurn();
    }

    // Keep the turn index inside the list after rows come and go. Caller holds gate.
    private void ClampTurn()
    {
        if (entries.Count == 0)
        {
            turnIndex = 0;
            return;
        }

        turnIndex = Math.Clamp(turnIndex, 0, entries.Count - 1);
    }

    private void Notify() => Changed?.Invoke();
}
