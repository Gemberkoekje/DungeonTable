# The Silent Bell: a sample content pack

A small, complete adventure for DungeonTable: five areas under a village chapel, the people above them,
three quests, two card decks, and the whole System Reference Document 5.1 bestiary and spell list. It
has three jobs:

- **It shows the content format by example.** Every kind of document the app loads is here once, small
  enough to read in a sitting. Copy it to start your own campaign.
- **It runs.** Point the app at this folder and you have a table to click around.
- **It is the test fixture.** The app's tests run against it, so everything here is checked.

## The adventure

The bell of Millbrook's chapel has not rung for ten days. Tam, the miller's son, went down into the
undercroft to find out why and has not come back. His mother, Wenna Brask, asks the party to find him.

Underneath: Brother Aldous, the chapel's acolyte, lost forty gold to Grask at dice and paid part of it
with the bell's silver clapper. Grask, a bugbear, is melting it down in the old bell-founder's workshop,
and keeps Tam chained there because Tam saw it. Nib's goblins run Grask's errands and would sell him out
for their den. A gargoyle, the Bell-Warden, guards the nave, and since the bell fell silent it guards it
against everyone. Below it all, the ringers' stair leads down to a sealed crypt: Level 2, which is
deliberately never written, so you can see how the app treats a floor that isn't authored yet.

## Running it

From the repository root, set a passphrase once (the app refuses to start without one), then run the
app with `Content:Root` pointing here:

```bash
dotnet user-secrets set "Auth:Passphrase" "any-passphrase-you-like" --project DungeonTable.Web
dotnet run --project DungeonTable.Web -- --Content:Root=../samples/demo
```

A relative `Content:Root` is resolved from the web project's folder, hence the `../`. Open the address
it prints, sign in with any user name and the passphrase, and go to `/dm`. The map is under `/editor`
too, where its regions, doors and markers were made.

## What is where

| file | what it holds |
|---|---|
| `Maps/demo/level-1-undercroft.ds` | The map, drawn in Dungeon Scrawl. The folder name is the adventure; the file name is the map id. |
| `Maps/demo/level-1-undercroft.regions.json` | What the Room Editor added: which area each room is, the double, open and secret doors, the concealed chest and pit, and a trap and hazard marker. |
| `data/dossiers/level-1.json` | The floor: seven areas (area 3 is an overview that 3a and 3b fill), who is in each and where it leads, the floor's people, factions and items, and how they relate. |
| `data/dossiers/campaign.json` | The background, where the DM screen opens, and the table of levels. |
| `data/dossiers/reference.json` | The rules the Rules tab shows. |
| `data/dossiers/quests.json` | Three quests: one open from the start, one found below, one gated. |
| `data/dossiers/npcs.json` | The people of Millbrook. Brother Aldous fights with the acolyte's stat block. |
| `data/dossiers/characters.json` | The player characters, as the DM needs them. |
| `data/dossiers/sessions.json` | One session's prep, scene by scene. |
| `data/dossiers/story.json` | The backstory, with its cast. |
| `data/dossiers/relations.json` | The ties that belong to no single floor. |
| `data/dossiers/deck-*.json` | Two decks: rumours the players keep, and omens drawn fresh every time. |
| `data/party.json`, `data/allies.json` | The battle rosters. The app edits these; the numbers match `characters.json`. |
| `data/statblocks/monsters-srd.json`, `spells-srd.json` | Every monster and spell in SRD 5.1, converted from the SRD itself. |
| `data/art/catalogue.json`, `data/art/demo/` | Two pictures for the projector. |
| `data/book-index/almanac.json`, `srd.json` | What search and links can find in the campaign's own book and in the SRD. |

Prose links with `[[target]]` or `[[target|shown text]]`. A target is an area on this floor
(`[[area 3a]]`) or another (`[[L2 area 1]]`), a person or thing by its id (`[[grask]]`,
`[[bell-warden]]`), a stat block or spell by name (`[[bugbear]]`, `[[sacred-flame]]`), or an entry
in the book index by its id (`[[the-old-mill]]`). The whole format is described in
[docs/content-format.md](../../docs/content-format.md).

## Starting your own campaign

Copy this folder and replace what is in it, one document at a time; the app shows whatever is there.
The [guide](../../docs/guide.md#2-writing-your-own-campaign) walks through it from an empty folder.
Keep the two `*-srd.json` files and put your own monsters in a `monsters.json` beside them, and your own
spells in a `spells.json`. Every `monsters*.json` and `spells*.json` is read, and where two share a
node id, the file whose name sorts later wins, so `monsters.json` overrides `monsters-srd.json`. Keep
`data/book-index/srd.json` too: it names the book the SRD files come from, so their stat blocks cite
"SRD 5.1", and the content tests expect every source to be a book the book index names.

Check your campaign with the content tests as you go. The app skips a document it cannot read, and
drops an entry with no id, without a word, so a typo just leaves a tab short. The content tests say what
was skipped, and name every link, exit, quest destination and map region that leads nowhere, file by
file. From the repository root:

```bash
DUNGEONTABLE_CONTENT_ROOT=/path/to/your/campaign dotnet test DungeonTable.ContentTests
```

Point the variable at the folder that holds your `Maps/` and `data/`; without it, the tests check this
pack. In PowerShell, set it first with `$env:DUNGEONTABLE_CONTENT_ROOT = "C:\path\to\your\campaign"`.

## Credits and licences

This work includes material taken from the System Reference Document 5.1 ("SRD 5.1") by Wizards of
the Coast LLC and available at https://dnd.wizards.com/resources/systems-reference-document. The
SRD 5.1 is licensed under the Creative Commons Attribution 4.0 International License available at
https://creativecommons.org/licenses/by/4.0/legalcode.

The SRD material is `data/statblocks/monsters-srd.json` and `spells-srd.json`, and the three rules
entries in `data/book-index/srd.json`, which summarise the SRD in their own words.

The map, `Maps/demo/level-1-undercroft.ds`, was drawn with Dungeon Scrawl, whose
[commercial use policy](https://help.roll20.net/hc/en-us/articles/36165245156247-Commercial-Use-Policy)
requires this attribution in any published work that uses a map made with it:

> Maps created with Dungeon Scrawl (https://dungeonscrawl.com) — used under CC BY 4.0 license.

So the map is under CC BY 4.0, not MIT, and so are the screenshots of it in the app's `docs/images`.
It uses none of Dungeon Scrawl's library images: its four sprites (the statue, the chest, the pit and
the furnace) were drawn for this pack.

Everything else here is original to this pack: the adventure and its prose, the sprites and the two
pictures. It is released under the same MIT licence as the rest of the repository.
