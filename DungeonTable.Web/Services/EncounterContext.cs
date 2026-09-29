using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// Finds what the area a fight started in says about one of its creatures, so the combatant detail
/// pane can show the "Bugbear Tactics" block next to the bugbear's stat block instead of sending the
/// DM back to the Info tab mid-round.
/// </summary>
/// <remarks>
/// <para>
/// Two ways in, deliberately: a block counts when the cross-reference resolver links it to the
/// creature's <b>stat-library node id</b>, <em>or</em> when the creature's own <b>name</b> appears in
/// the text. The node id is the precise route and covers every extracted monster; the name is what
/// catches a block the conservative resolver skipped (a heading like "Bugbear Tactics") and is the
/// only route at all for a combatant with no stat block, which a fight must never be blocked by.
/// </para>
/// <para>
/// A named individual (Grask, added from an area's creature list) fights with a bugbear's numbers
/// but is linked in the prose as himself, <c>[[grask|the bugbear chief]]</c>. So a link
/// to an entity or an NPC whose name is the combatant's counts as well.
/// </para>
/// <para>
/// Stateless and side-effect-free (the resolver caches per text), so it registers as a singleton.
/// </para>
/// </remarks>
public sealed class EncounterContext
{
    // A name short enough to appear inside unrelated words is not worth matching on ("Ox" would hit
    // "obnoxious"). Node-id matching still works for these.
    private const int ShortestNameMatch = 4;

    private readonly IDossierStore dossiers;
    private readonly ICrossRefResolver crossRefs;

    /// <summary>Creates the lookup over the dossier store and the cross-reference resolver.</summary>
    /// <param name="dossiers">Supplies the area's prose.</param>
    /// <param name="crossRefs">Finds the creature's own mentions inside that prose.</param>
    public EncounterContext(IDossierStore dossiers, ICrossRefResolver crossRefs)
    {
        this.dossiers = dossiers;
        this.crossRefs = crossRefs;
    }

    /// <summary>
    /// Lists the blocks of an area's dossier that mention one creature, in the order the book prints
    /// them (read-aloud, then the at-a-glance bullets, then the detail subsections).
    /// </summary>
    /// <param name="areaNodeId">The node id of the area the fight started in.</param>
    /// <param name="statBlockNodeId">The creature's stat-library node id, or an empty string.</param>
    /// <param name="creatureName">The combatant's display name ("Bugbear 2 (captain)").</param>
    /// <returns>The matching blocks (possibly empty — an area with no dossier yields none).</returns>
    public IReadOnlyList<EncounterNote> For(string areaNodeId, string statBlockNodeId, string creatureName)
    {
        if (string.IsNullOrWhiteSpace(areaNodeId))
        {
            return Array.Empty<EncounterNote>();
        }

        Result<AreaDossier> found = dossiers.GetArea(areaNodeId);
        if (!found.IsValid)
        {
            return Array.Empty<EncounterNote>();
        }

        AreaDossier dossier = found.Value;
        string name = NameKey(creatureName);
        HashSet<string> ids = IdsFor(statBlockNodeId ?? string.Empty, name);
        var notes = new List<EncounterNote>();

        if (Mentions(dossier.ReadAloud, ids, name))
        {
            notes.Add(new EncounterNote { Section = "read-aloud", Body = Readable(dossier.ReadAloud) });
        }

        notes.AddRange(Matching(dossier.Glance, "at a glance", ids, name));
        notes.AddRange(Matching(dossier.Detail, "detail", ids, name));
        return notes;
    }

    // Every id a link to this combatant could carry: its stat block's, and that of each entity or NPC
    // who has its name.
    private HashSet<string> IdsFor(string statBlockNodeId, string name)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (statBlockNodeId.Length > 0)
        {
            ids.Add(statBlockNodeId);
        }

        if (name.Length == 0)
        {
            return ids;
        }

        IEnumerable<(string Id, string Name)> named = dossiers.AllLevels()
            .SelectMany(level => level.AllEntities())
            .Select(entity => (entity.Id, entity.Name));

        Result<NpcRoster> npcs = dossiers.GetNpcs();
        if (npcs.IsValid)
        {
            named = named.Concat(npcs.Value.Npcs.Select(npc => (npc.Id, npc.Name)));
        }

        ids.UnionWith(named
            .Where(person => !string.IsNullOrEmpty(person.Id) && string.Equals(NameKey(person.Name), name, StringComparison.Ordinal))
            .Select(person => person.Id));

        return ids;
    }

    /// <summary>
    /// Reduces a combatant's display name to what to look for in the prose: "Bugbear 2 (captain)"
    /// becomes "bugbear", so a renamed and numbered member still finds its own creature's blocks.
    /// A plural in the prose ("bugbears") still matches, because the key is a substring.
    /// </summary>
    /// <param name="name">The combatant's display name.</param>
    /// <returns>The lower-cased key, or an empty string when nothing usable is left.</returns>
    private static string NameKey(string name)
    {
        string key = (name ?? string.Empty).Trim();

        // A DM's own suffix ("(captain)", "(the one with the horn)") says nothing about the species.
        int note = key.LastIndexOf('(');
        if (note > 0 && key.EndsWith(')'))
        {
            key = key[..note].TrimEnd();
        }

        // Drop the "2" of "Bugbear 2" — the ordinal is ours, never the book's.
        key = key.TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9').TrimEnd();
        return key.Length >= ShortestNameMatch ? key.ToLowerInvariant() : string.Empty;
    }

    private IEnumerable<EncounterNote> Matching(
        IReadOnlyList<DossierBlock> blocks,
        string section,
        HashSet<string> ids,
        string name) =>
        blocks
            .Where(block => Mentions(block.Heading, ids, name) || Mentions(block.Body, ids, name))
            .Select(block => new EncounterNote
            {
                Section = section,
                Heading = block.Heading,
                Body = Readable(block.Body),
                Secret = block.Secret,
            });

    // The pane shows the prose as text, not links, so an authored link shows its words rather than
    // its markup.
    private string Readable(string text) => CrossRefText.Display(text, crossRefs.Resolve(text));

    private bool Mentions(string text, HashSet<string> ids, string name)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (name.Length > 0 && text.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ids.Count > 0 && crossRefs.Resolve(text).Any(reference => ids.Contains(reference.TargetId));
    }
}
