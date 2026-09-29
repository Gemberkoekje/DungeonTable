namespace DungeonTable.Core.Dossier;

/// <summary>
/// The DM-facing dossier for one player character: the numbers a DM calls for mid-session, the
/// abilities that change how an encounter plays out, an abridged backstory, and the unresolved
/// threads the DM can pull on.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not the same thing as <c>Core.Battle.PartyMember</c>. That roster is live table
/// state the DM edits in the app and that seeds every fight — name, AC, hit points, initiative.
/// This is authored reference prose that changes only when a player levels or rewrites their
/// character, and it ships as a committed document beside the area dossiers.
/// </para>
/// <para>
/// The vitals are duplicated across both on purpose: the roster's copy is what the Battle tab
/// mutates, this one is what the Party tab prints, and a dossier that had to resolve against the
/// roster would show nothing at a table whose roster has been edited or cleared.
/// </para>
/// </remarks>
public sealed class CharacterDossier
{
    /// <summary>Stable slug used as the render key and the anchor ("maren", "oswin-tull").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The character's name as the table uses it ("Maren").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The player behind the character ("Robin"), or an empty string.</summary>
    public string PlayerName { get; init; } = string.Empty;

    /// <summary>The one-line build ("Human Cleric 2, Life Domain").</summary>
    public string Build { get; init; } = string.Empty;

    /// <summary>Character level; 0 when not recorded.</summary>
    public int Level { get; init; }

    /// <summary>Armour class; 0 when not recorded.</summary>
    public int ArmourClass { get; init; }

    /// <summary>What the AC is made of ("splint + shield"), or an empty string.</summary>
    public string ArmourNote { get; init; } = string.Empty;

    /// <summary>Maximum hit points; 0 when not recorded.</summary>
    public int MaxHp { get; init; }

    /// <summary>Passive Perception; 0 when not recorded.</summary>
    public int PassivePerception { get; init; }

    /// <summary>Initiative modifier.</summary>
    public int InitiativeModifier { get; init; }

    /// <summary>Movement as printed ("40 ft., plus climb 40 / swim 40").</summary>
    public string Speed { get; init; } = string.Empty;

    /// <summary>Special senses ("Darkvision 60 ft."), or an empty string.</summary>
    public string Senses { get; init; } = string.Empty;

    /// <summary>Saving throws as one line, proficient ones marked.</summary>
    public string Saves { get; init; } = string.Empty;

    /// <summary>The skills worth knowing at the table, as one line.</summary>
    public string Skills { get; init; } = string.Empty;

    /// <summary>Languages spoken, as one line.</summary>
    public string Languages { get; init; } = string.Empty;

    /// <summary>The character's own save DC and attack bonus, as printed ("DC 16, spell attack +8").</summary>
    public string SaveDc { get; init; } = string.Empty;

    /// <summary>Damage resistances and immunities, as one line. Empty when none.</summary>
    public string Defences { get; init; } = string.Empty;

    /// <summary>Notable weapon or unarmed attacks, as one line. Empty when none is recorded.</summary>
    public string Attacks { get; init; } = string.Empty;

    /// <summary>Magic items and notable gear, as one line. Empty when none is recorded.</summary>
    public string Kit { get; init; } = string.Empty;

    /// <summary>
    /// A warning the DM must read before running this character — a cover identity, a secret the
    /// rest of the table does not know. Rendered first and styled as DM-only. Empty when the
    /// character has no such secret.
    /// </summary>
    public string DmOnly { get; init; } = string.Empty;

    /// <summary>
    /// The abilities that change how an encounter plays out, one block each. Not a full spell list:
    /// the things worth knowing before writing a room.
    /// </summary>
    public IReadOnlyList<DossierBlock> Abilities { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>The abridged backstory, in prose.</summary>
    public string Backstory { get; init; } = string.Empty;

    /// <summary>The unresolved threads the DM can pull on, one block each.</summary>
    public IReadOnlyList<DossierBlock> Hooks { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>
    /// Things to confirm with the player: stale sheet numbers, ambiguous rulings, a missing
    /// component. Empty when the sheet is internally consistent.
    /// </summary>
    public IReadOnlyList<DossierBlock> Checks { get; init; } = Array.Empty<DossierBlock>();
}
