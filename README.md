# DungeonTable

A DM screen and a projector view for running a dungeon at the table. The DM works from a browser:
the map, the room they are in, the people and the quests, and the fight. The players see the map on a
projector, revealed room by room as they explore it.

DungeonTable holds no adventure of its own. It reads one from a **content root**: a folder of maps
drawn in [Dungeon Scrawl](https://www.dungeonscrawl.com) and JSON documents that describe the rooms,
people, quests and monsters. This repository ships one small original adventure,
[The Silent Bell](samples/demo/README.md), which shows every kind of document by example.

![The DM screen on the sample adventure: its map with three rooms revealed, the map tools on the left, and the box that frames what the projector shows](docs/images/dm-map.png)

## What it does

- **The map, on the projector.** Vector maps from Dungeon Scrawl, with fog of war the DM lifts room by
  room or brush by brush, doors to open, secrets to conceal, a measuring tool, and a pointer the players
  see (`/player`, meant to run full screen on the projector).
- **The room briefing.** Click a room and the Info tab shows what the DM needs: the read-aloud text, who
  is there and where it leads, the DM-only notes, and links to every person, monster, spell and place
  it mentions, each opening a card of its own.
- **The campaign around it:** the level and its factions, the quest log with tickable story beats, the
  party, the NPCs, the backstory, the evening's session prep, the rules, card decks to draw from, and a
  search across all of it and the books the campaign indexes.
- **The fight.** Start a battle from the room: the creatures it lists join the party in an initiative
  order, with hit points, conditions, and each monster's full stat block and spells a click away.
- **Pictures on the projector**: the campaign's art, or anything the DM uploads, framed over the map.
- **The Room Editor** (`/editor`): mark which area each room is, and where the doors, secrets, traps
  and features are, over the Dungeon Scrawl map.
- **It keeps the table.** With PostgreSQL configured, what was revealed, the running fight and the
  quest log survive a restart. Without it the app still runs, and says on the DM screen that nothing is
  being saved.

## Quick start

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), 10.0.302 or later. The app refuses
to start without a passphrase (see [Security](#security)), so set one first:

```bash
dotnet user-secrets set "Auth:Passphrase" "choose-a-passphrase" --project DungeonTable.Web
dotnet run --project DungeonTable.Web
```

Open the address it prints, sign in with any user name and the passphrase, and go to `/dm`. In
Development the app reads the sample pack in `samples/demo`. Open `/player` in a second window to see
what the projector shows.

**The [guide](docs/guide.md) says how to use it at the table**: the projector, the map's tools, a
fight, pictures. It also covers writing a campaign of your own, by hand or with an LLM, and what to
do when something does not show.

### With Docker

```bash
cp .env.example .env    # then fill in AUTH_PASSPHRASE and POSTGRES_PASSWORD
docker compose up --build
```

This runs the app on the sample pack with a PostgreSQL database beside it, at
<http://localhost:8080/dm>. Set `CONTENT_DIR` in `.env` to run your own campaign instead.

## Your own campaign

Copy `samples/demo` somewhere outside this repository and change it, one document at a time, or start
from an empty folder as the [guide](docs/guide.md#2-writing-your-own-campaign) does. The format is
described in [docs/content-format.md](docs/content-format.md), and the sample pack's
[README](samples/demo/README.md) says which file holds what. Then point the app at it:

```bash
dotnet user-secrets set "Content:Root" "/path/to/your/campaign" --project DungeonTable.Web
```

Edit a document while the app runs and the open pages show the change a moment after you save it, with
no restart. A file you save half-finished does not take anything away: the table keeps what it had
until the file reads again.

The app is forgiving by design: a document it cannot read is skipped, so a typo never takes the table
down. So that a mistake does not go unnoticed, the app says in its log as it starts which documents it
skipped and which entries it dropped, file by file. The **content tests** go further: they check a
campaign the way the app reads it, and name every document that did not load and every link, exit,
quest destination or map region that leads nowhere:

```bash
DUNGEONTABLE_CONTENT_ROOT=/path/to/your/campaign dotnet test DungeonTable.ContentTests
```

In PowerShell, set the variable first: `$env:DUNGEONTABLE_CONTENT_ROOT = "C:\path\to\your\campaign"`.

## Configuration

Settings are read from `appsettings.json`, user-secrets in Development, environment variables (with
`__` for `:`, as in `Auth__Passphrase`) and the command line (`--Content:Root=...`).

| setting | what it does |
|---|---|
| `Auth:Passphrase` | **Required.** The shared passphrase for the whole app. Blank counts as missing. |
| `Content:Root` | The content root: the folder that holds `Maps/` and `data/`. A relative path is read from the web project's folder. Development sets it to the sample pack. |
| `Maps:Root`, `Dossiers:Root`, `Stats:Root`, `Art:Root`, `Rosters:Root`, `BookIndex:Root` | One kind of content from somewhere else, overriding its place under `Content:Root`. |
| `ConnectionStrings:Postgres` | A PostgreSQL connection string. With it, the table is saved; without it, nothing is. |
| `Session:Id` | The key the table is saved under. Defaults to `table`. |
| `PathBase` | Also serve the app below a path, such as `/dnd`. It must start with `/`. |
| `Maps:Durable` | Set `false` where the maps folder is not kept across restarts, such as inside an image, so the Room Editor warns that a save will not last. |
| `Content:Watch` | Read a file again when it changes, so an edit shows without a restart. On unless set to `false`. |
| `DOTNET_USE_POLLING_FILE_WATCHER` | An environment variable: `true` makes the app look for changed files every few seconds instead of waiting to be told, which a folder mounted into a container from a Windows or macOS host needs. The compose files set it. |

With no `Content:Root`, each kind of content is looked for by walking up from the app's folder, which
is how the Docker image finds content in `/app/Maps` and `/app/data`. Every kind is required except the
book index, and the app fails at startup, naming the setting, when one is missing.

## Deploying

The [Dockerfile](Dockerfile) builds the app with no content in it:

```bash
docker build -t dungeontable-app .
```

Each release is also published as `ghcr.io/gemberkoekje/dungeontable-app:<version>`, such as `1.0.0`.

Either mount a campaign into the container, as `docker-compose.yml` does, or build an image of your own
on top of it that copies the campaign in. That takes a `Dockerfile` in the campaign's folder:

```dockerfile
FROM dungeontable-app
COPY Maps ./Maps
COPY data ./data
```

and beside it a `.dockerignore` that keeps out everything the app does not read:

```text
*
!Maps
!data
```

Wherever it runs, keep these in mind:

- Set `Auth__Passphrase`, and put the app behind HTTPS: the passphrase travels with every request.
- The app writes to its content at run time. Pictures uploaded on the Art tab go to
  `data/art/uploads/`, and the Room Editor saves `Maps/**/*.regions.json`. Put a volume on the uploads
  folder if they should outlive the container, and set `Maps:Durable=false` if the maps folder does not
  survive a restart.
- The party and ally rosters are edited in the app. With a database they are kept there, seeded once
  from `data/party.json` and `data/allies.json`; without one they are written back to those files.
- `/healthz` answers without the passphrase, for a liveness probe.

## Security

The whole app sits behind one shared passphrase (HTTP Basic auth, any user name). A campaign is often
built from published books, and the maps, stat blocks and read-aloud text it serves are usually
someone else's copyright: serving them to anyone who finds the URL is the risk the passphrase is
there for. Only `/healthz` is exempt.

It is not meant to keep secrets from the players at the table. They share the passphrase, and the
player view is a projector, not something they browse on their own devices.

To report a vulnerability, see [SECURITY.md](SECURITY.md).

## Development

```bash
dotnet build DungeonTable.slnx
dotnet test DungeonTable.slnx
```

The tests need no database and no content but the sample pack; the PostgreSQL tests are skipped unless
`DUNGEONTABLE_TEST_POSTGRES` holds a connection string. Warnings are errors, and the analysers are
strict.

The package lock files are committed, and CI restores against them with SDK 10.0.302, the one
[global.json](global.json) names. A newer .NET 10 SDK builds the app too, and leaves the lock files as
they are: the one package that follows the SDK's own version is pinned in
`DungeonTable.Web/DungeonTable.Web.csproj`.

To release, set `<Version>` in `DungeonTable.Web/DungeonTable.Web.csproj`, commit, and push a tag
`v<version>` for that commit. [release.yml](.github/workflows/release.yml) runs the tests and publishes
the image.

| project | what it holds |
|---|---|
| `DungeonTable.Core` | The domain: maps, dossiers, stat blocks, the battle, the saved table. No dependencies. |
| `DungeonTable.Application` | The ports: the interfaces the web app uses to reach content and storage. |
| `DungeonTable.Infrastructure` | The adapters: file-system stores for the content, links, and PostgreSQL through Marten. |
| `DungeonTable.Web` | The Blazor Server app and its composition root, `Program.cs`. |
| `DungeonTable.Tests` | The tests, on the sample pack. |
| `DungeonTable.ContentTests` | The content rules, run against the sample pack or any campaign. |

[docs/architecture.md](docs/architecture.md) explains how the pieces fit, and [CLAUDE.md](CLAUDE.md)
holds the house style and the traps worth knowing before changing anything.

## Licence

[MIT](LICENSE), except for two things in the sample pack, which its
[README](samples/demo/README.md#credits-and-licences) credits in full:

- It includes material from the System Reference Document 5.1 by Wizards of the Coast, under the
  Creative Commons Attribution 4.0 International licence.
- Its map, and the screenshots of it in [docs/images](docs/images): Maps created with Dungeon Scrawl
  (https://dungeonscrawl.com) — used under CC BY 4.0 license.

The app uses SixLabors.ImageSharp under the Apache License 2.0, and other open-source packages under
their own licences.
