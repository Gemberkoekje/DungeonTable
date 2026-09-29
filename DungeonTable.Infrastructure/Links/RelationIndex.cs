using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Links;

/// <summary>
/// Every relation in the loaded content, read from both of its ends: the ones a level file declares,
/// the campaign-wide ones in <c>relations.json</c>, and the NPC contacts that name a person and a
/// kind. Built once and immutable afterwards, so it is safe to
/// share as a singleton.
/// </summary>
/// <remarks>
/// <para>
/// A relation whose ends do not both resolve to an individual, a faction, an item or an area is left
/// out rather than shown half-drawn; the content rules report it. So is a relation naming a stat
/// block or a spell: those are kinds of things, and a relation is between particular ones.
/// </para>
/// <para>
/// The same relation written more than once (the twins each list the other as family) is one
/// relation: a mutual kind whichever way round it is written, a directed one only when its direction
/// matches too. It is secret only while every copy is, keeps the first note written, and remembers
/// every NPC whose contacts recorded it.
/// </para>
/// <para>
/// A contact's role is not its relation's note. It is written about the contact from the NPC's side
/// ("his twin sister"), and would read wrongly on the other end's card, where the line is shown.
/// </para>
/// <para>
/// The book index's links join in too. Each is
/// <see cref="RelationKinds.Related"/> with the book's own verb as its note, so two links between the
/// same ends with different verbs stay two lines. From the linking end the verb is the phrase
/// ("ground by Wenna Brask"); from the other it is the note. And a book link may end at a stat
/// block or a spell: the book's lore is about kinds of things as often as particular ones.
/// </para>
/// </remarks>
public sealed class RelationIndex
{
    private static readonly IReadOnlyList<RelationLine> None = Array.Empty<RelationLine>();

    private readonly Dictionary<string, List<RelationLine>> byEnd;

    private RelationIndex(Dictionary<string, List<RelationLine>> byEnd, int count)
    {
        this.byEnd = byEnd;
        Count = count;
    }

    /// <summary>How many distinct relations resolved, for a quick check that anything was read.</summary>
    public int Count { get; }

    /// <summary>Reads every relation in the content, without a book index, and resolves its ends.</summary>
    /// <param name="dossiers">The content: level files, the campaign list and the NPC roster.</param>
    /// <param name="targets">Resolves each end the way a link target is resolved.</param>
    /// <param name="backlinks">Where each end is listed as being, for the "(8b)" on a line.</param>
    /// <returns>The index.</returns>
    public static RelationIndex Build(IDossierStore dossiers, LinkTargets targets, Backlinks backlinks) =>
        Build(dossiers, targets, backlinks, Array.Empty<BookIndex>());

    /// <summary>Reads every relation in the content and the book index, and resolves its ends.</summary>
    /// <param name="dossiers">The content: level files, the campaign list and the NPC roster.</param>
    /// <param name="targets">Resolves each end the way a link target is resolved.</param>
    /// <param name="backlinks">Where each end is listed as being, for the "(8b)" on a line.</param>
    /// <param name="books">The book index, whose links join the authored relations.</param>
    /// <returns>The index.</returns>
    public static RelationIndex Build(IDossierStore dossiers, LinkTargets targets, Backlinks backlinks, IEnumerable<BookIndex> books)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(backlinks);
        ArgumentNullException.ThrowIfNull(books);

