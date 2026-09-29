# The content format

_To understand this, open the [sample pack](../samples/demo/README.md) beside it: every kind of
document below is there once, small enough to read in a sitting. This page says what each field does;
the pack shows it in use. For how the app is built around the content, see
[architecture.md](architecture.md)._

## 1. A content root

Everything the app shows comes from one folder, the **content root**:

```
<content root>/
  Maps/<adventure>/<map>.ds              a map drawn in Dungeon Scrawl
  Maps/<adventure>/<map>.regions.json    what the Room Editor adds to it
  data/dossiers/                         the campaign: levels, quests, people, rules, decks...
  data/statblocks/                       monsters*.json and spells*.json
  data/art/                              catalogue.json and the pictures it lists
  data/book-index/                       what the books say (optional)
  data/party.json, data/allies.json      the battle rosters
```

### Where the app looks

Set `Content:Root` to that folder. Each kind of content can also be put elsewhere with its own
setting, which wins over `Content:Root`:

| content | under the content root | its own setting |
|---|---|---|
| maps | `Maps/` | `Maps:Root` |
| dossiers | `data/dossiers/` | `Dossiers:Root` |
| stat blocks | `data/statblocks/` | `Stats:Root` |
| art | `data/art/` | `Art:Root` |
| rosters | `data/` | `Rosters:Root` |
| book index | `data/book-index/` | `BookIndex:Root` |

A relative path is read from the web app's own folder. A setting that names a folder that does not
exist is passed over. When neither finds a folder, the app walks up from its own folder looking for
the usual place in each parent, which is how the Docker image finds `/app/Maps` and `/app/data`.

Every folder is required except the book index: the app will not start without it, and says which
setting to fix. Inside the folders, every document is optional. A tab whose document is missing says
that nothing is written for it.

### When it is read

Everything is read at startup, and **read again while the app runs whenever one of its files changes**.
Save a document, by hand or from an LLM, and the open pages show it a moment later, with no restart:
the DM screen keeps its room, card and tab, and the projector keeps what was revealed. The rosters are
read each time they are used.

- **A document that stops parsing does not take anything away.** When a file the table could read
  cannot be read any more (a typo, or a file caught half-saved), the table keeps the content it had
  until the file is fixed and saved again, and the log names the file and says why.
- **A running fight keeps its numbers**: an edited stat block reaches the next monster added to it, and
  the combatant's detail pane, but not the hit points and armour class the fight started with.
- **The Room Editor keeps the map it has open**, since its own save is what usually changed it; pick
  the map again to see a hand edit to its `.regions.json` there. The DM screen and the projector read a
  changed map at once.
- **In a container**, a folder bind-mounted from a Windows or macOS host sends no change events, so set
  `DOTNET_USE_POLLING_FILE_WATCHER=true` and the app looks every few seconds instead. Both compose files
  set it.
- `Content:Watch=false` turns watching off: content is then read once, at startup.

### What the app writes

- **The Room Editor** rewrites `Maps/<adventure>/<map>.regions.json` when you save. It writes a
  temporary file beside it and then moves that over the old one, so a crash mid-save leaves the last
  save whole.
- **Pictures uploaded on the Art tab** go to `data/art/uploads/`, with a `catalogue.json` of their
  own. Keep that folder out of version control: it is table state, not content.
- **The rosters** (`data/party.json`, `data/allies.json`) are written by the party and ally editors,
  but only when the app has no database. With one, they live in the database, and the files only seed
  a new table.

Nothing else is ever written: the dossiers, stat blocks, book index and art catalogue are yours.
What happens at the table (what was revealed, the fight, quest ticks, drawn cards, notes) is saved in
the database, or nowhere.

## 2. How documents are read

Every document is JSON:

- **Property names are camelCase**, as in the sample pack; the app matches them ignoring case.
- **Comments and trailing commas are allowed.**
- **Unknown properties are ignored**, so a misspelt field name is silently not there.
- **Enum values are written in camelCase** (`"questComplete"`, `"hiddenDoor"`). A value the app does
  not know loses the **whole document**. That is why relation kinds and book entry kinds are plain
  strings instead.
