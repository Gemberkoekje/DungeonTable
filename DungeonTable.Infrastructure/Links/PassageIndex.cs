using System.Text;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Links;

/// <summary>
/// Find anything's text half: every piece of the campaign's prose as a <see cref="Passage"/>, with its
/// words in plain lower case, so a query finds the paragraph that answers it ("ceiling" finds the rules
/// section that says how high the ceilings are) rather than only things by their names. Built once per
/// content snapshot and immutable afterwards, so it is safe to share.
/// </summary>
/// <remarks>
/// <para>
/// A passage matches when it holds every word of the query, in its heading or its body, in any order.
/// Hits rank by where the words are: the whole query in the heading, every word in the heading, the whole
/// query in the body, then every word anywhere; within a rank the rules and the floor's facts come before
/// the rooms, and the rooms before the rest (<see cref="PassageKind"/>), then the content's own order.
/// </para>
/// <para>
/// Words are matched in the prose as the DM reads it, with each link shown as its words, so
/// "[[grask|the bugbear]]" is found by "bugbear" and not by its id. Plain substring tests over a few
/// thousand passages take well under a millisecond a keystroke, so nothing cleverer is kept.
/// </para>
/// </remarks>
public sealed class PassageIndex
{
    // How much of a passage a result shows around the first match, in characters.
    private const int Before = 60;
    private const int After = 140;

    // A passage this short is shown whole.
    private const int Whole = Before + After;

    private const string Ellipsis = "…";
    private const string Separator = " › ";

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<string, Passage> byKey = new Dictionary<string, Passage>(StringComparer.Ordinal);
    private readonly HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
    private readonly Func<string, string, string> plain;

    private PassageIndex(Func<string, string, string> plain)
    {
        this.plain = plain;
    }

    /// <summary>How many passages the index holds.</summary>
    public int Count => entries.Count;