        var merged = new Dictionary<string, Merged>(StringComparer.Ordinal);
        var order = new List<Merged>();
        foreach (Written written in Collect(dossiers).Concat(Collect(books)))
        {
            Result<LinkTarget> a = targets.Resolve(written.Relation.A, written.Floor);
            Result<LinkTarget> b = targets.Resolve(written.Relation.B, written.Floor);
            if (!IsEnd(a, written.Book) || !IsEnd(b, written.Book) || string.Equals(a.Value.TargetId, b.Value.TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            string kind = (written.Relation.Rel ?? string.Empty).Trim();
            string key = Key(kind, a.Value.TargetId, b.Value.TargetId, written.Relation.Note);
            if (!merged.TryGetValue(key, out Merged relation))
            {
                relation = new Merged(kind, a.Value, b.Value, written.Floor);
                merged[key] = relation;
                order.Add(relation);
            }

            relation.Absorb(written);
        }

        var byEnd = new Dictionary<string, List<RelationLine>>(StringComparer.Ordinal);
        foreach (Merged relation in order)
        {
            Add(byEnd, relation.A.TargetId, relation.Line(fromA: true, dossiers, backlinks));
            Add(byEnd, relation.B.TargetId, relation.Line(fromA: false, dossiers, backlinks));
        }

        return new RelationIndex(byEnd, order.Count);
    }

    /// <summary>The relations one end takes part in, read from its side, in the order they were written.</summary>
    /// <param name="id">An entity's, a person's or an area's id.</param>
    /// <returns>The lines (possibly empty).</returns>
    public IReadOnlyList<RelationLine> For(string id) =>
        !string.IsNullOrEmpty(id) && byEnd.TryGetValue(id, out List<RelationLine> lines) ? lines : None;

    // Every relation as written, with the floor its ends and note are read on and, for an NPC
    // contact, whose contact list it came from.
    private static IEnumerable<Written> Collect(IDossierStore dossiers)
    {
        foreach (LevelDossier level in dossiers.AllLevels())
        {
            foreach (Relation relation in level.Relations.Where(relation => relation is not null))
            {
                yield return new Written(relation, level.LevelNodeId, string.Empty, Book: false);
            }
        }

        Result<RelationList> campaign = dossiers.GetRelations();
        if (campaign.IsValid)
        {
            foreach (Relation relation in campaign.Value.Relations.Where(relation => relation is not null))
            {
                yield return new Written(relation, string.Empty, string.Empty, Book: false);
            }
        }

        // A contact joins in once it names the person and the kind.
        Result<NpcRoster> npcs = dossiers.GetNpcs();
        if (npcs.IsValid)
        {
            foreach (NpcDossier npc in npcs.Value.Npcs.Where(npc => npc is not null))
            {
                foreach (CastMember contact in npc.Contacts.Where(Joins))
                {
                    var relation = new Relation { A = npc.Id, Rel = contact.Rel, B = contact.Ref, Secret = contact.Secret };
                    yield return new Written(relation, string.Empty, npc.Id, Book: false);
                }
            }
        }
    }

    // The book index's links, on no floor: an area in them names its level ("L1 area 1").
    private static IEnumerable<Written> Collect(IEnumerable<BookIndex> books) =>
        books
            .Where(book => book is not null)
            .SelectMany(book => book.Links.Where(link => link is not null))
            .Select(link => new Written(link, string.Empty, string.Empty, Book: true));

    private static bool Joins(CastMember contact) =>
        contact is not null && !string.IsNullOrWhiteSpace(contact.Ref) && !string.IsNullOrWhiteSpace(contact.Rel);

    // An end is something particular that is loaded: not a stat block or a spell, and not an area on
    // a floor nobody has written yet. The book's links may end at a stat block or a spell too.
    private static bool IsEnd(Result<LinkTarget> end, bool book) =>
        end.IsValid && end.Value.TargetId.Length > 0 && (book || !end.Value.Rulebook);

    // A book link's verb is part of what it says, so two verbs between the same ends are two links.
    private static string Key(string kind, string a, string b, string note)
    {
        if (RelationKinds.IsMutual(kind) && string.CompareOrdinal(a, b) > 0)
        {
            (a, b) = (b, a);
        }

        string verb = string.Equals(kind, RelationKinds.Related, StringComparison.Ordinal) ? (note ?? string.Empty).Trim() : string.Empty;
        return $"{kind}\u001f{verb}\u001f{a}\u001f{b}";
    }

    private static void Add(Dictionary<string, List<RelationLine>> byEnd, string id, RelationLine line)
    {
        if (!byEnd.TryGetValue(id, out List<RelationLine> lines))
        {
            lines = new List<RelationLine>();
            byEnd[id] = lines;
        }

        lines.Add(line);
    }

    private sealed record Written(Relation Relation, string Floor, string ContactOf, bool Book);

    // One relation however many times it was written. Mutable only while the index is being built.
    private sealed class Merged
    {
        private readonly List<string> contactsOf = new List<string>();
        private bool secret = true;
        private string note = string.Empty;

        public Merged(string kind, LinkTarget a, LinkTarget b, string floor)
        {
            Kind = kind;
            A = a;
            B = b;
            Floor = floor;
        }

        public string Kind { get; }

        public LinkTarget A { get; }

        public LinkTarget B { get; }

        public string Floor { get; }

        public void Absorb(Written written)
        {
            secret = secret && written.Relation.Secret;
            if (note.Length == 0 && !string.IsNullOrWhiteSpace(written.Relation.Note))
            {
                note = written.Relation.Note;
            }

            if (written.ContactOf.Length > 0 && !contactsOf.Contains(written.ContactOf, StringComparer.Ordinal))
            {
                contactsOf.Add(written.ContactOf);
            }
        }

        public RelationLine Line(bool fromA, IDossierStore dossiers, Backlinks backlinks)
        {
            LinkTarget other = fromA ? B : A;

            // A book link reads best with its verb as the phrase, from the end that does the verb:
            // "ground by Wenna Brask" on the old mill's card. The other end keeps the
            // plain "related to" and shows the verb as the note.
            bool verb = fromA && note.Length > 0 && string.Equals(Kind, RelationKinds.Related, StringComparison.Ordinal);
            return new RelationLine
            {
                Kind = Kind,
                Phrase = verb ? note : RelationKinds.Phrase(Kind, fromA),
                OtherId = other.TargetId,
                OtherLabel = LabelOf(other, dossiers),
                OtherKind = other.Kind,
                OtherWhere = backlinks.For(other.TargetId)
                    .Where(area => area.Relation.Contains(Backlinks.InTheRoom, StringComparison.Ordinal))
                    .Select(area => LinkTargets.PrintedKeyOf(area.Label))
                    .Where(key => key.Length > 0)
                    .ToArray(),
                Note = verb ? string.Empty : note,
                Floor = Floor,
                Secret = secret,
                FromContactsOf = contactsOf.ToArray(),
            };
        }

        // An area shows its title, everything else the name a link to it would show.
        private static string LabelOf(LinkTarget target, IDossierStore dossiers)
        {
            if (target.Kind == CrossRefKind.Area)
            {
                Result<AreaDossier> area = dossiers.GetArea(target.TargetId);
                if (area.IsValid && area.Value.Title.Length > 0)
                {
                    return area.Value.Title;
                }
            }

            return target.DisplayName;
        }
    }
}
