# Using DungeonTable

_This guide covers using the app at the table and writing a campaign for it. The [README](../README.md)
says what the app is and how to start it. [content-format.md](content-format.md) describes every
field a campaign can hold, and the sample pack in [samples/demo](../samples/demo/README.md) is the
example this guide uses throughout. Start the app on the sample pack and follow along._

1. [Running a session](#1-running-a-session)
2. [Writing your own campaign](#2-writing-your-own-campaign), by hand or [with an LLM](#writing-it-with-an-llm)
3. [Troubleshooting](#3-troubleshooting)

## 1. Running a session

### Before the players arrive

The app has two screens, served by the same address:

- **`/dm` is yours.** Open it on the laptop in front of you. Everything below happens here.
- **`/player` goes on the projector.** Open a second browser window, drag it onto the projector (or
  the TV the players can see), go to `/player`, and press **f** or the **Full screen** button. The
  button fades once the mouse is left alone. The window keeps itself up to date: you never touch it
  again during the session.

Both ask for the passphrase the first time. Sign in through the browser's own prompt. Do not put the
passphrase in the address (`http://dm:secret@host/dm`): the page then loads but does not respond.

The projector shows only the map, and only what you reveal. At the start of a session that is
nothing. Open `/player` before you frame anything on the DM screen: the projector reports its shape to
the DM screen, and the frame you draw takes that shape.

### The DM screen

![The DM screen: the map with badges under each room's name, the party token in the Goblin Den with its vision ring, the player view framed on the Nave, the Workshop's peek card under the pointer, Find anything in the bar, the Goblin Den on the Room tab, party here, with the rooms around it under Nearby, and two pins along the foot of the dock](images/dm-screen.png)

The DM screen is the map, which never leaves the screen, and beside it the **dock**, with four tabs:
**Room**, **Battle**, **Campaign** and **Art**. **Ctrl+1** to **Ctrl+4** switch the dock from
anywhere; the map stays where it is, so you can reveal, open doors and move the projector's view
whichever tab is up. The bar across the top is in view all the time:

- **Players see 3 regions · 0 cells** is what the projector shows right now.
- **Viewport: Nave** is the room under the middle of the projector's view.
- **Frame** moves the projector's view onto the room on the Room tab, or the whole map when it has no
  room on this map.
- **Follow party**, beside it, keeps the projector on the party: while it is on, the projector frames
  the party again whenever the token leaves the middle of its view. It is off until you turn it on.
- **Find anything** (**Ctrl+K**, or **/**) looks up a room, a person, a rule or any line of the
  campaign's text (see [finding anything](#finding-anything-and-pins)).
- When the campaign has more than one map, the **map picker** switches between them. The projector
  follows. Each map keeps what you revealed on it, its open doors, where the projector was framed and
  where the party stands, so when the party goes back to a floor they see it as they left it. A map shown for the first time
  starts dark, with its doors as the Room Editor set them.
- The save status is on the right (see [what "Not saved" means](#what-not-saved-means)). When a
  picture is on the projector, the bar says so too.

**The dock's width is yours.** Drag the line between the map and the dock to make it wider or
narrower; the screen remembers it in this browser. Double-click the line to go back to the usual width.
**\\** (or the **⇔** button at the end of the dock's tabs) swaps the dock between narrow and wide.
The **Battle** tab keeps a width of its own, wide unless you change it (see [a fight](#a-fight)); Room,
Campaign and Art share the other. On a screen narrower than about 1200 pixels, the dock slides over
the map instead: **Tab**, or the **Dock** button in the bar, brings it out and puts it away.

### The map and its tools

Rooms the players can see are drawn light; the rest are shaded. The box labelled **Player view** is
exactly what the projector shows. Drag it by its border to move the projector's view, or by a corner to
zoom in or out, or press **Frame**. Drag anywhere else on the map to pan it, and use the mouse wheel
(or **+**, **−** and **Reset** at the bottom right) to zoom.

**The map says what is in each room.** Beside each room's name are badges that only you see:

| badge | means |
|---|---|
| **☠ 3** | creatures there not yet ticked off on the Room tab. A count written as dice stays as written: Nib and 1d4+2 goblins read **☠ 1d4+3** |
| **✔** | cleared: every creature there is ticked off. The room is greyed too |
| **◆** | loot: the room has a block tagged loot |
| **⚠** | a trap or hazard marked in the room in the Room Editor |
| **S** | a secret way: a secret door in its walls, a secret or hidden-door marker, or an exit the players do not know of |
| **✎** | you wrote a note on the room |

Names and badges stay the same size however far you zoom. With a whole floor in view, a name wider than
its room is left out and its badges stay; zoom in and the names come back. **c**, or **Badges** at the
foot of the map, switches the badges off for a clean map.

**Rest the pointer on a room** and after a moment a card says what is in it: who is there (crossed out
once ticked off), the loot, the checks, traps, secret ways, where it leads and your note. Move off the
room, click, pan or zoom, and it goes. The Fog brush and Measure show no card.

**The party is a token, and moving it is how you reveal.** The blue token is where the party stands,
and the faint ring round it is how far they see.

- **Put the party down** with the **Party** tool (**4**): click where they stand. The token goes there
  and the party sees from there. Use it at the start of a session, and when the party arrives without
  walking (the stairs, a teleport): it shows nothing on the way.
- **Drag the token to walk the party.** Everything the party could see from each square on the way is
  revealed as it goes, stopping at walls, closed doors and secret doors nobody has found. Squares of
  solid rock you drag across do not count. Drag it in Look or with any tool but Fog and Measure,
  which use the drag themselves.
- **Opening a door shows what is behind it**: the party looks again from where it stands.
- **Click the token** for its vision range: **30**, **60** or **120 ft**, or type another. A longer
  range shows straight away what it reaches. **Take the token off the map** is there too.
- **Ctrl+Z** (or **Undo move** at the foot of the map) takes back the party's last move: what it showed
  is hidden again and the token goes back. A whole drag is one move. What an opened door showed goes
  back with the door, which closes again. It goes back up to fifty moves, on the map you are on.
- The players never see the token, only what the party has seen. What they have seen stays revealed.

On a map with no calibrated grid the token still marks the party's room, but reveals nothing.

**A click looks.** Unless you pick a tool, the map is in **Look**:

- **Click a room** and the Room tab shows it. Nothing changes for the players.
- **Click a door** to open or close it. A secret door the players have not found is left alone: a click
  on it reads the room behind it, so you never give one away by accident. Discover it with the Doors
  tool; once found, a click opens and closes it like any other door.

**The tools are for the occasional job.** Pick one from the bar over the map, or press its number.
**Esc** always goes back to Look.

| key | tool | what it does | afterwards |
|---|---|---|---|
| **1** | Look | Click a room to read it, click a door to open or close it. | |
| **2** | Reveal | Click a room, a door or a marker (a trap, a hazard) to show it to the players, and again to hide it. | back to Look |
| **3** | Fog | A brush: drag to reveal grid cells. Hold **Alt**, or drag with the right button, to hide them again. For caves, and for rooms the players only glimpse. | stays |
| **4** | Party | Click where the party stands: the token goes there, and what the party sees from there is revealed. Nothing on the way is: to walk, drag the token instead. | back to Look |
| **5** | Doors | Click a door to open or close it. A secret door is drawn as wall until you click it to discover it; click again to open it, and again to hide it. | back to Look |
| **6** | Conceal | Click a concealed object (a hidden stair, a pit, a chest) to show it to the players, and again to hide it. It is drawn ghosted on your map until then. You mark what is concealed in the [Room Editor](#step-3-mark-the-rooms-in-the-room-editor). | back to Look |
| **7** | Measure | Drag between two points to read the distance, with diagonals counted the 5e way. Only you see it. | stays |
| **8** | Point | Click a spot to point at it on the projector. It pulses a few times and fades. | back to Look |

"Back to Look" happens after a click that did something, so a click that misses keeps the tool. To keep
one of those tools up, for revealing a whole wing room by room, hold **Shift** as you pick it
(**Shift+2**, or Shift and click). Pressing a tool's number again also puts Look back.

**Clear reveals…** at the foot of the map hides everything on this map again, and offers to undo the
clear. Other maps keep their reveals, and the token stays where it is. **Grid** draws the grid, and
**Badges** switches the badges on and off (**c**).

The single-key shortcuts (these, and the ones on the Battle and Art tabs) do nothing while you are
typing in a box, so a note never changes your tool.

### The Room tab: what the room says

The Room tab follows the party: walk the token into a room and that room is on the Room tab, ready to
read. Click another room on the map to look at it; a strip at the top then says which room the party
is in, and **Back to the party's room** (or **Esc**) goes back. Looking at another room lasts until the
party walks into a new one, and then the tab follows it again, unless you **Pin** it: a pinned tab stays
on what you show it whatever the party does.

The room is laid out in the order you need it at the table:

- **The header**: the area number, the title, and whether the **party is here** and the room is
  **cleared** and **revealed**. **Reveal** shows the whole room to the players (a room drawn as several
  regions is revealed whole), **Frame** frames the projector on it, **Centre** brings it to the middle of
  your map, and **Pin** keeps the tab here while the party moves. **ⓘ Floor** opens the floor's standing
  facts under the header: what dwells here, the factions and the wandering monsters, so "who else is on
  this floor?" needs no other tab. **←** and **→** step back and forth through what you opened.
- **The text to read aloud**, in a box, first.
- **Skill checks** as DC pills, then **At a glance**, the short list to read next.
- **The encounter**: each creature the room lists, with where its stat block comes from and how many
  there are. A count written as dice (**1d4+2**) is yours to roll: set the number with **−** and
  **+**. **Add** puts them in the fight, and **Add all to battle** puts in everyone. Tick a creature
  off once it has been dealt with; when all are, the room reads **cleared**.
- **Nearby**: the rooms this one leads to, one line each, with their badges, who is still up in them and
  how you get there. A secret way is marked **DM**. Rest the pointer on a line for that room's card, and
  click it to read the room. A way to a floor you have not written yet waits as plain text.
- **Detail**, folded until you open it, with secrets marked **SECRET**.
- **Notes**: your own note on the room, for what happened here, one line until you click into it. It
  is kept with the table.
- At the foot, folded: who is here and how they are tied to each other.

![A card over the Room tab: Nib, with the breadcrumb back to the Goblin Den and the card's pin beside it](images/dm-card.png)

Every underlined name is a link. A person, a monster, a spell or a faction opens a **card** over the
dock: who they are, their stat block, where they appear and how they are tied to others. Its **📌**
pins it along the foot of the dock (see [pins](#finding-anything-and-pins)). **Esc**, or the card's
**×**, puts it away, and the room is still there underneath. **←** and **→** (or **Alt+←**
and **Alt+→**) walk back and forth through what you opened, and the row of names beside them jumps
straight to one. A link to a room shows that room on the Room tab.

### The Campaign tab

The **Campaign** tab (**Ctrl+3**) holds the rest of the campaign, in tabs of its own:

| tab | what it holds |
|---|---|
| Level | the floor: who lives here, the factions, the wandering monsters, and the campaign's background below |
| Quests | the quest log. Tick each beat as it happens; a beat's destination shows its room |
| Party | the player characters, with the numbers you are asked for mid-turn in view |
| NPCs | the campaign's people. Type to filter them by name, place, or anything they know |
| Story | what happened before this campaign |
| Session | tonight's prep, scene by scene |
| Rules | the campaign's rules, and its card decks: **Draw** hands out a card. A deck whose cards the players keep remembers which are out until you reset it |

The tab you leave it on stays put when you look at another room. Each rules section and each of the
floor's facts has a **📌** in its corner, to keep it along the foot of the dock. Searching is not a tab
here: it is [Find anything](#finding-anything-and-pins), over the map.

### Finding anything, and pins

![Find anything over the map: "darkness" typed, with the Darkness spell, a book entry and the Darkness Below rules section, the word marked in each](images/dm-find.png)

**Find anything** is one box over the map. Open it with **Ctrl+K**, **/**, or the box in the bar, and
type: it looks through names (rooms, people, monsters, spells, the books the campaign indexes) and
through words, in every rules section, the floor's facts, each room's read-aloud and detail, the
people's entries, the quests, the session prep, the decks, the background, the party and the story.
"ceiling" finds the paragraph that says how high the ceilings are, wherever it is.

- Things found by their name come first, then passages found by their words, each with the words
  around the match and what you typed marked. Most lookups end there, in the list.
- **↑** and **↓** choose a row; **Enter** or a click opens it. A name opens as a link would: a room on
  the Room tab, anything else as its card. A passage opens as a sheet over the dock, with its links
  working, and **Read the room** or **Open the card** when it comes from one.
- **Esc** puts the box, then the sheet, away; nothing moves the map. Open the box again and your last
  search is still there.
- Every word must be somewhere in the passage, in any order: "goblin fire" finds the rules section that
  mentions the goblins' fire.

**Pins** keep what you look up every session one click away. Pin a rules section or a floor's fact by
the **📌** in its corner, a card or a passage by the **📌** at its top. Pinned things sit as chips along
the foot of the dock, on every tab, the Battle tab included: click a chip to read it in a popover over
the dock, click it again or press **Esc** to put it away, and **×** on the chip takes the pin off. A
pinned card is the whole card, so a player character's pin has their armour class and passive
Perception in it. The pins are kept with the table, like your notes. A pin whose section was renamed
or deleted stays, dashed, until you take it off.

### A fight

![The Battle tab: a fight in the Goblin Den, each enemy's saves and attacks on its row, Nib frightened and his pane open, and the map a bird's-eye view of the den](images/dm-battle.png)

**Starting one.** Press **Add all to battle** on the room's encounter, on the Room tab: that starts
a fight in the room, with the party, and puts in everyone the room lists. **Add** on one line puts in
just those. **Start battle in Goblin Den**, on the Battle tab, starts a fight with only the party in
it, for a fight the room does not list. While a fight is running, adding from another room asks
whether to add to it or start a new one there, which ends the old one.

**Initiative.** Every row starts blank. Type what each player calls out. **Enter** moves to the next
row that is still blank, and once none is, the order sorts itself, highest first. **Tab** moves to the
next box without sorting. **r**, or **Roll monsters**, rolls d20 plus Dexterity for every monster row,
and you can overwrite any number it rolls. **Sort** sorts again. To settle a tie, drag a row by its
**⋮⋮** handle, or focus the handle and use the arrow keys.

**Turns.** **n** and **p** (**Next** and **Prev**) move the turn marker **▸**, and the round counts
up as it wraps. A row whose creatures are all at 0 hit points is skipped.

**Hit points.** Type a number in a creature's **dmg** box: **Enter** takes that much damage and
**Shift+Enter** heals it. The **−** and **+** buttons do the same. A creature at half its hit points
or less is marked **bloodied**, and at 0 **down**.

**Conditions.** **◇** opens the conditions. What you pick shows on the row; click it there to clear
it. **…** opens the rest: renaming, a note, concentration, and correcting the numbers.

**What an enemy can do, on its row.** Under a monster's name, its row shows its saving throws (the
ones it is proficient in stand out) and its attacks: the bonus to hit and the damage, average first.
Hover over an attack for its reach and the book's sentence. Its AC is on each creature's line. So you
can keep Grask's pane open all fight and still run his two bugbears from the order. Everything
else about a creature is in its detail pane.

**Groups.** Monsters of one kind share a row and an initiative, as most tables run them, with each
one's hit points listed under it. **▾** folds a group to one line, which still shows how many are up
and their AC. **Split** gives each its own row, and **Merge** puts a split group
back together. A named creature (Nib, who fights with the goblin's stat block) always has a row of
his own.

**The detail pane.** Click a creature's name for its full stat block: its attacks as short lines, its
saves, its spells (click one to read it) and its spell slots. It stays open until you close it or open
another creature's, whatever the turn marker does. **Esc** closes it.

**The fight's layout.** The Battle tab widens the dock by itself, so the order and the detail pane sit
side by side, each about half of it, and the map becomes a narrow bird's-eye view, framed on the
fight's room and a few squares around it the first time you open the fight. Move it as you like:
going back to the room puts your map back the way it was before the fight, and coming back to the
fight finds the bird's-eye view where you left it. A fight in another room is framed afresh. If you
drag the dock narrower than about 1,090 pixels, the pane goes under the order instead.

**Adding and ending.** Below the order you can add a party member, an ally, or a one-off creature
with just a name. **Edit party** and **Edit allies** change the rosters. **End battle** clears the
whole fight, after asking, and cannot be undone.

The fight never reaches the projector: the players see the map, and the fight happens on the table.

### Pictures on the projector

The **Art** tab (**Ctrl+4**) puts a picture over the map on the projector: a monster as it comes
round the corner, or a handout.

- Search the catalogue and add pictures to the **tray** before the scene.
- Click a picture in the tray to show it. **n** and **p** step through the tray, and **h** hides the
  picture and brings it back.
- **S**, **M**, **L** and **Full** set its size on the projector.
- **Add from the fight** adds a picture for every creature in the running fight that has one.
- **Your own pictures**, at the bottom, adds one from your computer. It is converted to WebP and kept
  in `data/art/uploads/`.

### What "Not saved" means

What happens at the table is saved in a PostgreSQL database: what you revealed, the fight, quest
ticks, the cards drawn, the rooms dealt with and your notes. The Docker setup in the README includes
one. The status on the right of the bar says where that stands:

| status | what it means |
|---|---|
| **Not saved** | No database is configured (`ConnectionStrings:Postgres`). Everything lives in memory: it survives a page refresh, but not restarting the app. |
| **Not saved yet** | There is a database, and nothing has changed yet. |
| **Saved 21:14** | Saved. Every change is written within a couple of seconds. |
| **Save failed** | The database could not be reached. The app keeps trying; hover over the status for the reason. |
| **Save blocked** | The table in the database was saved by a newer version of the app. Saving is paused so that it is not overwritten. Update the app. |

Your campaign's own files are never written to, except by the Room Editor, the party and ally
editors (without a database), and uploads.

## 2. Writing your own campaign

A campaign is a folder of maps and JSON documents. This section goes from an empty folder to a first
room you can play. It rebuilds two rooms of the sample pack, so you can compare every file with the
one in [samples/demo](../samples/demo), and every file below passes the content tests as written.

The app reads a file again when it changes, so keep it running while you write: save a file, and the
DM screen shows the change a moment later. A file saved half-finished does not take anything away;
the table keeps what it had until the file reads again.

### Step 1: the folder

Make this folder structure, and copy the three files marked from the sample pack:

```
my-campaign/
  Maps/silent-bell/
  data/dossiers/
  data/statblocks/monsters-srd.json    from samples/demo/data/statblocks/
  data/statblocks/spells-srd.json      from samples/demo/data/statblocks/
  data/book-index/srd.json             from samples/demo/data/book-index/
  data/art/
```

The two `*-srd.json` files are every monster and spell in the SRD 5.1, so a goblin or a *fireball*
works from the start. `srd.json` names the book they come from, so their stat blocks cite "SRD 5.1".
Make every folder now, even the empty ones: the app will not start without them. The book index is
the one folder it can do without, and it looks for that folder only when it starts.

Then point the app at the folder, from the folder that holds `DungeonTable.slnx`:

```bash
dotnet user-secrets set "Content:Root" "/path/to/my-campaign" --project DungeonTable.Web
```

With Docker, set `CONTENT_DIR=/path/to/my-campaign` in `.env` instead. Start the app. The DM screen
is empty until the next step.

### Step 2: draw the map

Draw the floor in [Dungeon Scrawl](https://www.dungeonscrawl.com): rooms, walls, doors and stairs,
and sprites for statues, pits and chests. Save it as a `.ds` file, and put it in
`Maps/silent-bell/undercroft.ds`. The folder below `Maps/` is the adventure, and the file name is the
map's id.

The map shows on the DM screen at once, all of it in fog. To compare with the pack's map, copy
`samples/demo/Maps/demo/level-1-undercroft.ds` there instead.

### Step 3: mark the rooms in the Room Editor

![The Room Editor: the Goblin Den selected and linked to its area](images/room-editor.png)

The map is a drawing: the app does not know which shapes are rooms. Open `/editor` to tell it:

1. Press **Draw rectangle** and drag over a room, or **Draw polygon** and click its corners for a
   room no rectangle fits.
2. **Link it to its area.** An area is a room as the campaign writes it, and it is known by an id.
   Type the id in full in **Links to**: `data_dossiers_level_1_area_1` for area 1 on floor 1. Click
   **Link to data_dossiers_level_1_area_1** below it, and the region is tagged **not written**: the
   area it opens is written in the next step. Once areas are written, the box finds them by name.
3. Give it a **Label**: the name you see in the editor's lists.
4. Do the same for the next room: `data_dossiers_level_1_area_2`.
5. Press **Save**. It shows **Save\*** while there is something to save.

The editor writes `Maps/silent-bell/undercroft.regions.json` beside the map. While you are here:

- **Level name** is what the map pickers call the map.
- Click a **door** to make it a secret door (drawn as wall until the DM discovers it), open at the
  start, or double.
- Click a **stair or a decoration** to make it concealed: the players do not see it until the DM
  reveals it with the Conceal tool.
- **Place marker** puts a trap, a hidden door, a secret or a hazard on the map, for the DM's eyes
  until it is revealed.
- A room that no one shape fits can be several regions: select one and press **Add another region
  for this area**.

### Step 4: write the level

Each floor is a file, `data/dossiers/level-<n>.json`, where `<n>` is the floor's number. Save this as
`data/dossiers/level-1.json`:

```json
{
  "level": {
    "levelNodeId": "data_dossiers_level_1",
    "title": "Level 1: The Undercroft",
    "designedFor": "Three or four 2nd-level characters.",
    "whatDwellsHere": "[[goblin|Goblins]] have moved into the undercroft below the chapel, and the miller's son went down after them."
  },
  "areas": [
    {
      "areaNodeId": "data_dossiers_level_1_area_1",
      "title": "Undercroft Landing (Level 1, Area 1)",
      "readAloud": "The stair from the chapel ends in a low stone room that smells of tallow and cold ashes. To the west a passage runs into the dark, and somewhere down it a fire crackles.",
      "glance": [
        { "heading": "Exits", "kind": "exits", "body": "The stair climbs back to the chapel. A passage runs west to the goblins' den ([[area 2]])." },
        { "heading": "Candle-Ends", "kind": "feature", "body": "Stubs of chapel candles, most of them chewed. The teeth marks are too wide for rats." }
      ],
      "creatures": [],
      "exits": [
        { "to": "area 2", "note": "West along the passage." }
      ]
    },
    {
      "areaNodeId": "data_dossiers_level_1_area_2",
      "title": "Goblin Den (Level 1, Area 2)",
      "readAloud": "A smoky room with a fire of broken pews burning in the middle of the floor. Small figures leap up from around the fire, shrieking.",
      "glance": [
        { "heading": "Exits", "kind": "exits", "body": "The door east opens onto the passage to the landing ([[area 1]])." },
        { "heading": "Sacking Beds", "kind": "feature", "body": "Mill sacks stuffed with straw. Two still say BRASK in faded paint: [[wenna-brask|Wenna]]'s sacks." }
      ],
      "detail": [
        { "heading": "Loose Flagstone", "kind": "loot", "secret": true, "body": "Under a flagstone in the corner: 23 sp and Tam's knife, its handle carved with a mill-wheel." }
      ],
      "skillChecks": [
        { "ability": "Wisdom (Perception)", "dc": 13, "purpose": "Find the loose flagstone." }
      ],
      "creatures": [
        { "ref": "goblin", "count": "1d4+2", "note": "Around the fire. They scatter once three have fallen." }
      ],
      "exits": [
        { "to": "area 1", "note": "The door east, and the passage to the landing." }
      ]
    }
  ]
}
```

Save it, and click the Goblin Den on the DM screen's map: its briefing is there, and **Add all to
battle** puts the goblins in a fight. The regions you drew are now tagged **linked** in the Room
Editor.

What the parts do:

- **`areaNodeId`** is the id the Room Editor linked. The pattern `data_dossiers_level_<n>_area_<key>`
  is what lets a region, or a quest, name an area before it is written.
- **The title ends in `(Level 1, Area 2)`.** That is where the room's key comes from, the "2" that
  `[[area 2]]` and an exit's `"to": "area 2"` look for.
- **`readAloud`** is the boxed text you read to the players. **`glance`** is the short list you read
  first, and **`detail`** is folded until you open it. A block with `"secret": true` is marked as the
  DM's only.
- **`creatures` is the encounter.** Name a monster the way a link names it (below). `count` is empty
  for one, a number, or dice for the DM to roll. `[]` says, deliberately, that nobody is here.
- **`exits`** are where the party can go: they become jumps to those rooms.

Every field is described in [content-format.md §5.2](content-format.md#52-level-njson-a-floor).

### Step 5: links

Anywhere text is shown as prose, `[[target]]` is a link, and `[[target|words]]` shows other words:

| written | links to |
|---|---|
| `[[area 2]]` | area 2 on the same floor, in a level file |
| `[[L1 area 2]]` | area 2 on floor 1, from anywhere |
| `[[goblin]]`, `[[goblin\|Goblins]]` | the goblin's stat block, by its name |
| `[[wenna-brask\|Wenna]]` | a person, by the id you give them in `npcs.json` |

The app never turns a name into a link by itself: only what you bracket is a link. A link that leads
nowhere is shown as its words, marked as broken. The level file above links to `wenna-brask`, who is
not written yet, so the Goblin Den shows "Wenna" as broken until the next step. A link to a floor
with no level file yet, such as `[[L2 area 1]]`, is **pending** instead: muted now, and a live link
by itself the day `level-2.json` is written.

Outside a level file (in quests, people and decks) there is no floor to read a bare `[[area 2]]`
on, so write the floor: `[[L1 area 2]]`. The whole of it is in
[content-format.md §4](content-format.md#4-links).

### Step 6: a person

The campaign's people are in `data/dossiers/npcs.json`:

```json
{
  "summary": "The people of Millbrook, above the undercroft.",
  "npcs": [
    {
      "id": "wenna-brask",
      "name": "Wenna Brask",
      "role": "The miller, Tam's mother",
      "where": "The mill, by the ford",
      "disposition": "Ally",
      "summary": "Has run the mill since her husband died. Three days ago her son Tam went down into the undercroft, and he has not come back.",
      "knows": [
        { "heading": "Tam went down at dusk", "body": "Three days ago, with the mill's lantern. His boots have a split heel." },
        { "heading": "Goblins at the mill", "body": "Sacks have gone missing all autumn. She has seen small bare footprints in the spilt flour." }
      ],
      "hooks": [
        { "heading": "Her savings", "body": "She offers the party 15 gp, everything the mill has put by, to bring Tam home." }
      ]
    }
  ]
}
```

Save it: "Wenna" in the Goblin Den is now a link to her card, and she is on the NPCs tab. The `id`
is how links name her; write ids in lower case, with words joined by `-`. `knows` comes first on her
card, because "what can she tell us?" is what the players ask. Someone who might fight can have a
`statBlock` (`"statBlock": "commoner"`): listed in a room's `creatures`, they join a fight with its
numbers and their own name.

### Step 7: a quest

Quests are in `data/dossiers/quests.json`:

```json
{
  "quests": [
    {
      "id": "the-millers-son",
      "title": "The Miller's Son",
      "availability": "starting",
      "giverLabel": "Wenna Brask",
      "giverNodeId": "wenna-brask",
      "hook": "Tam went down into the chapel undercroft and has not come back. His mother, [[wenna-brask|Wenna]], begs the party to bring him home.",
      "beats": [
        {
          "id": "hear-wenna-out",
          "ordinal": 1,
          "summary": "Hear Wenna out at the mill.",
          "target": { "kind": "surface", "placeLabel": "The mill" }
        },
        {
          "id": "search-the-den",
          "ordinal": 2,
          "summary": "Find Tam's knife in the goblins' den.",
          "detail": "The goblins took it off him. It is under the loose flagstone in [[L1 area 2]].",
          "target": { "kind": "area", "level": 1, "areaKey": "2", "placeLabel": "Goblin Den", "nodeId": "data_dossiers_level_1_area_2" }
        }
      ]
    }
  ]
}
```

On the Quests tab it is a card with its giver, its hook and its beats, each ticked off as it
happens. A beat's `target` says where it happens. One in a written room is a jump to it; one on a
floor you have not written yet is marked "not authored yet". The beat and quest ids are what the ticks
are saved under, so keep them once you have played with them.

### Step 8: check it

The app is forgiving: a document it cannot read is skipped, and an entry with no id is dropped, so a
typo never takes the table down. That also means a mistake can go unnoticed. The **content tests**
read your campaign the way the app does and name every problem, file by file: a document that did not
load, a field its card expects that is missing, a link, exit, quest destination or region that leads
nowhere. From the folder that holds `DungeonTable.slnx`:

```bash
DUNGEONTABLE_CONTENT_ROOT=/path/to/my-campaign dotnet test DungeonTable.ContentTests
```

In PowerShell, set the variable first: `$env:DUNGEONTABLE_CONTENT_ROOT = "C:\path\to\my-campaign"`.
On the files above, every rule passes, or is skipped because there is nothing for it to check yet.

The content tests need the .NET SDK. Without it (running the app in Docker, say), read the app's log
as it starts: it names every document it skipped and every entry it dropped, as `Content problem in
data/dossiers/npcs.json: …`. With Docker Compose, that is `docker compose logs webapp`.

### Going further

With a room, a person and a quest working, the rest is more of the same. Each of these is a document
of its own, described in [content-format.md](content-format.md) and shown in the sample pack:

- **`characters.json`** and **`party.json`**: the player characters, for the Party tab, and the
  roster a fight starts with. The two must agree.
- **`campaign.json`**: the background, the room the DM screen opens on, and the table of levels.
- **`reference.json`**: the rules on the Rules tab. **`deck-*.json`**: card decks to draw from.
- **`sessions.json`** and **`story.json`**: tonight's prep, and what happened before.
- **`relations.json`**, or a level's `relations`: who is tied to whom.
- **`monsters.json`** and **`spells.json`**: your own stat blocks, beside the SRD's.
- **`data/art/catalogue.json`**: pictures for the Art tab.
- **`data/book-index/`**: what the books a campaign is run from say, so search and links can reach
  them without copying them.

### Writing it with an LLM

An LLM writes these documents well, given the right material. Give it:

1. **[docs/content-format.md](content-format.md)**, whole. It is the format, field by field.
2. **The sample pack's document of the same kind**, as an example: its `npcs.json` when you want
   people, its `level-1.json` when you want a floor.
3. **What the new document has to agree with**: your level file, when it writes quests that lead
   into its rooms; your `npcs.json`, when it writes rooms that people are in.
4. **Your own notes**: the rooms, the people, the plot, in whatever form you have them.

Ask for **one document at a time**, and save each one before asking for the next, so the next one
can link to it. For example:

> Here is the content format of DungeonTable (content-format.md), the sample pack's npcs.json as an
> example, and my level-1.json. Write npcs.json for my campaign, with the four people in my notes
> below. Link only to ids that exist in level-1.json or in this file, write the floor in every area
> link (`[[L1 area 2]]`), and answer with the JSON only.

Then save it, look at it on the DM screen, and run the content tests. Give the LLM what they report,
word for word, and ask it to fix exactly that. Two things LLMs like to do are harmless here: writing
`null` for a field they have nothing for reads as if they left it out, and a comment in the JSON is
allowed. What does not work is a field the format does not have: the app ignores it without a word,
and the content tests are what notice what is missing.

If your campaign is built from a published book, the text you give the LLM and the documents it
writes are that publisher's. Keep them in a private folder or repository, and serve them only behind
the passphrase ([content-format.md §8](content-format.md#8-content-that-is-not-yours)).

## 3. Troubleshooting

### The app will not start

- **`A compatible .NET SDK was not found`.** The app needs the .NET 10 SDK, 10.0.302 or later.
  `dotnet --list-sdks` shows which you have.
- **`Missing or blank required configuration "Auth:Passphrase"`.** The app refuses to start
  without a passphrase. Set one with `dotnet user-secrets set "Auth:Passphrase" "…" --project
  DungeonTable.Web`, as the environment variable `Auth__Passphrase`, or, with Docker Compose, as
  `AUTH_PASSPHRASE` in `.env`.
- **`Could not locate a 'data/dossiers' directory under Content:Root…`** (or `Maps`,
  `data/statblocks`, `data/art`, `data`). `Content:Root` has to be the folder that holds `Maps/` and
  `data/`, not one of them, and every one of those folders has to exist (see
  [step 1](#step-1-the-folder)). A relative path is read from the `DungeonTable.Web` folder, not from
  where you ran the command. With Docker Compose, check `CONTENT_DIR` in `.env`.
- **`set AUTH_PASSPHRASE in .env`** (or `POSTGRES_PASSWORD`) from `docker compose up`: copy
  `.env.example` to `.env` and fill it in.

### A page loads but does not respond

The page shows but nothing can be clicked. That happens when the address holds the passphrase
(`http://dm:secret@host/dm`): sign in through the browser's prompt at the plain address instead.
Otherwise the connection to the app dropped (the app stopped, or the laptop slept): reload the page.

### A tab looks short

A room, a person or a quest you wrote is not there, or a whole tab is empty. The app skipped
something it could not read. It says what in its log, as it starts and after every change:

```
Content problem in data/dossiers/npcs.json: it could not be read, so the app skipped it: …
```

and, for a file that was fine until an edit:

```
data/dossiers/npcs.json changed and cannot be read now, so the table keeps the content it had until the file is fixed and saved again: …
```

The usual causes:

- **The JSON does not parse**: a missing comma or quote, or a bracket not closed. The message says
  where. The table keeps showing what the file said before, so the problem can hide until the app
  restarts.
- **The file name is not one the app reads**: `npc.json` for `npcs.json`, or `Level-2.json` with a
  capital, which works on Windows and not on Linux, where Docker runs. Write names in lower case.
- **An entry has no id**, and is dropped. Or two entries share an id, and the later one wins.
- **A field name is misspelt.** The app ignores a field it does not know, so this one does not show
  in the log. The content tests notice the field that is missing.
- **A value the app does not know** where it expects one of a fixed set (a quest's `availability`,
  a block's `kind`) loses the whole document.
- **The book index folder was made while the app was running.** It is looked for only when the app
  starts: restart it.

The content tests report all of these, and more, file by file ([step 8](#step-8-check-it)).

### A link shows as broken

A link that leads nowhere is shown as its words, marked as broken. A muted one is not broken: it is
**pending**, a link to a floor that has no level file yet, and it comes alive when that file is
written. For a broken one:

- **Check the spelling against the id.** People, entities and book entries are matched by id, as a
  slug: lower case, apostrophes dropped, anything else that is not a letter or a digit turned into
  `-`. `[[Wenna Brask]]` finds `wenna-brask`; `[[Wenna]]` does not.
- **A room is matched by its key**, which comes from its title (`(Level 1, Area 3a)`) or its
  `areaNumber`. A room whose title has neither has no key.
- **Outside a level file, write the floor**: `[[L1 area 3a]]`, not `[[area 3a]]`.
- **A name that could mean two things links to neither.** Make it unique.
- **The target is not written, or was skipped.** If it is written, see
  [a tab looks short](#a-tab-looks-short).

The content tests name every broken link, with the file it is in.

### The players see nothing, or the wrong thing

- **Nothing shows until you reveal it.** The bar says what the players can see: **Players see 0
  regions · 0 cells** means nothing yet.
- **The projector shows only what is inside the Player view box** on the map. Press **Frame** to
  bring it to the room on the Room tab.
- **The projector follows the map picked on the DM screen.**
- **Dragging the party reveals nothing**: the map has no calibrated grid (Measure says so too), so
  there are no squares to see from. Reveal its rooms with **Reveal** instead.
- **A room cannot be revealed**, or is revealed but does not show: it has no region, or its region
  is not linked to it. Open the Room Editor and check the room's region says **linked**.
- **A map whose `.regions.json` does not parse** shows nothing on the projector, rather than risk
  showing a secret. The DM screen says so. Fix the file by hand: the Room Editor will not save over a
  file it cannot read.
- **Everything was gone after a restart**: the app has no database, and says **Not saved** (see
  [what "Not saved" means](#what-not-saved-means)).

---

_The screenshots show the sample pack's map. Maps created with Dungeon Scrawl
(https://dungeonscrawl.com) — used under CC BY 4.0 license._
