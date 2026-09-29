# How DungeonTable is put together

_To understand this, read [the README](../README.md) first, then this, then
[content-format.md](content-format.md) for the documents the app reads. The code comments carry the
detail; this page carries the shape and the reasons._

## Layers

```
DungeonTable.Core            the domain: maps, dossiers, stat blocks, the battle, the saved table
      ↑
DungeonTable.Application     the ports: IDossierStore, IStatLibrary, IMapStore, ISessionStore, ...
      ↑
DungeonTable.Infrastructure  the adapters: FileSystem* stores, links, Marten* persistence
      ↑
DungeonTable.Web             Blazor Server: the DM screen, the player view, the Room Editor
```

References only point up this list. `Core` has no dependencies and does no logging. `Application`
holds one interface per port and no infrastructure types. `Infrastructure` names each adapter after
the technology behind it (`FileSystemDossierStore`, `MartenSessionStore`). `Web` is the only
composition root: every service is registered in `Program.cs`, as a singleton or scoped, never
transient.

## Content

### Finding it

The app reads six kinds of content: maps, dossiers, stat blocks, art, the roster seeds and the book
index. Each has a locator (`MapFileLocator`, `DossierFileLocator`, ...) that tries, in order:

1. its own setting (`Maps:Root`, `Dossiers:Root`, ...);
2. its usual place under `Content:Root` (`Maps/`, `data/dossiers/`, ...);
3. walking up from ASP.NET's content root, looking for that usual place in each parent folder.

The walk-up is how the container finds content copied or mounted at `/app/Maps` and `/app/data`. It
is also a trap: a checkout nested inside a folder that holds content will quietly find it. Tests
therefore start their hosts from an empty folder (`SamplePack.Serve`), so content the sample pack
lacks stops the host instead of being found somewhere above.

Every kind is required except the book index, and a missing one stops the app at startup with a
message naming `Content:Root` and the kind's own setting. A missing content root is a
misconfiguration, so it is an exception, not a `Result`.

### Reading it

The content stores load their documents into immutable objects. They are deliberately **fail-soft**: a
document that does not parse is skipped, a `null` list reads as empty, a `null` entry in a list is
dropped, and an entry with no id is dropped, so a hand-edited typo leaves a tab short rather than
taking the table down in the middle of a session.

**A `null` reads as if the field were left out**, in every document the app reads, maps and rosters
included. People and LLMs both write `null` for "nothing here", and every Core type starts its fields
at an empty value the app relies on (nothing checks a title for `null`), so an explicit `null` used to
reach the page and fail it. It is handled once, where the JSON enters, rather than at each of the many
places a field is read:

- `NullMeansLeftOut`, a contract modifier, makes the setter of every text or object field decline a
  `null`, so the field keeps its start value;
- `NullTolerantValueConverter` reads a `null` number, flag or enum as its default (`0`, `false`,
  `None`), which is what those fields hold when left out;
- `LenientListConverter` reads a `null` list as empty and drops a `null` entry before the entry's own
  converter sees it, so a `null` in a list of ids is dropped rather than read as an empty id;
- `LenientStringConverter` reads a number or a true/false written where text belongs as that text.

None of these changes what the app writes: the Room Editor, the rosters and the uploads catalogue
write the same bytes as before, which a test pins.

What was skipped is not hidden: **each store records what it skipped or dropped** (`Problems`: the file
and why), and `ContentReport` writes it to the log as the app starts, before the first request, with a
line saying what the content amounts to. The content tests (below) report all of that and much more,
but they need the .NET SDK; the log does not.

### Reading it again: live reload

The dossiers, the stat library and the book index are read into one **`ContentSnapshot`**, together
with everything built from them: the link index, the projection behind briefings, cards and search, and
the prose link resolver with its cache. `LiveContent` holds the current snapshot. The stores the pages
inject (`IDossierStore`, `IStatLibrary`, `IBookIndexStore`, `IContentProjection`, `ICrossRefResolver`)
are thin facades that hand every call on to it, so no page holds a store it would have to swap.

`ContentWatcher` watches the content folders. When a file changes it waits a moment for the folder to
settle (an editor saves in more than one write, a search-and-replace touches many files), then:

- **a document**: builds a whole new snapshot and swaps it in with one reference write, so a page in the
  middle of a render sees the old content or the new, never a mixture;
