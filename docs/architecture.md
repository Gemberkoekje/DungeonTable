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
  own creatures and exits, a card for anything a link can name, and search: by name, and by words
  through its `PassageIndex` (see [Answers that come to you](#answers-that-come-to-you)).

An area's creature list is the encounter. The Battle tab's encounter strip is built from it, with the
authored counts, and never from names in the prose: "an empty list means nobody is there".

## The live table

Blazor Server keeps one circuit per browser tab. The state of the table is shared between them:

| service | lifetime | holds |
|---|---|---|
| `SessionState` | singleton | the map the DM is on and the map on the projector (the same, until the DM moves on); revealed regions and features, fog cells, open doors, concealed objects, the framing, the picture over the map; the party token and the undo of what it revealed; and the same per map the table has left |
| `BattleState` | singleton | the one running fight |
| `CampaignState` | singleton | quest progress, each deck's drawn cards, per-area checklists and notes, the pins along the foot of the dock |
| `DmWorkspace` | scoped | one DM tab's own view: the loaded map, the room shown, the reference history, the passage open over the dock, whether the dock is pinned |
| `PartyMoves` | scoped | works out what the party sees as one DM tab moves the token, from that tab's map |

The app assumes **one live table**. The shared state is a singleton so that a refreshed DM tab, a
second DM window and the player view all agree, and each service raises `Changed` so the others
re-render.

## The DM screen: one map, one dock

The DM screen is the map, which never leaves the screen, and beside it a **dock** with four tabs: Room,
Battle, Campaign and Art. It used to be four whole-screen tabs, Map and Info among them, and reading a
room meant leaving the map: pick the inspect tool, click the room, switch to Info, read, switch back to
Map, pick the lifter again. Five actions to answer "what do I say", and the projector's frame and the
reveals worked on the Map tab only. Now the bar (what the players see, the frame, the map picker, the
save status), the map and the dock are all on screen at once, and reveals, the frame and the doors work
whichever dock tab is up.

- **A click looks.** The map has a default mode, Look, and goes back to it on Esc. A click on a room
  shows it on the Room tab and changes nothing for the players; a click on a door opens or closes it.
  A secret door the players have not found gets no click target in Look, so the click reads the room
  behind it: discovering one takes the Doors tool, so it never happens by accident (`MapHitTargets`).
- **Revealing is moving the party.** The DM drags the party token and what the party sees on the way
  is revealed (see [The party token](#the-party-token)); the Room tab follows the token.
- **The tools are for the occasional job.** The ones that do one thing to one object (reveal, doors,
  conceal, point, put the party down) go back to Look after one click that did something; the fog brush
  and measuring stay until Esc; Shift keeps a one-click tool up (`ToolSelection`). The trap this removes is
  being left in the wrong mode. The toolbar and its options are drawn over the map, not beside it, so
  the canvas keeps its size whatever tool is up.
- **The Room tab reads in table order**: the read-aloud first, then the checks and the short list, the
  encounter, the rooms around it (Nearby), the detail, the DM's note, and, folded at the foot, who is
  here (`RoomReading`). It started with the encounter and the note and reached the read-aloud only
  after nine sub-tabs.
- **The map says what is in each room**, with badges, a peek card and the Room tab's Nearby list (see
  [What is in each room, on the map](#what-is-in-each-room-on-the-map)).
- **Cards are a sheet over the dock** (`CardSheet`), with the history's back, forward and breadcrumb,
  so Esc puts the card away and the room is still there, scrolled where it was. The sheet covers the
  Room and Campaign tabs only: the fight has its own detail pane.
- **Answers come to the DM**: Find anything over the map, pins along the foot of the dock, and the
  floor's facts behind the Room tab's ⓘ (see [Answers that come to you](#answers-that-come-to-you)).
- **Every dock tab stays mounted**, stacked and hidden with CSS, so a tab keeps its scroll position,
  half-typed boxes and the fight's open pane. The map pane is mounted once and never hidden, because
  the canvas keeps its pan and zoom in JS.
- **The fight has a layout of its own** (`DockLayout`). In a fight the DM keeps the map still and
  reads stat blocks all the time, so the Battle tab gives the dock about 72 % of the width and the
  reading tabs about 40 %. The fight's detail pane sits beside the order only when the dock has about
  68rem: a row stays two lines only while the order has about 36rem, and whatever is left goes to
  the pane, split evenly, because the pane is what the DM reads. (The redesign's plan assumed both
  would fit in the reading dock; measured, each row wrapped to five lines.)
- **Each layout keeps its own dock width and its own view of the map.** The DM drags the divider, and
  `dt-dock.js` keeps a width per layout in local storage (guarded, since that can be refused), following
  the shell's `layout-battle` class; it is not table state. `\` swaps the dock between the two default
  widths. `MapViewMemory` decides what the map does on a change of layout: keep the view of the layout
  being left, then bring back the other's, except that the first time the DM opens a fight the map
  frames the fight's room and a few cells around it (`DmWorkspace.AreaSurroundings`), a bird's-eye
  reminder of what is around the fight. The pan and zoom are a transform on the canvas's wrapper, which
  does not depend on its size, so a view kept at one width comes back exactly at that width. The move
  happens after the render that changed the layout, so the map is measured at its new size.
- **Below 1200 px wide the dock is a drawer** over the map, toggled with Tab: a fallback, since the
  table runs on a 1920×1080 laptop.

## Maps and what the players see

A map is drawn in Dungeon Scrawl and saved as a `.ds` file. `DungeonScrawlMapReader` reads its
geometry (floors, walls, doors, stairs, sprites) into a `VectorMap`, and `VectorMapSvgRenderer` draws
it as SVG for both views. The Room Editor adds what Dungeon Scrawl cannot say, in a `*.regions.json`
beside it: which area each room is, which doors are secret or open, and where the traps, hazards and
features are. That file describes hidden things, so it is never served to a browser.

The DM reveals the map as the party explores: by moving the party token, which uncovers what the party
could see from every square it passes (`VisionReveal`), a room at a time, with a fog brush, and corridor
stubs that fade out a step or two past an open door (`CorridorReveal`). The player view draws the same map under an opaque fog built
from the revealed geometry only, and leaves out what the DM has concealed.

Reveals belong to a map: each has its own world coordinates, regions and doors. When the table moves to
another map, `SessionState` sets the reveals, painted fog, open doors and framing aside under the map
they were on, and brings back whatever was left on the new one, so a party that goes down a floor and
back up finds the first floor as they left it. A map brought back counts as already seeded: its
default-open doors and starting framing are not applied again over what the DM changed. A map left
before it was ever set up, with nothing on it, is not kept, so it still gets them on its first real
visit. The maps set aside are saved with the rest, in `RevealSnapshot.OtherMaps`.

**The projector does not follow the DM to another map.** `SessionState` keeps two map ids: the map the
DM is on (`CurrentMapId`, which the reveals, the framing and the party token belong to) and the map on
the projector (`ProjectorMapId`). Moving to another map changes only the first, so the players go on
seeing the floor they are on while the DM looks ahead or sets up the next one, and the projector's map
is drawn from what was set aside for it in `OtherMaps`. The DM's **Show on projector**
(`ShowCurrentMapOnProjector`) makes them the same again; so does going back to the projector's map.
The player view draws `SessionState.Projector()`, one `ProjectorView` taken under the lock, never the
current map's sets directly. The very first map the table is on goes straight to the projector, since
there is nothing else to show. A point the DM makes is drawn on the projector only while it shows the
DM's map, and is dropped when the map is shown, because the players never saw it. A document saved
before `ProjectorMapId` existed has it blank, and is read as the map in play, which is where the
projector always was then.

Leaking hidden detail into the player view's DOM is not treated as a security problem: past the
passphrase, only the people at the table have the URL, and the player view is a projector. What the
projector actually shows must be right, and the tests check that.

## The fog lifter

The lifter was a tool: click where the party stands and see what they see. The party token replaced it
(next section), and its rule is what the token runs from each square it passes. The lifter reveals what
someone standing at a point could see within a range: every floor tile with a
straight line to one of its floor sample points (the centre, then a 7×7 lattice at eighths of the tile,
so a sliver of floor beside a slanted wall still counts) that crosses no wall and no still-hidden secret
door. Wall segments are extended half a cell past every corner, so a line cannot slip through the point
where two walls meet. Done plainly, that is each sample point tested against every floor outline on the
map and each sight line against every wall in reach: on a large level a 120 ft lift took seconds.

What never changes about a map is therefore worked out once per map, in a `VisionIndex` kept per
`VectorMap` instance (a map read again from disk gets a new one):

- which sample points are floor, a grid row at a time: the crossings of the floor's edges with each
  row's lines of sample points are found once and sorted, and a point is floor when an odd number of
  them lie to its right. That is `PointInRings`, with the same arithmetic, so the answer is the same to
  the bit;
- the walls, with how many meet at each end (for the corners) and which cells each one passes.

Each lift then files the walls within its reach in a grid of buckets (`SightLines`), and a sight line
tests only the walls in the buckets it passes, nearest first. The projector's corridor peek uses the
same index for its floor test and its walls.

**The rule did not change, only the bookkeeping.** The old algorithms are kept in the tests
(`VisionRevealReference`, `CorridorRevealReference`), and the fast ones must agree with them tile for
tile on the sample pack, from spots all over it, with doors shut, open and secret. If a lift ever has to
be faster still, the next step is a visibility polygon from the viewer: one sweep, then a cheap test per
tile. That changes the rule slightly, which is why it was not the first step.

`LiftBenchmark` keeps "fast enough" a number. It is an explicit test the build never runs
(`dotnet test DungeonTable.Tests -c Release --filter "FullyQualifiedName~LiftBenchmark" -- xUnit.Explicit=only`),
on the sample pack's map or any map named in `DUNGEONTABLE_BENCH_MAP`. On a 138×177-cell level a
120 ft lift takes 10 to 25 ms in a Release build, which is what the container runs.

## The party token

The lifter was the reveal tool the table actually used, and it was a mode: pick it, click, pick
something else to read the room, pick it again. So revealing became moving the party. A token on the DM's
map stands where the party is, and dragging it reveals what the party sees as it goes; opening a door
looks again from it; the Room tab shows the room it is in.

- **The token is the table's.** It is on `SessionState` with the reveals, per map, with its vision range,
  so a second DM window and a restart agree on where the party is, and the range no longer goes back to
  30 ft on every reload. It is saved as `PartyTokenSnapshot`, an additive field with an empty default,
  so `TableSnapshot.CurrentVersion` did not change. It is DM-only: an HTML overlay on the DM's canvas
  (`dt-party.js`), placed from world units like the player-view box, never part of the projector's SVG.
  It and its ring are moved by a transform, each on a compositing layer of its own. Placed by `left` and
  `top`, they were painted in the map's layer, so each move repainted the map under them, and at 120 ft
  the ring covers much of the map. On a 138×177-cell level, a quick 120 ft drag across about 80 squares
  stalled the DM screen's main thread for 2.0 s over ground already revealed and 3.7 s over new ground;
  on their own layers, 0.3 s and 1.8 s. What is left over new ground is the fog's own redraw, once per
  square that reveals anything: at 30 ft that is nearly every square (4.2 s before, 3.6 s after). If it
  shows at the table, the next step is fewer fog updates on the DM's map while a drag is under way: the
  server fills in the walk anyway, so nothing would be skipped, only shown later.
- **A drag reports itself a square at a time.** The browser reports once per square the pointer enters,
  plus where the drag ends, one report at a time: while one is on its way, the latest position waits.
  A quick drag therefore jumps several squares between two reports, so the server fills the walk in
  square by square (`PartyVision.Path`, a quarter square at a time) and lifts from each, and a side
  passage walked past is seen. A square with no floor in it is not stood in: the DM drags across rock
  as often as along a corridor, and a viewer in a wall would see into both rooms it divides. The squares
  one drag has looked from are not looked from again (`PartyMoves`). The token is snapped to the middle
  of its square, which is where the lifter has always stood the viewer.
- **The Party tool puts the token down without a walk**, for the stairs or a teleport, where dragging
  across the map would reveal everything between. It took the lifter's key, so the numbers did not move.
- **Ctrl+Z takes back a move.** Each move, opened door or longer range that showed something keeps what
  it showed that was not showing already, where the token stood before, and the door as it stood
  (`PartyReveal`). Undo hides those cells, puts the token back and the door too, so a door opened by
  mistake is one key away from shut and dark. A drag is one move: its first report starts the move and
  the rest join it. The list is fifty deep and live only, like the clear's undo, and goes on a map
  switch, a clear or a restore, since its cells and positions belong to one map and one evening.
- **The dock follows the party** (`DmWorkspace.FollowParty`). The shell calls it on every change of the
  shared state, on the DM circuit's own dispatcher, because the token is moved by this window, another
  one, or an undo. When the token has walked into another area, the Room tab shows it. Looking at
  another room lasts until the party moves on, which is when the DM needs the new room; the pin is how a
  room stays, and it belongs to one DM window, not the table.
- **The projector can follow the party.** A setting of the table, off by default. While it is on, a move
  that leaves the token outside the middle half of the projector's view centres the view on it again,
  at the same size, kept on the map (`MapFraming.KeepInMiddle`). Re-framing in steps keeps the picture
  still while the party walks about a room, which is what the players are looking at.

The projector's circuit renders again on every change of the session, a move of the token included,
though nothing it draws has changed: about 10 ms on a large level, and Blazor sends nothing. If it ever
shows during a drag, key the projector's layers as `MapLayerKey` keys the DM's.

## What is in each room, on the map

To see which rooms held monsters, loot or a trap, the DM clicked each one, and to look ahead, each room
next to the party's. Now the map says it, and the Room tab lists the rooms around the one on show.

- **Badges beside each room's label**, on the DM's map only (the projector draws no overlay): the
  creatures not yet ticked off, cleared (the room greyed too), loot, a trap or hazard, a secret way, the
  DM's note. A count rolled at the table is kept as written, so Nib and 1d4+2 goblins are `☠ 1d4+3`:
  plain counts add up, dice of one size add up, and the constants go into one number (`CreatureTally`).
  `c` switches the badges off for a clean map; it is one DM window's choice.
- **Nothing new is authored.** A room's `RoomGlance` is read from what the content already holds (its
  creature list, through `EncounterSource` as the encounter strip reads it, its blocks tagged loot, its
  checks and exits) and from what the Room Editor marked on the map (`RoomMarks`: trap and hazard markers
  standing in its regions or linked to it, secret doors within a square of its edge). The workspace keeps
  one per room until the content changes or another map is loaded (`DmWorkspace.GlanceOf`). What changes
  during the evening, the checklist and the notes, is read at every render (`RoomBadges`), so a tick
  shows on the map at once and can never go stale. On a 138×177-cell level a drag of the party token,
  which renders the map pane on every report, answered in 5 to 6 ms with the badges and without.
- **S means a secret way, not a secret block.** The redesign's plan read it from secret doors, blocks
  marked secret and secret exits. On a real floor 72 rooms of 89 have a block marked secret, because
  that flag means "DM only, never read aloud", so S was on nearly every room and said nothing. It is now
  a secret door in the room's walls, a secret or hidden-door marker in it, or an exit the players do not
  know of (18 rooms of 89 there). A creature listed as secret is in the creature badge, and marked on
  the peek card.
- **Labels keep one size on screen.** They were drawn in map units, so with a large level in view they
  could not be read. Their CSS sizes them from `--dt-u`, world units per screen pixel, which
  `dt-pan-zoom.js` writes on the map's wrapper when the zoom or the box's size changes, never while
  panning. A new value restyles the whole map, but a zoom step on that level took about 300 ms with it
  or without it: the cost is the map drawn again at the new scale. The overlay carries `--dt-u0`, the
  size the labels used to have, for the first paint and for an SVG shown without the canvas.
- **A name that does not fit its room is hidden** (`declutter` in `dt-pan-zoom.js`). At one size on
  screen, the long names of a crowded floor lay across each other: with a whole level in view, 78 of 87
  overlapped. A name wider than its room's box is hidden and its badges stay, which is what the DM reads
  at that distance; zooming in brings the names back. It runs when the zoom changes, and, through a
  `MutationObserver`, whenever Blazor puts a new overlay in, before the browser paints it. The name and
  the badges are two text elements so that one can go without the other.
- **The peek card** (`RoomPeek`). `dt-peek.js` notices the pointer resting on a region for a quarter of
  a second and tells the canvas; the map pane draws the card over the map, and the script places it
  beside the pointer. Being drawn by .NET, it is the same component the Nearby list shows, so the two
  say the same thing in the same words. A press, a pan, a zoom or leaving the room puts it away; the fog
  brush and measuring, which are about the map's shape, show none.
- **Nearby** (`NearbyRooms`): one line per exit, with the badges and the creatures still up of the room
  it leads to, and that room's card on hover, shown by CSS since it is in the page already. It took the
  place of the Connections list at the foot of the Room tab, which listed the same exits with less.

## One somebody in several rooms

A published adventure lists who is where, and it lists the same people in more than one room: the boss
at his card table, in the hall where the garrison gathers once alerted, and in the room he retreats to;
the gang's bandits at the gate, in the barracks, and all of them again in that hall. The table used to
tick each room's list off on its own, so the boss dealt with at his table was still up in three other
rooms, and the map counted the gang two or three times over.

- **Who counts across rooms follows what the reference names**, nothing guessed (`EncounterMonster.Shared`).
  An entity, a person or a character is one particular somebody, the same in every room that lists
  them. A stat block listed as itself is a kind of creature, and each room's are its own. An entity
  with a `count` is a **band**: one gang, listed in parts.
- **Dealt with is for good; fled is about one room.** The table keeps per area, beside the old whole
  listing tick, how many of a creature were counted down there as dealt with and how many fled
  (`CreatureCount`, on `AreaProgress`, saved as an additive field). What is left in a room
  (`CreatureStanding`) is its own listing less what was dealt with or fled there, and, for one somebody
  or a band, never more than the band has left: its size less everything dealt with of it in every
  room. Fled takes the boss out of the room he ran from and leaves him up where he went.
- **Nothing is stored that can be worked out.** What is left is worked out at each render from the
  records, as "cleared" always was, so the badges, the peek card, Nearby and the encounter strip read
  the same numbers and can never disagree. A record in a room whose listing has since been edited
  away still counts: the fight happened.
- **A band's room counts are written as counts, never as the whole-listing tick.** Ticking a room's
  bandits off records how many were left there, so a hall that lists the whole gang, ticked off after
  most of them fell elsewhere, takes away only the ones still up. A kind of creature keeps the
  whole-listing tick, which stays right if an edit changes its count. A tick from an older build on one
  somebody counts as him dealt with, everywhere.
- **The DM can move a creature where the book does not put it** (`CreatureMove`, a list on
  `CampaignSnapshot` in the order the moves were made, saved as an additive field). A kind of creature
  or a band moves by count: a room has what it lists, plus what was moved in, less what was moved out,
  and the rest of the arithmetic is unchanged. One somebody is in one place, so only his last move
  counts: he is in that room and gone from every room that lists him, until he flees from it, which
  sets him loose again wherever the book lists him. The room a creature is moved to need not list it:
  `EncounterSource.WithMoves` adds a row for it, read from the room that does
  (`CreatureMove.ListedInAreaNodeId`, carried along as it moves on) rather than copied into the save,
  so a content edit to its name or stat block shows there too. The moves are table state, so the
  workspace adds them each time the encounter or a room's glance is read and never keeps them in the
  glance cache. A whole-listing tick in the room they arrive in becomes a count of the ones that were
  there, or the newcomers would arrive already dealt with; for the same reason a row that moves touched
  is counted, never ticked whole. A room is "moved elsewhere" only when nothing else happened to its
  creatures there: some fell and the rest left reads as dealt with, with its move in a chip of its own.
- **Putting back** clears the room's record, and, for one somebody or a band still gone after that,
  what was dealt with of them everywhere: unticking the boss in a room he was not killed in means he
  is alive again. Who fled from where is kept.
- **The content checks keep a band's numbers honest**: an entity's count reads as a whole number, only
  a creature has one, and no room lists more of one somebody or a band than there are
  (`LinkAudit.OverfullListings`). And no numbered area and its lettered rooms list the same kind of
  creature twice (`LinkAudit.RepeatedKinds`): that is the shape the double counting took on every
  floor of the first campaign run with this, a room's creatures listed under its numbered area too,
  and nothing at the table could tell it from two groups. A band whose count is all of them is right
  either way, so the rule needs no judgement. At the table the rule is fail-soft: a listing larger than the band
  widens it rather than hiding creatures the room says are there.

## Answers that come to you

General lookups were three clicks deep and found by name only: how big the rooms are here was Campaign,
Rules, open a section, and a search for "ceiling" found nothing, because search matched names. Now one
box finds names and words, what the DM looks up every session is pinned, and the floor's standing facts
are beside the room.

- **Find anything** (`FindBox`, Ctrl+K or /) is one box over the map, never over the dock, so the DM
  keeps the room in view while looking something up. It lists things found by name first (the same
  search the Room Editor's node picker uses), then passages found by their words, each with the words
  around the match and the query marked (`TextHighlight`), so most lookups end in the list. A name opens
  as a link would; a passage opens as a sheet over the dock (`PassageSheet`). It replaced the Campaign
  tab's Search, which matched names only.
- **A passage is a block of prose as the content already holds it** (`Passage`): a rules section, one
  of a floor's facts, a room's read-aloud, at-a-glance bullet or detail, a person's entry, a quest's hook
  or beat, a scene, a deck's card, the background, the party, the story. Nothing new is authored.
  `PassageIndex` reads them once per content snapshot, inside `AuthoredProjection`, so a reload rebuilds
  it with everything else. A heading with nothing under it (a room with no read-aloud) is not a passage.
- **A passage matches when it holds every word of the query**, anywhere in its heading or body, in any
  order. Hits rank by where the words are: the whole query in the heading, every word in the heading,
  the whole query in the body, then every word anywhere; within a rank the rules and the floor's facts
  come first (they are what is looked up mid-session), then the rooms, then the rest, then the content's
  own order. The words matched are the words the DM reads: each link as the name it shows, through the
  same resolver the page uses (`MarkupCrossRefResolver.Words`), so a passage is found by "Grask" and not
  by `[[grask]]`'s markup. Plain substring tests over a few thousand passages take well under a
  millisecond a keystroke on a large campaign, so there is no index cleverer than a list of lower-cased
  strings.
- **The result shows the answer.** A long passage shows about 200 characters around the first match,
  cut at words, with an ellipsis where it was cut; a short one shows whole, on one line.
- **A passage's key is where it sits and its heading** (`PassageKeys`): `rules/darkness-below`,
  `level/<floor>/factions/grasks-crew` (a block's id wins over its heading), `area/<area>/detail/...`.
  Never its place in the list, so a section inserted above another does not move a pin onto the wrong
  one; two blocks with one heading get `-2`, `-3`. The index and the DM screen's pin buttons build keys
  with the same functions, so they cannot drift apart.
- **The passage sheet is a lookup, not a stop in the history.** It stands outside `NavHistory`: Esc puts
  it away and the dock is as it was, a card underneath included. Following a link in it, going to a room
  or the dock following the party puts it away too (`DmWorkspace.Render`). A content reload reads it
  again by its key, and closes it when its block is gone.
- **Pins** keep what the DM looks up every session one click away (Darkness Below, Resting in the
  Undercroft, a player character's armour class). A rules section, a floor's fact, a card or any passage gets a 📌
  (`PinButton`); the pins sit as chips along the foot of the dock on every tab, the fight's included
  (`PinStrip`), and a click shows the text in a popover over the dock. A pin is `text:` and a passage's
  key, or `card:` and the id a link opens the card by (`PinKeys`). A pinned card is worked out exactly as
  the card sheet works it out (`DmWorkspace.CardFor`, which `Render` uses too), so it is the whole card.
- **The pins are the table's**, on `CampaignState` beside the notes, saved as `CampaignSnapshot.Pins`,
  an additive field with an empty default, so `TableSnapshot.CurrentVersion` did not change. A second DM
  window and a restart show the same pins. A pin whose passage or card the content no longer holds stays,
  dashed, until the DM takes it off: a heading renamed in an edit should not make a pin vanish without a
  word.
- **The floor's ⓘ** on the Room tab's header opens the floor's standing facts under it (`LevelFacts`:
  what dwells here, the factions, the wandering monsters), the same component the Campaign tab's Level
  section shows, so "who else is on this floor" needs no other tab.
- **Ctrl+K needs a round trip before the box exists**, and a DM who types straight away lost the first
  letters to the page. `dt-find.js` therefore holds what is typed from the moment the shortcut is pressed
  until the box is up, keeps it from the other shortcuts (a "3" typed early is a letter, not a tool), and
  puts it into the box. The arrow keys and Enter in the box are the script's too, since swallowing the
  arrows' caret move is decided when the key arrives.

## What a reveal sends

Blazor Server sends a component's changes as a diff, but markup handed over as a `MarkupString` is
diffed as one piece: if one character changes, all of it goes again. The map used to be one such piece,
so every lift, door or ping sent the whole SVG again to every DM screen (1.7 MB on a large level) and to
the projector (2.8 MB), and each browser parsed it again.

`VectorMapCanvas` therefore draws a Razor `<svg>` that holds three pieces, and Blazor sends only the
pieces whose text has changed:

| layer | DM screen | projector |
|---|---|---|
| map | the map itself; drawn again only when `MapLayerKey` changes (a door, a secret door found, a concealed object shown, the grid) | the floor, wall and grid geometry as definitions, the floor clip, the hatch and the sprites; once per map |
| middle | the fog: revealed rooms, and the revealed cells as one path of row runs | the revealed tiles, the peek rings and the walls, drawing the geometry with `use` |
| top | the editor overlay | the doors, stairs, decorations and markers, clipped to the revealed tiles |

On a large level a lift now sends a few kilobytes to each DM screen and 5 to 40 KB to the projector.
The `<svg>` element itself now stays in place between renders, so an empty `g.dt-live` group kept last
in it holds the editor's drag previews above the layers when Blazor replaces one.

`Render` and `RenderRevealed` still return one SVG, built from the same pieces, and tests hold the
layers to them: the DM canvas must produce exactly `Render`'s output, and the projector's layers, with
each `use` expanded into what it draws, must define and paint exactly what `RenderRevealed` does. A
door still sends the DM's whole map layer again; the doors could move to a layer of their own if that
ever matters.

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
- **An enemy's row carries what its turn needs**: its six saves and its attacks (`EnemyGlance`), read
  from the live stat block. The DM keeps one creature's pane open all fight (the one with the most to
  read) and runs the rest from the order, so the pane never follows the turn marker: it changes only when
  the DM opens another. The pane and the row word an attack the same way (`AttackText`).
- **Nothing demands a roll.** A room's creature count ("4", or a dice expression such as "1d4+1") is
  a pre-fill the DM can change.
- **The list never re-sorts under the cursor.** Enter and Tab walk the initiative column, and the order
  is sorted only once the last blank is filled.
- **The detail pane stands on its own.** Damage, healing and conditions are repeated there, beside the
  stat block and the room's own notes on that creature, so the DM never has to go back to the Room tab
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

**A picture turns and flips per picture** (`ArtOrientation`, on `SessionState`, saved with the shown
art): a scan on its side, or a portrait facing the wrong way, is fixed once and stays fixed as the tray
flips through. The player view turns the whole frame and flips the picture inside it. Turned on its
side, the frame is laid out upright and turned after, so each size limit is taken on the other axis
(`on-side` in `PlayerArtOverlay`); that is what lets a tall picture on its side fill a wide screen. At
**Full** the picture is as large as the screen allows, worked out from its shape, because the
catalogue's pictures are a few hundred to fourteen hundred pixels high and drawn at their own size they
filled nothing.

## The projector on a table

When the projector shines down onto the table, the miniatures stand on the map, and two settings of the
table follow from that, both on `SessionState` and saved as additive fields of `RevealSnapshot`:

- **The picture turns a quarter at a time** (`PlayerTurns`), for players sitting around the table. The
  player view turns the map and the DM's pointer inside a box the stage's height wide and its width
  high, turned about the middle, so it covers the stage exactly. Every framing is cut to
  `FramingAspect`, the screen's shape or the other way up, so the DM's box is the shape the players see,
  with a heavier edge for the top of their screen. A turn turns the view about its own middle, on this
  map and on every map set aside, so the scale does not change: a square on the table stays the size
  it was. The picture over the map turns on its own (above).
- **A size in squares, remembered** (`RememberedView`): the size of the view in grid squares across and
  down the projector's own screen, so a turn does not change it. Used on another map, the squares
  across hold the scale and the squares down follow the screen's shape (`MapFraming.AtSize`); the view
  is moved onto the map but never shrunk to fit it, since shrinking would change the size of a square.

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