- **A text field may hold a number or true/false** (`"count": 2` reads as "2"), in every document.
- **`null` reads as if the field were left out**: text is empty, a number is 0, a flag is false, a kind
  is none, and an object is empty. Write `null` for "nothing here" if that is easier, as LLMs often do.

The app is **fail-soft**, so a mistake never takes the table down in the middle of a session:

| mistake | what happens |
|---|---|
| a document that is not valid JSON | it is skipped. A map's `.regions.json` is the exception: the DM screen warns that it could not be read, the player view shows nothing rather than risk showing secrets, and the Room Editor will not save over it |
| a list written as `null` | it reads as empty |
| a `null` entry in a list | it is dropped |
| an entry with no id | it is dropped: an area, quest, deck, stat block, spell or picture |
| two entries with one id | the later one wins, in the earlier one's place. Files are read in ordinal order of their names |
| a field written as `null` | it reads as if it were left out |

Fail-soft means a typo can go unnoticed, so **the app says in its log, as it starts**, which documents
it skipped and which entries it dropped, file by file and why (`Content problem in
data/dossiers/npcs.json: ...`). [The content tests](#9-the-content-tests) report every one of these
too, and much more.

### File names

| folder | name | what it is |
|---|---|---|
| `data/dossiers/` | `level-<n>.json` | one floor each; `n` is its floor number (below) |
| | `campaign.json`, `reference.json`, `npcs.json`, `characters.json`, `sessions.json`, `story.json`, `relations.json` | one of each |
| | `quests*.json` | every one is read, and they merge |
| | `deck-*.json` | one deck each, known by the `id` inside, not by the file name |
| `data/statblocks/` | `monsters*.json`, `spells*.json` | every one is read, and they merge |
| `data/book-index/` | `<book>.json` | one book each |
| `data/art/` | `catalogue.json` | the pictures, by a path under `data/art/` |
| `data/` | `party.json`, `allies.json` | the battle rosters |
| `Maps/<adventure>/` | `<map>.ds` and `<map>.regions.json` | a map, one folder below `Maps/` |

- **Merging:** files are read in ordinal order of their names, and a later file wins an id both use.
  `-` sorts before `.`, so `monsters.json` is read after `monsters-srd.json`, and a campaign's own
  block replaces the SRD's under the same `nodeId`.
- **The floor number** comes from the file name only: `level-` and then digits, above zero
  (`level-01.json` is floor 1). `level-bonus.json` loads, but on no floor, so no `L<n>` link, exit or
  quest target can reach it. Two files with one number make every link to that floor ambiguous.
- **Letter case** matters on Linux, where the app usually runs, and not on Windows. A file called
  `Level-2.json` loads on a Windows laptop and not on the server. Write names in lower case, and write
  a picture's path with the exact case of its file.
- The dossier, stat block and book index folders are read at their top level only; maps are found at
  any depth, but keep each one exactly one folder below `Maps/`: the folder is its adventure.

## 3. Ids

The app never makes up an id: every id is a string you write, and the app compares ids exactly. What
an id looks like is up to you, with three exceptions:

- **Entity ids and book entry ids must be slugs** (`bell-warden`): lower case, words joined by `-`.
- **Area ids follow the pattern `data_dossiers_level_<n>_area_<key>`** wherever an area is named
  before its floor is written: a quest destination, or a map region on a floor with no level file yet.
  The id is then checked against the one the area will have, and the link lights up by itself the day
  the floor is written. The sample pack uses the pattern everywhere.
- **A map's id is its `.ds` file's name**, and its adventure is the folder the file is in.

**Renaming an id loses what the table saved under it.** Reveals are saved by map and region, quest
ticks by quest and beat, drawn cards by deck and card, the picture on show by its id. Rename those
between sessions and the saved state for the old id is gone.

Ids that have to agree across documents:

- a region's `graphNodeId`, a quest target's `nodeId` and `campaign.json`'s `startAreaNodeId` are area
  ids;
- a quest's `anyOfQuestIds` are quest ids;
- `allies.json`'s `statBlockNodeId`, a picture's `nodeIds` and a spellcasting block's spell `nodeId`
  are the exact `nodeId` of a stat block or spell;
- `party.json`'s members are the characters in `characters.json`, by name, with the same level, AC,
  hit points, passive Perception, initiative and player;
- a `.regions.json` names its map and adventure the way its `.ds` file does;
- everything else that names something (a creature, an exit, a relation's ends, a contact) names it
  the way a link does.

## 4. Links

In any field shown as prose, `[[target]]` or `[[target|shown text]]` is a link. Everything else is
plain text: **the app never turns a name into a link by itself**, because a guessed link is worse than
none.

### What a link can name

| target | written as | opens |
|---|---|---|
| an area on the same floor | `[[area 3a]]`, `[[Area 3A]]`, `[[area-3a]]` | the area |
| an area on another floor | `[[L2 area 1]]` | the area |
| a person | the id from `npcs.json` or `characters.json`: `[[wenna-brask]]` | their card |
| an entity a level declares | its id: `[[bell-warden]]`, `[[grasks-crew]]` | its card |
| a stat block or spell | its name: `[[bugbear]]`, `[[Sacred Flame]]`, `[[sacred-flame]]` | the full block |
| a book index entry | its id: `[[the-old-mill]]` | what the book says |

- Everything but an area is matched as a **slug**: lower case, apostrophes dropped, anything that is
  not a letter or digit turned into `-`. So `[[Wenna Brask]]` finds `wenna-brask`, and "Grask's crew"
  is `grasks-crew`.
- An **area** is named by the key printed on the map, which the app reads from the area's title: the
  first `Area <number><letter>` in it (`Undercroft Landing (Level 1, Area 1)`), or its `areaNumber`
  when the title has none. A key has at most one letter.
- The campaign's own content wins over the book index: a book entry is only a link target where nothing
  the campaign wrote has its name.
- **A target that could mean two things resolves to neither.** Make names unique.

### What a link shows

The shown text, when there is some. Otherwise an area shows as written ("area 3a"); a stat block or
spell shows its name in lower case ("a bugbear"), unless the target starts with a capital
(`[[Gargoyle]]`); and a person, entity or book entry shows its `name` exactly as written. So leave
articles out of names: an entity called "the Bell-Warden", linked as `the [[bell-warden]]`, reads "the
the Bell-Warden".

A link that resolves to nothing is shown as its words, marked as broken, and does nothing. A link to a
floor with no level file yet (`[[L2 area 1]]` while only floor 1 is written) is **pending**: a muted
label now, and a live link, by itself, the day `level-2.json` exists.

### Which floor a bare `[[area 3a]]` means

- In a level file (its areas, entities, notes and relations) it means that file's floor.
- Everywhere else (quests, people, sessions, the story, decks, `campaign.json`, `reference.json`,
  `relations.json`) there is no floor to read it on, so **write the floor: `[[L1 area 3a]]`**. A bare
  `[[area 3a]]` there is shown as broken.

### Where links work

Only in the fields shown as prose; brackets anywhere else are shown as brackets, and the content tests
flag them:

- the body of every block (§5.1), in every document;
- an area's `readAloud`, and the `note` of its creatures and exits;
- a level's `whatDwellsHere` and `aftermath`, an entity's `description`, a relation's `note`;
- `npcs.json`'s `summary`, and an NPC's `summary` and `appearance`;
- `characters.json`'s `summary`, and a character's `dmOnly`, `kit` and `backstory`;
- a quest's `hook`, and a beat's `summary` and `detail`;
- `sessions.json`'s `summary`, a session's `summary` and a scene's `purpose`;
- a story chapter's `summary`;
- a card's `body`.

## 5. The documents

In the tables, a field marked **required** is one without which the entry is dropped. Everything else
is optional to the app, but the content tests expect most of it to be written; section 9 says which.
Where a field says "not shown", the app loads it but does not display it today.

### 5.1 Blocks

Most documents hold lists of **blocks**: a heading and a body, shown as a collapsible section or a
line in a list.

| field | what it does |
|---|---|
| `heading` | the heading, as plain text |
| `body` | the text, as prose |
| `secret` | marks the block as the DM's only, with a badge |
| `kind` | `none`, `exits`, `feature`, `loot`, `secret` or `lore`. Used only on an area's `glance` items, to style them |
| `id` | used only on a level's `factions`, where it makes the faction an entity (§5.2) |

### 5.2 `level-<n>.json`: a floor

```json
{
  "level": { "levelNodeId": "data_dossiers_level_1", "title": "Level 1: The Undercroft", ... },
  "areas": [ { "areaNodeId": "data_dossiers_level_1_area_1", "title": "Undercroft Landing (Level 1, Area 1)", ... } ]
}
```

**`level`**, shown on the Level tab:

| field | what it does |
|---|---|
| `levelNodeId` | the floor's id. Without one, its areas still load, but on no floor: no `[[area …]]` link reaches them |
| `title` | the tab's heading |
| `designedFor` | who the floor is for, as plain text |
| `whatDwellsHere` | the overview, as prose |
| `factions` | blocks, one per faction. A faction block with an `id` is also an entity: its heading is the name and its body the description |
| `wanderingMonsters` | blocks |
| `aftermath` | what changes once the floor is cleared, as prose. On its own it does not make the tab show |
| `entities` | the people, creatures and things on this floor that links, creature lists and relations can name |
| `relations` | who is tied to whom on this floor (§5.11) |

**An entity**:

| field | what it does |
|---|---|
| `id` | the link target; a slug |
| `kind` | `creature`, `npc`, `faction` or `item`. Only a creature or an npc can be in a room |
| `name` | the words a bare link shows, and the card's title |
| `statBlock` | the stat block it fights with, by name (`"gargoyle"`). It uses the block's numbers but keeps its own name and story |
| `description` | the card's text, as prose |
| `secret` | not shown |

**An area** (`areas`), shown on the Room tab:

| field | what it does |
|---|---|
| `areaNodeId` | **required**: the area's id |
| `title` | the area's name everywhere, and the source of its printed key: `"Undercroft Landing (Level 1, Area 1)"` |
| `areaNumber` | the key, when the title has none |
| `readAloud` | the boxed text for the players, as prose |
| `glance` | blocks: the short list read first |
| `detail` | blocks: the rest, collapsed |
| `skillChecks` | `{ "ability": "Wisdom (Survival)", "dc": 10, "purpose": "..." }`, shown as DC pills |
| `creatures` | who is here (below). An empty list means nobody is |
| `exits` | where the party can go from here (below) |
| `pages` | not shown |

**A creature** in an area:

| field | what it does |
|---|---|
| `ref` | who: a stat block by name, an entity, an NPC or a character, named as a link would name them |
| `count` | how many: empty for one, a number up to 99, or dice (`"1d4+1"`) the DM rolls |
| `note` | as prose, beside them in the encounter |
| `secret` | only the DM should know they are here |

The creature list is the encounter: **Add all to battle** on the room's Info tab puts exactly these
into the fight.

**An exit**: `to` (`"area 2"`, or `"L2 area 1"` for another floor), `note` (prose) and `secret`. It
becomes a jump to that area, a muted label while its floor is unwritten, or a broken label.

### 5.3 `campaign.json`

| field | what it does |
|---|---|
| `title` | shown as "Campaign — title" at the foot of the Level tab |
| `startAreaNodeId` | the area the DM screen opens on. Without it, the first area of the first level file |
| `sections` | blocks: the background, at the foot of the Level tab |
| `levelsHeading`, `levelsNote` | the level table's heading ("Levels" when blank) and a note beside it |
| `levels` | the level table, one row per floor (below) |
| `bonusXp` | blocks, after the table |
| `pages` | not shown |

The campaign part of the Level tab shows only when there are `sections` or `levels`.

**A level row**: `level` (the floor number), `name`, `characterLevel` (the "Characters" column, as
text: `"2-3"`), `authored` (tags the row "in app"; keep it true exactly for the floors with a level
file) and `requiredLevel` (not used by the app).

### 5.4 `reference.json`

`title`: the heading above the sections, in the campaign's own words ("Rules of the undercroft"); none
when blank. `sections`: blocks, shown on the Rules tab above the decks.

### 5.5 `quests*.json`

`{ "quests": [ ... ] }`, shown on the Quests tab. Every `quests*.json` is read, so a level's side
quests can live in a file of their own.

| field | what it does |
|---|---|
| `id` | **required**: quest progress is saved under it |
| `title` | the card's title |
| `availability` | `starting`, `future` or `side`: a badge. Whether a quest can be taken up depends on its prerequisites |
| `pages` | shown as "p. N" |
| `giverLabel` | the giver, as written |
| `giverNodeId` | makes the giver a button that opens their card: an NPC, a character or anything with a card |
| `hook` | how the party comes across it, as prose |
| `prerequisites` | shown with ✓ or •, never enforced (below) |
| `beats` | the story, in order, each ticked off as it happens (below) |
| `rewards`, `detail` | blocks |

**A prerequisite**: `kind` is `questComplete` (met when any quest in `anyOfQuestIds` is marked
complete by the DM) or `characterLevel` (met when the highest `level` in `party.json` reaches
`characterLevel`); `text` is what it shows.

**A beat**: `id` (ticks are saved under it), `ordinal` (the number shown, from 1), `summary` and
`detail` (prose), `secret`, and a `target`: where it happens.

**A target**: `kind` is `area`, `level`, `place` or `surface`, with `level`, `areaKey`, `placeLabel` and,
for an area, `nodeId`, the area's id. An area target whose area is written becomes a jump; the others
are labels. An area target is marked "not authored yet" until its area is written, and a level or place
target until the floor it names (`level`) has a level file. A place with no `level`, and the surface,
are never marked.

```json
"target": { "kind": "area", "level": 1, "areaKey": "4", "placeLabel": "Bell-Founder's Workshop", "nodeId": "data_dossiers_level_1_area_4" }
```

### 5.6 `npcs.json`

`{ "summary": "...", "npcs": [ ... ] }`, shown on the NPCs tab, which filters them as you type.

| field | what it does |
|---|---|
| `id` | the link target; unique |
| `name`, `role` | the card's header |
| `where`, `disposition` | the questions asked mid-session: where they are, and how they feel about the party |
| `appearance` | "Looks like", as prose |
| `armourClass`, `maxHp`, `statLine` | numbers at a glance, if they may come to blows |
| `statBlock` | the stat block they fight with. List them in a room and they join the fight with its numbers, under their own name |
| `summary` | as prose |
| `knows` | blocks: what they can tell the party, shown first |
| `detail` | blocks: worth knowing |
| `hooks` | blocks: what the DM can pull on |
| `contacts` | their people, as cast rows (below) |

**A cast row** (an NPC's contacts, a story's cast): `name`, `role`, `state` (where they stand now),
`secret`, `ref` (makes the name a button that opens that card) and `rel`. On a contact, `ref` and `rel`
together are a relation from this NPC to that person (§5.11).

### 5.7 `characters.json`

`{ "summary": "...", "characters": [ ... ], "notes": [ ... ], "openQuestions": [ ... ] }`, shown on
the Party tab. `notes` and `openQuestions` are blocks: what the party does to encounters, and what to
settle before the next session.

| field | what it does |
|---|---|
| `id` | the link target; unique |
| `name`, `playerName`, `build` | the card's header |
| `level` | the character's level, 1 to 20; not shown |
| `armourClass`, `armourNote`, `maxHp`, `passivePerception`, `initiativeModifier`, `speed`, `senses`, `saveDc`, `defences` | the vitals, shown without opening the card |
| `saves`, `skills`, `languages`, `attacks`, `kit` | the rest of the sheet |
| `dmOnly` | always visible, marked as the DM's |
| `abilities` | blocks: what changes the DM's encounters |
| `backstory` | as prose |
| `hooks`, `checks` | blocks: hooks to pull, and what to ask the player |

### 5.8 `party.json` and `allies.json`

The battle rosters, `{ "members": [ ... ] }`, edited in the app.

- **A party member**: `name`, `playerName`, `class`, `level`, `armourClass`, `maxHp`,
  `passivePerception`, `initiativeModifier`. Starting a fight seeds one row per member. The highest
  `level` decides the `characterLevel` quest prerequisites.
- **An ally**: `name`, `owner`, `statBlockNodeId` (the exact `nodeId` of its stat block),
  `armourClass`, `maxHp`, `passivePerception`, `initiativeModifier`. Allies are added to a fight by
  hand.

### 5.9 `sessions.json`

`{ "summary": "...", "sessions": [ ... ] }`, shown on the Session tab, with a picker when there are
two or more.

- **A session**: `id`, `title`, `source`, `summary` (prose), `canon` (blocks: what is settled),
  `scenes`, and `decisions` (blocks: what is still to decide).
- **A scene**: `id`, `ordinal` (from 0), `title`, `when`, `cast` (who is on), `purpose` (prose) and
  `beats` (blocks).

### 5.10 `story.json`

`{ "chapters": [ ... ] }`, shown on the Story tab: what happened before the campaign. **A chapter**:
`title`, `source`, `summary` (prose), `cast` (cast rows, §5.6), `sections` (blocks) and `looseEnds`
(blocks).

### 5.11 Relations: `relations.json` and a level's `relations`

A relation says how two things stand: `{ "a": "grask", "rel": "leads", "b": "grasks-crew", "note":
"...", "secret": false }`. `a` and `b` are named as a link would name them, but may not be a stat block
or a spell, and may not be the same. Each relation shows on the cards of both ends, and beside each
occupant in a room's "Involved with".

A level's relations are read on that floor; `relations.json` holds the ones that belong to none.

`rel` is one of sixteen kinds, written exactly as here:

| kind | from `a` | from `b` |
|---|---|---|
| `ally`, `rival`, `enemy`, `family` | ally of, rival of... | the same: these read both ways |
| `leads` | leads | led by |
| `member-of` | member of | has as a member |
| `serves` | serves | served by |
| `controls` | controls | controlled by |
| `fears` | fears | feared by |
| `hunts` | hunts | hunted by |
| `plots-against` | plots against | plotted against by |
| `owes` | owes | owed by |
| `protects` | protects | protected by |
| `loves` | loves | loved by |
| `runs` | runs | run by |
| `located-in` | located in | home to |

A seventeenth, `related`, is for the book index's own links only (§5.13).

### 5.12 `deck-*.json`: card decks

One deck per file, shown on the Rules tab with a Draw button.

| field | what it does |
|---|---|
| `id` | **required**: drawn cards are saved under it. A deck without one is skipped |
| `title` | the heading |
| `cardNoun`, `cardNounPlural` | what a card is called ("rune"; "card" when blank). The plural defaults to the noun plus "s" |
| `drawRule` | `fresh`: any card, every time; `kept`: a drawn card is handed out and not drawn again until the deck is reset |
| `drawHint` | the Draw button's tooltip |
| `usage` | when to draw, shown under the heading |
| `cards` | the cards (below) |
| `pages` | not shown |

**A card**: `id`, `name`, `subtitle`, `body` (prose) and `effects` (blocks).

### 5.13 `book-index/<book>.json`: what the books say

A campaign often runs out of published books. The book index lets search and links reach what those
books mention without copying them: a name, a line of description and a page.

| field | what it does |
|---|---|
| `book` | a short id, shown upper-cased in search results ("SRD, p. 86") |
| `title` | the book's title, for citations ("SRD 5.1, p. 86") |
| `source` | the file a stat block or spell cites as its `source`, so its citation can use the `title` |
| `isAdventure` | read first, so this book wins an id two books share |
| `entries` | the entries: `id` (a slug), `name`, `kind` (free text, not shown), `description` and `page` |
| `links` | relations between entries or the campaign's own things, each with `rel: "related"` and the book's own verb as its `note` |

An entry whose id is also something the campaign wrote does not become a link target of its own:
its text is shown under the campaign's card, as "In the book".

### 5.14 `monsters*.json` and `spells*.json`

Each is a JSON array. The sample pack ships the whole SRD 5.1 in `monsters-srd.json` and
`spells-srd.json`; put your own in a `monsters.json` and `spells.json` beside them.

A link names a stat block or spell by its **name**; everything else (allies, pictures, spellcasting,
saved fights) by its `nodeId`.

**A stat block**: `nodeId`, `name`, `size`, `creatureType`, `alignment`, `armourClass`, `armourNote`,
`averageHitPoints`, `hitDice`, `speeds` (`{ kind, feet, note }`), `abilities` (`{ str, dex, con,
intelligence, wis, cha }`: note `intelligence` in full), `savingThrows` (`{ ability, bonus }`), `skills`,
the damage and condition lists, `senses`, `passivePerception`, `languages`, `challengeRating` (as text:
`"1/4"`), `xp`, `proficiencyBonus`, `traits`, `actions`, `bonusActions`, `reactions`,
`legendaryActions` (each `{ name, text, attacks }`), `legendaryActionsPerRound`, `spellcasting`,
`source` and `sourceLocation`.

**A spell**: `nodeId`, `name`, `level` (0 for a cantrip), `school`, `castingTime`, `range`,
`components`, `duration`, `concentration`, `ritual`, `text`, `higherLevels`, `source` and
`sourceLocation`.

The shapes are easiest to copy from the SRD files. A number written where text is expected
(`"challengeRating": 2`) reads as text, as in the dossiers; text where a number is expected
(`"armourClass": "high"`) loses the whole file.

### 5.15 `art/catalogue.json`: pictures for the projector

`{ "images": [ ... ] }`, listing pictures stored under `data/art/`.

| field | what it does |
|---|---|
| `id` | **required**: the picture on show is saved under it |
| `file` | **required**: its path under `data/art/`, with forward slashes and the file's exact case. PNG, JPEG or WebP |
| `title`, `subject`, `tags` | what the picker shows and searches |
| `kind` | `creature`, `npc`, `location`, `item`, `handout` or `diagram`; not shown |
| `nodeIds` | the stat blocks it shows, by `nodeId`, so the Art tab can suggest it during a fight |
| `width`, `height` | the picture's size in pixels, which frames it on the projector |
| `source`, `sourceLocation` | not shown |

### 5.16 Maps

**The `.ds` file** is a map saved by [Dungeon Scrawl](https://www.dungeonscrawl.com): its floors,
walls, doors, stairs and sprites. Its file name is the map's id, and the folder it is in is its
adventure. When there are several maps, the first in ordinal order of their ids is the one that opens.

**The `.regions.json` file** is what the Room Editor adds; you rarely need to edit it by hand.

| field | what it does |
|---|---|
| `mapId`, `adventure` | the `.ds` file's name and folder |
| `levelName` | the name the map pickers show; the map id when blank |
| `regions` | the rooms, as outlines on the map (below) |
| `features` | markers: `{ featureId, position, kind, label }`, where `kind` is `trap`, `hiddenDoor`, `secret` or `hazard`. The players see one only once the DM reveals it |
| `objects` | what the editor says about the map's doors, stairs and sprites (below) |

**A region**: `regionId`, `label`, `polygon` (at least three `{ x, y }` corners in the map's own
units) and `graphNodeId`, the area it is. Clicking the region opens that area, and several regions can
share one. A region may name an area on a floor that has no level file yet, by the id it will have.
The Room Editor links a region to areas only; one linked by hand to anything else (a person, a stat
block) opens that one's card instead, and the editor tags it "not an area".

**An object**: `objectId` (the Dungeon Scrawl node it describes), `kind`, `label`, and what it hides:
`isSecret` (a door drawn as wall until found), `isConcealed` (stairs or a sprite hidden until revealed),
`defaultOpen` (the door starts open) and `doubleDoors`.

## 6. Citations

A stat block or spell cites its `source` and `sourceLocation` (`"SRD_CC_v5.1.pdf"`, `"p.266"`). Where
a book index names that file as its `source`, the citation uses the book's `title` instead: "SRD 5.1
p.266", on its card, in the encounter strip and under the full block or spell.

An entry of a stat block (a trait, an action) is shown as its `text`, as the book prints it. Its parsed
`attacks` feed the Battle tab's quick attack lines, and stand in for the text only when an entry has
none.

## 7. Starting your own campaign

Copy the sample pack, keep its two `*-srd.json` files and `book-index/srd.json` (which names the book
they come from, for their citations), and replace the rest one document at a time; the app shows
whatever is there. Write a map in Dungeon Scrawl, save it as `Maps/<adventure>/<map>.ds`, and mark its
rooms in the Room Editor (`/editor`). Check the result with the content tests as you go. The
[guide](guide.md#2-writing-your-own-campaign) walks through it from an empty folder to a first room.

## 8. Content that is not yours

A campaign is usually built from published books. Keep that content in a private repository or folder
of your own, and serve it only behind the app's passphrase: the maps, stat blocks, read-aloud text and
art of a published adventure are its publisher's copyright.

## 9. The content tests

`DungeonTable.ContentTests` checks a content root the way the app reads it, and reports what the app
would skip or cannot use, file by file:

```bash
DUNGEONTABLE_CONTENT_ROOT=/path/to/your/campaign dotnet test DungeonTable.ContentTests
```

Run it from the folder that holds `DungeonTable.slnx`. In PowerShell, set the variable first:
`$env:DUNGEONTABLE_CONTENT_ROOT = "C:\path\to\your\campaign"`. Without the variable, it checks the
sample pack. A rule with nothing to check (a campaign with no NPCs) is skipped, not failed, but a root
with no level file holding an area fails outright, so a mistyped path cannot pass by checking nothing.

What it checks:

- **Everything loads.** Every document parses as the app reads it; every `.json` among them is one the
  app reads (`npc.json` or `Level-2.json` is not); no list holds a `null`; the folders the app needs
  are there.
- **Nothing is dropped.** Every area, level, quest, deck, card, beat, scene, stat block, spell and
  picture has an id, and no id is written twice.
- **Every reference resolves.** Every link, creature, exit, relation end, contact, quest giver,
  prerequisite, quest destination, map region and start area leads somewhere, or waits for a floor
  that is not written yet; no link target means two things; links sit only in prose fields.
- **Every card has what it shows.** NPCs, characters, sessions and scenes, story chapters, quests and
  beats, decks and cards, level rows and blocks have the fields their cards display; beats and scenes
  are numbered in order.
- **The numbers add up.** Monsters have a legal AC, hit points, ability scores and challenge rating,
  with the proficiency bonus it gives; every spell a monster casts is in the library; every `source` is
  a book the book index titles.
- **The pictures are right.** Each exists at its path, with the case it is written in, and has the size
  the catalogue says; every picture on disk is catalogued.
- **The maps can be used.** Each `.regions.json` sits beside its `.ds`; every region has an id and an
  outline; no region covers another's middle; every area on a mapped floor has a region to click.
- **The people agree.** `party.json` matches `characters.json`.