- **a map's `.ds` or `.regions.json`**: tells the pages, which read the map again;
- **the committed art catalogue**: reads it again, keeping the uploads.

Then `LiveContent.Changed` fires. Each page that keeps content marshals onto its own circuit and reads
again: `DmWorkspace.RefreshAsync` re-reads the campaign's documents and shows the same history stop from
the new content, and reloads the loaded map in place, without pointing the shared session anywhere, so
reveals, framing and open doors stay; the player view reloads its map; the combatant pane its block; the
Room Editor forgets what it cached about ids but keeps the map it is editing.

**A reload never takes away what the table could read.** If a document that parsed before cannot be read
now, the new snapshot is not taken: the table keeps the content it had, and the log says which file and
why. A document that was already unreadable does not hold a reload up. The art catalogue follows the
same rule.

Watching goes through `PhysicalFileProvider`, which polls instead of listening when
`DOTNET_USE_POLLING_FILE_WATCHER` is set: a folder bind-mounted into a container from a Windows or
macOS host sends no file events, and the compose files set it. `Content:Watch=false` turns watching off.
A reload runs on a pool thread as a task, so a failure faults the task (and is logged) instead of
escaping the timer's callback and ending the process.

A running fight keeps the numbers it started with; `BattleState` copies them when a combatant is added.

### Writing it

Three things write into the content at run time:

- the **Room Editor** saves a map's `*.regions.json` (`FileSystemMapStore`);
- **art uploads** go to `data/art/uploads/`, with a catalogue of their own beside the committed one;
- the **party and ally rosters** are written back to `data/party.json` and `data/allies.json`, but only
  when there is no database. With one, they live in PostgreSQL, seeded once from those files.

All three are written to a temporary file beside the target and then moved into place, so a crash
mid-save never leaves one half-written: the last save stays whole until the new one is complete.

## Links, cards and search

