namespace DungeonTable.Core.Session;

/// <summary>
/// Everything about the one live table that must survive an app restart: what the players can see
/// and the fight that is running. This is the single document persisted per session — reveal sets,
/// fog cells and the initiative order are all plain JSON, which is why a document store fits and no
/// relational schema is hand-maintained.
/// </summary>
/// <remarks>
/// <para>
/// The app assumes <b>exactly one live table</b>, so in practice there
/// is one of these keyed by the configured session id. The key stays on the document anyway: it
/// costs nothing and is what a second concurrent game would need.
/// </para>
/// <para>
/// This is deliberately a <em>purpose-built document</em> rather than the live services' own types.
/// The live <c>SessionState</c> and <c>BattleState</c> hold things a restart must <b>not</b> restore
/// (the one-step clear-reveals undo) and things they never expose to a renderer but cannot lose (the
/// battle's id and ordinal counters — restoring combatants without them would hand the next creature
/// an id that is already taken). What is stored is therefore chosen here, once, explicitly.
/// </para>
/// </remarks>
public sealed class TableSnapshot
{
    /// <summary>
    /// The schema version this code writes. Bump it when a change to the stored shape cannot be
    /// read by the previous version, so an older build refuses the document instead of restoring a
    /// half-understood table.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> bumped when <see cref="Campaign"/> was added, nor when
    /// <c>Reveals.ShownArt</c> was. The contract is "bump when the previous version
    /// <em>cannot read</em> the new shape", and an older build reading a document with those
    /// present ignores them and restores the reveals and the fight correctly, losing only quest
    /// ticks and a staged picture. Bumping would make that build refuse the whole document —
    /// costing the reveals and the running fight too. Additive and default-empty means no bump.
    /// The same holds for the campaign's drawn cards moving from one deck's list to
    /// <c>Campaign.Decks</c>: each build ignores the other's field and loses only the drawn cards.
    /// </remarks>
    public const int CurrentVersion = 1;

    /// <summary>Stable session identifier; the document key.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The <see cref="CurrentVersion"/> of the code that wrote this document.</summary>
    public int Version { get; set; }

    /// <summary>When this snapshot was written, for the DM's "saved at" indicator.</summary>
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>What the players can currently see: the map, the reveals and the projector framing.</summary>
    public RevealSnapshot Reveals { get; set; } = new RevealSnapshot();

    /// <summary>The running fight, or an empty snapshot when none is.</summary>
    public BattleSnapshot Battle { get; set; } = new BattleSnapshot();

    /// <summary>How far the campaign's quests have got, and which secrets have been handed out.</summary>
    public CampaignSnapshot Campaign { get; set; } = new CampaignSnapshot();
}