    /// <summary>Indexes every passage of the content.</summary>
    /// <param name="dossiers">The authored content.</param>
    /// <param name="plain">
    /// Turns prose read on a floor (the level node id, or an empty string for none) into the words the DM
    /// reads, each link as its words. Null shows each link as its shown text or target.
    /// </param>
    /// <returns>The index.</returns>
    public static PassageIndex Build(IDossierStore dossiers, Func<string, string, string> plain)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        var index = new PassageIndex(plain ?? ((text, _) => LinkMarkup.Plain(text)));
        index.AddRules(dossiers);
        index.AddLevels(dossiers);
        index.AddAreas(dossiers);
        index.AddNpcs(dossiers);
        index.AddQuests(dossiers);
        index.AddSessions(dossiers);
        index.AddDecks(dossiers);
        index.AddCampaign(dossiers);
        index.AddParty(dossiers);
        index.AddStory(dossiers);
        return index;
    }

    /// <summary>The passage with a key, as it stands in this content.</summary>
    /// <param name="key">A key from <see cref="PassageKeys"/> or a <see cref="TextMatch"/>.</param>
    /// <returns>The passage, or an invalid result when no passage has that key.</returns>
    public Result<Passage> Get(string key) =>
        !string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out Passage passage)
            ? Result.For(passage)
            : Result.WithMessages<Passage>(ValidationMessage.Error($"No passage has the key '{key}'.", nameof(key)));

    /// <summary>The passages that hold every word of a query, best first.</summary>
    /// <param name="query">The words looked for; a blank query finds nothing.</param>
    /// <param name="limit">The most hits to return.</param>
    /// <returns>The hits, best first (possibly empty).</returns>
    public IReadOnlyList<TextMatch> Find(string query, int limit)
    {
        string[] words = Words(query);
        if (words.Length == 0 || limit <= 0)
        {
            return Array.Empty<TextMatch>();
        }

        string phrase = string.Join(' ', words);
        return entries
            .Select((entry, order) => (Entry: entry, Order: order, Rank: Rank(entry, words, phrase)))
            .Where(hit => hit.Rank >= 0)
            .OrderBy(hit => hit.Rank)
            .ThenBy(hit => (int)hit.Entry.Passage.Kind)
            .ThenBy(hit => hit.Order)
            .Take(limit)
            .Select(hit => new TextMatch { Passage = hit.Entry.Passage, Snippet = Snippet(hit.Entry, words, phrase) })
            .ToList();
    }

    /// <summary>
    /// The words of a query, as the index compares them: lower case, split on white space, each once.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The words (possibly none).</returns>
    public static string[] Words(string query) =>
        (query ?? string.Empty)
            .ToLowerInvariant()
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    // Lower sorts first; -1 means the passage lacks a word.
    private static int Rank(Entry entry, string[] words, string phrase)
    {
        if (!words.All(word => entry.Heading.Contains(word, StringComparison.Ordinal) || entry.Lower.Contains(word, StringComparison.Ordinal)))
        {
            return -1;
        }

        if (entry.Heading.Contains(phrase, StringComparison.Ordinal))
        {
            return 0;
        }

        if (words.All(word => entry.Heading.Contains(word, StringComparison.Ordinal)))
        {
            return 1;
        }

        return entry.Lower.Contains(phrase, StringComparison.Ordinal) ? 2 : 3;
    }

    // The body around the first match: the whole query where it stands together, else the first word that
    // is in the body at all. A passage matched on its heading alone shows the start of its body.
    private static string Snippet(Entry entry, string[] words, string phrase)
    {
        string text = entry.Plain;
        if (text.Length <= Whole)
        {
            return text;
        }

        int at = entry.Lower.IndexOf(phrase, StringComparison.Ordinal);
        int length = phrase.Length;
        if (at < 0)
        {
            at = words
                .Select(word => entry.Lower.IndexOf(word, StringComparison.Ordinal))
                .Where(found => found >= 0)
                .DefaultIfEmpty(0)
                .Min();
            length = 0;
        }

        int start = Math.Max(0, at - Before);
        int end = Math.Min(text.Length, at + length + After);
        start = WordStart(text, start);
        end = WordEnd(text, end);

        string cut = text[start..end].Trim();
        return (start > 0 ? Ellipsis : string.Empty) + cut + (end < text.Length ? Ellipsis : string.Empty);
    }

    // Moves a cut forward to the start of the next word, unless it already sits at one.
    private static int WordStart(string text, int index)
    {
        if (index == 0 || char.IsWhiteSpace(text[index - 1]))
        {
            return index;
        }

        int next = text.IndexOf(' ', index);
        return next < 0 ? index : next + 1;
    }

    // Moves a cut back to the end of the word before it, unless it already sits at one.
    private static int WordEnd(string text, int index)
    {
        if (index >= text.Length || char.IsWhiteSpace(text[index]))
        {
            return index;
        }

        int previous = text.LastIndexOf(' ', index);
        return previous <= 0 ? index : previous;
    }

    // Prose with its line breaks and runs of spaces as single spaces, so a snippet reads as one line.
    private static string OneLine(string text)
    {
        var builder = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string Trail(params string[] parts) =>
        string.Join(Separator, parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));

    // ---- Building ------------------------------------------------------------------------

    private void Add(string key, PassageKind kind, string title, string trail, string body, bool secret, string floor, string owner)
    {
        // A heading with nothing under it (a room with no read-aloud) answers nothing.
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var passage = new Passage
        {
            Key = PassageKeys.Unique(key, used),
            Kind = kind,
            Title = (title ?? string.Empty).Trim(),
            Trail = trail,
            Body = body ?? string.Empty,
            Secret = secret,
            Floor = floor ?? string.Empty,
            OwnerId = owner ?? string.Empty,
        };

        string words = OneLine(plain(passage.Body, passage.Floor));
        entries.Add(new Entry(passage, OneLine(plain(passage.Title, passage.Floor)).ToLowerInvariant(), words, words.ToLowerInvariant()));
        byKey[passage.Key] = passage;
    }

    // Blocks under one scope, keyed as PassageKeys.Blocks keys them, so the DM screen's pin buttons name
    // the same passages.
    private void AddBlocks(string scope, IReadOnlyList<DossierBlock> blocks, PassageKind kind, string trail, string floor, string owner)
    {
        IReadOnlyList<string> keys = PassageKeys.Blocks(scope, blocks);
        for (int i = 0; i < blocks.Count; i++)
        {
            DossierBlock block = blocks[i];
            if (block is not null)
            {
                Add(keys[i], kind, block.Heading, trail, block.Body, block.Secret, floor, owner);
            }
        }
    }

    private void AddRules(IDossierStore dossiers)
    {
        Result<ReferenceLibrary> reference = dossiers.GetReference();
        if (reference.IsValid)
        {
            ReferenceLibrary rules = reference.Value;
            AddBlocks("rules", rules.Sections, PassageKind.Rule, rules.Title.Length > 0 ? rules.Title : "Rules", string.Empty, string.Empty);
        }
    }

    private void AddLevels(IDossierStore dossiers)
    {
        foreach (LevelDossier level in dossiers.AllLevels().Where(level => level is not null && level.LevelNodeId.Length > 0))
        {
            string id = level.LevelNodeId;
            string title = level.Title.Length > 0 ? level.Title : id;
            Add(PassageKeys.LevelOverview(id), PassageKind.Level, "What dwells here", title, level.WhatDwellsHere, false, id, string.Empty);
            AddBlocks($"level/{id}/factions", level.Factions, PassageKind.Level, Trail(title, "Factions"), id, string.Empty);
            AddBlocks($"level/{id}/wandering", level.WanderingMonsters, PassageKind.Level, Trail(title, "Wandering monsters"), id, string.Empty);
            Add(PassageKeys.LevelAftermath(id), PassageKind.Level, "Aftermath", title, level.Aftermath, false, id, string.Empty);

            foreach (Entity entity in level.Entities.Where(entity => entity is not null && entity.Id.Length > 0))
            {
                Add($"entity/{entity.Id}", PassageKind.Entity, entity.Name.Length > 0 ? entity.Name : entity.Id, title, entity.Description, entity.Secret, id, entity.Id);
            }
        }
    }

    private void AddAreas(IDossierStore dossiers)
    {
        foreach (AreaDossier area in dossiers.AllAreas().Where(area => area is not null && area.AreaNodeId.Length > 0))
        {
            string id = area.AreaNodeId;
            Result<LevelDossier> level = dossiers.GetLevelForArea(id);
            string floor = level.IsValid ? level.Value.LevelNodeId : string.Empty;
            string title = area.Title.Length > 0 ? area.Title : id;
            Add($"area/{id}/read-aloud", PassageKind.Area, "Read-aloud", title, area.ReadAloud, false, floor, id);
            AddBlocks($"area/{id}/glance", area.Glance, PassageKind.Area, Trail(title, "At a glance"), floor, id);
            AddBlocks($"area/{id}/detail", area.Detail, PassageKind.Area, Trail(title, "Detail"), floor, id);
        }
    }

    private void AddNpcs(IDossierStore dossiers)
    {
        Result<NpcRoster> roster = dossiers.GetNpcs();
        if (!roster.IsValid)
        {
            return;
        }

        foreach (NpcDossier npc in roster.Value.Npcs.Where(npc => npc is not null && npc.Id.Length > 0))
        {
            string name = npc.Name.Length > 0 ? npc.Name : npc.Id;
            string about = string.Join(" ", new[] { npc.Role, npc.Where, npc.Disposition, npc.Appearance, npc.Summary }.Where(part => !string.IsNullOrWhiteSpace(part)));
            Add($"npc/{npc.Id}", PassageKind.Npc, name, "NPCs", about, false, string.Empty, npc.Id);
            AddBlocks($"npc/{npc.Id}/knows", npc.Knows, PassageKind.Npc, Trail(name, "Knows"), string.Empty, npc.Id);
            AddBlocks($"npc/{npc.Id}/detail", npc.Detail, PassageKind.Npc, Trail(name, "Detail"), string.Empty, npc.Id);
            AddBlocks($"npc/{npc.Id}/hooks", npc.Hooks, PassageKind.Npc, Trail(name, "Hooks"), string.Empty, npc.Id);
        }
    }

    private void AddQuests(IDossierStore dossiers)
    {
        Result<QuestLog> log = dossiers.GetQuests();
        if (!log.IsValid)
        {
            return;
        }

        foreach (Quest quest in log.Value.Quests.Where(quest => quest is not null && quest.Id.Length > 0))
        {
            string title = quest.Title.Length > 0 ? quest.Title : quest.Id;
            string trail = Trail("Quests", title);
            Add($"quest/{quest.Id}/hook", PassageKind.Quest, title, "Quests", quest.Hook, false, string.Empty, string.Empty);
            var beats = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestBeat beat in quest.Beats.Where(beat => beat is not null))
            {
                string name = beat.Id.Length > 0 ? beat.Id : beat.Summary;
                string body = string.IsNullOrWhiteSpace(beat.Detail) ? beat.Summary : beat.Detail;
                Add(PassageKeys.Unique($"quest/{quest.Id}/beats/{PassageKeys.Slug(name)}", beats), PassageKind.Quest, beat.Summary, trail, body, beat.Secret, string.Empty, string.Empty);
            }

            AddBlocks($"quest/{quest.Id}/rewards", quest.Rewards, PassageKind.Quest, Trail(trail, "Rewards"), string.Empty, string.Empty);
            AddBlocks($"quest/{quest.Id}/detail", quest.Detail, PassageKind.Quest, Trail(trail, "Detail"), string.Empty, string.Empty);
        }
    }

    private void AddSessions(IDossierStore dossiers)
    {
        Result<SessionLog> log = dossiers.GetSessions();
        if (!log.IsValid)
        {
            return;
        }

        foreach (SessionPlan session in log.Value.Sessions.Where(session => session is not null))
        {
            string title = session.Title.Length > 0 ? session.Title : session.Id;
            string scope = $"session/{PassageKeys.Slug(session.Id.Length > 0 ? session.Id : session.Title)}";
            Add($"{scope}/summary", PassageKind.Session, title, "Sessions", session.Summary, false, string.Empty, string.Empty);
            AddBlocks($"{scope}/canon", session.Canon, PassageKind.Session, Trail("Sessions", title, "Canon"), string.Empty, string.Empty);
            var scenes = new HashSet<string>(StringComparer.Ordinal);
            foreach (SessionScene scene in session.Scenes.Where(scene => scene is not null))
            {
                string sceneScope = PassageKeys.Unique($"{scope}/scenes/{PassageKeys.Slug(scene.Id.Length > 0 ? scene.Id : scene.Title)}", scenes);
                string about = string.Join(" ", new[] { scene.When, scene.Cast, scene.Purpose }.Where(part => !string.IsNullOrWhiteSpace(part)));
                Add(sceneScope, PassageKind.Session, scene.Title, Trail("Sessions", title), about, false, string.Empty, string.Empty);
                AddBlocks($"{sceneScope}/beats", scene.Beats, PassageKind.Session, Trail("Sessions", title, scene.Title), string.Empty, string.Empty);
            }

            AddBlocks($"{scope}/decisions", session.Decisions, PassageKind.Session, Trail("Sessions", title, "Decisions"), string.Empty, string.Empty);
        }
    }

    private void AddDecks(IDossierStore dossiers)
    {
        foreach (CardDeck deck in dossiers.AllDecks().Where(deck => deck is not null && deck.Id.Length > 0))
        {
            string title = deck.Title.Length > 0 ? deck.Title : deck.Id;
            var cards = new HashSet<string>(StringComparer.Ordinal);
            foreach (DeckCard card in deck.Cards.Where(card => card is not null))
            {
                string effects = string.Join(" ", card.Effects.Where(effect => effect is not null).Select(effect => $"{effect.Heading}: {effect.Body}"));
                string body = string.Join(" ", new[] { card.Subtitle, card.Body, effects }.Where(part => !string.IsNullOrWhiteSpace(part)));
                string key = PassageKeys.Unique($"deck/{deck.Id}/{PassageKeys.Slug(card.Id.Length > 0 ? card.Id : card.Name)}", cards);
                Add(key, PassageKind.Deck, card.Name, title, body, false, string.Empty, string.Empty);
            }
        }
    }

    private void AddCampaign(IDossierStore dossiers)
    {
        Result<CampaignDossier> found = dossiers.GetCampaign();
        if (!found.IsValid)
        {
            return;
        }

        CampaignDossier campaign = found.Value;
        string title = campaign.Title.Length > 0 ? campaign.Title : "Campaign";
        AddBlocks("campaign", campaign.Sections, PassageKind.Campaign, title, string.Empty, string.Empty);
        Add("campaign/levels", PassageKind.Campaign, campaign.LevelsHeading.Length > 0 ? campaign.LevelsHeading : "Levels", title, campaign.LevelsNote, false, string.Empty, string.Empty);
        AddBlocks("campaign/bonus", campaign.BonusXp, PassageKind.Campaign, title, string.Empty, string.Empty);
    }

    private void AddParty(IDossierStore dossiers)
    {
        Result<PartyDossier> found = dossiers.GetParty();
        if (!found.IsValid)
        {
            return;
        }

        PartyDossier party = found.Value;
        string title = party.Title.Length > 0 ? party.Title : "Party";
        Add("party/summary", PassageKind.Party, title, "Party", party.Summary, false, string.Empty, string.Empty);
        AddBlocks("party/notes", party.Notes, PassageKind.Party, Trail(title, "Notes"), string.Empty, string.Empty);
        AddBlocks("party/questions", party.OpenQuestions, PassageKind.Party, Trail(title, "Open questions"), string.Empty, string.Empty);

        foreach (CharacterDossier character in party.Characters.Where(character => character is not null && character.Id.Length > 0))
        {
            string name = character.Name.Length > 0 ? character.Name : character.Id;
            string sheet = string.Join(" ", new[] { character.Build, character.Senses, character.Defences, character.Attacks, character.Kit, character.Backstory, character.DmOnly }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            Add($"character/{character.Id}", PassageKind.Party, name, title, sheet, false, string.Empty, character.Id);
            AddBlocks($"character/{character.Id}/abilities", character.Abilities, PassageKind.Party, Trail(name, "Abilities"), string.Empty, character.Id);
            AddBlocks($"character/{character.Id}/hooks", character.Hooks, PassageKind.Party, Trail(name, "Hooks"), string.Empty, character.Id);
            AddBlocks($"character/{character.Id}/checks", character.Checks, PassageKind.Party, Trail(name, "Checks"), string.Empty, character.Id);
        }
    }

    private void AddStory(IDossierStore dossiers)
    {
        Result<StoryLibrary> found = dossiers.GetStory();
        if (!found.IsValid)
        {
            return;
        }

        var chapters = new HashSet<string>(StringComparer.Ordinal);
        foreach (StoryDossier chapter in found.Value.Chapters.Where(chapter => chapter is not null))
        {
            string title = chapter.Title.Length > 0 ? chapter.Title : "Story";
            string scope = PassageKeys.Unique($"story/{PassageKeys.Slug(chapter.Title)}", chapters);
            Add($"{scope}/summary", PassageKind.Story, title, "Story", chapter.Summary, false, string.Empty, string.Empty);
            AddBlocks($"{scope}/sections", chapter.Sections, PassageKind.Story, Trail("Story", title), string.Empty, string.Empty);
            AddBlocks($"{scope}/loose-ends", chapter.LooseEnds, PassageKind.Story, Trail("Story", title, "Loose ends"), string.Empty, string.Empty);
        }
    }

    // A passage with its heading and body in the words the DM reads: the heading in lower case, the body
    // as shown and in lower case.
    private sealed record Entry(Passage Passage, string Heading, string Plain, string Lower);
}