Prose in the dossiers links with authored markup: `[[area 3a]]`, `[[grask|the bugbear]]`. The rule is
that **only what an author marked is a link**. Nothing is guessed from the text around it: a mention
is not a reference, and a guessed link ("disguised as vampires" opening the vampire's stat block) is
worse than none, because nothing flags it.

- `LinkTargets` indexes everything a link can name, once: areas by the key printed on the map, per
  floor; people by id; the entities each level declares; stat blocks and spells by the slug of their
  name; and, below all of those, the book index. The campaign's own content always wins over the book
  index. A name that could mean two things resolves to neither.
- `MarkupCrossRefResolver` turns a text into spans. A link that resolves becomes a chip that opens its
  card; one that resolves to nothing keeps its words and is shown as broken; one to a floor with no
  level file yet is **pending**, and becomes a live link by itself the day that floor is written.
- `Backlinks` answers "where does this appear?": the rooms whose creature list names something, and
  the rooms whose prose links to it. Only authored references count, for the same reason.
- `RelationIndex` holds every relation (who serves whom, who is at odds with whom) from both ends, so
  a card can show the lines that involve it.
- `AuthoredProjection` builds what the DM screen shows from all of this: a room's briefing from its
  own creatures and exits, a card for anything a link can name, and search.

An area's creature list is the encounter. The Battle tab's encounter strip is built from it, with the
authored counts, and never from names in the prose: "an empty list means nobody is there".

## The live table

Blazor Server keeps one circuit per browser tab. The state of the table is shared between them:

| service | lifetime | holds |
|---|---|---|
| `SessionState` | singleton | what the projector shows: the map, revealed regions and features, fog cells, open doors, concealed objects, the framing, the picture over the map |
| `BattleState` | singleton | the one running fight |
| `CampaignState` | singleton | quest progress, each deck's drawn cards, per-area checklists and notes |
| `DmWorkspace` | scoped | one DM tab's own view: the loaded map, the room shown, the reference history |

The app assumes **one live table**. The shared state is a singleton so that a refreshed DM tab, a
second DM window and the player view all agree, and each service raises `Changed` so the others
re-render. The DM screen's four tabs (Map, Info, Battle, Art) stay mounted and are hidden with CSS, so
switching never loses the map's pan and zoom or the navigation history.

## Maps and what the players see

A map is drawn in Dungeon Scrawl and saved as a `.ds` file. `DungeonScrawlMapReader` reads its
geometry (floors, walls, doors, stairs, sprites) into a `VectorMap`, and `VectorMapSvgRenderer` draws
it as SVG for both views. The Room Editor adds what Dungeon Scrawl cannot say, in a `*.regions.json`
beside it: which area each room is, which doors are secret or open, and where the traps, hazards and
features are. That file describes hidden things, so it is never served to a browser.

The DM reveals the map as the party explores: a room at a time, a fog brush, a "lifter" that uncovers
what someone standing at a point could see (`VisionReveal`), and corridor stubs that fade out a step or
two past an open door (`CorridorReveal`). The player view draws the same map under an opaque fog built
from the revealed geometry only, and leaves out what the DM has concealed.

Leaking hidden detail into the player view's DOM is not treated as a security problem: past the
passphrase, only the people at the table have the URL, and the player view is a projector. What the
projector actually shows must be right, and the tests check that.

## Persistence

Everything the table cannot lose is one document per table, a `TableSnapshot`, stored with Marten in
PostgreSQL: the reveals, the fight and the campaign's progress. It is a purpose-built document, not the
live services' own state: some things must not come back after a restart (the one-step undo of a
cleared map), and some must (the fight's id counters, or restored combatants would reuse ids).

`TablePersistence` restores the document in the host's `Starting` phase, before Kestrel accepts a
request, so no circuit can start working on state that is about to be replaced. After that it writes on
a two-second debounce: painting fog changes the state per cell, and writing per change would be
pointless. A failed write is retried on the next tick.

`TableSnapshot.CurrentVersion` is bumped only when an older build **cannot read** the new shape.
Adding a field with an empty default is not such a change: the older build ignores it and restores
everything else. A document written by a newer version is neither restored nor overwritten, and the DM
screen says so.

Without a connection string the app uses `EphemeralSessionStore` and says "Not saved" on the DM
screen all session. Finding out after a restart that nothing was being saved is the one surprise worth
being loud about.

## The Battle tab

The fight is run from across the table, mostly while looking at the players, and that decides the
design:

- **A row is read at a glance**: name, hit points, state, AC, passive Perception, a damage box.
  Everything that is typed rather than glanced at hides behind the row's "..." toggle.
- **Nothing demands a roll.** A room's creature count ("4", or a dice expression such as "1d4+1") is
  a pre-fill the DM can change.
- **The list never re-sorts under the cursor.** Enter and Tab walk the initiative column, and the order
  is sorted only once the last blank is filled.
- **The detail pane stands on its own.** Damage, healing and conditions are repeated there, beside the
  stat block and the room's own notes on that creature, so the DM never has to go back to the Info tab
  mid-round.
- **There is one fight.** `BattleState` replaces a running fight without asking; asking is the UI's
  job, because it knows how to word the question for where it came from.
- **The rosters are never hand-edited**: the party and its allies have editors in the app.

## Art on the projector

The DM can lay a picture over the map on the projector: from the campaign's catalogue, or uploaded
during play. An upload is never stored as it arrived. It is identified by its magic bytes, its header
is checked for its pixel count before anything is decoded (a small file can declare an enormous image),
and it is decoded, scaled down and written back out as WebP, which also drops its metadata. The
encoding runs outside the catalogue's lock, so an upload never stalls the projector.

The `/art` route serves raster images only. An SVG served from the app's own origin would be markup
that can run script.

## Security

`PassphraseAuthMiddleware` puts the whole app behind one shared passphrase with HTTP Basic auth. It
runs after `UsePathBase` and before the static files, the `/maps` and `/art` routes and the Blazor
app, so everything is behind it; only `/healthz` is exempt. The passphrase is required at startup, and
blank counts as missing: an empty slot in `appsettings.json` must not turn into an open gate.

## Tests

- **`DungeonTable.Tests`** runs without a database and on no content but the sample pack. Most tests
  are plain unit tests; components are rendered with `HtmlRenderer`; the host tests start the real
  app through `WebApplicationFactory`, served from the sample pack alone. The PostgreSQL tests run when
  `DUNGEONTABLE_TEST_POSTGRES` holds a connection string, and skip otherwise.
- **`DungeonTable.ContentTests`** checks a content root: the sample pack, or whatever
  `DUNGEONTABLE_CONTENT_ROOT` names. It loads the content through the app's own locators and stores,
  and every rule runs as a theory over the documents of its kind, so a failure names the file. The rules
  have self-tests of their own in `RuleTests/`, each feeding a rule content built to break it: content
  that passes cannot show that a rule works.
