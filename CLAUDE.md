# DungeonTable — notes for working on the app

_To understand this repository, start with [README.md](README.md), then
[docs/architecture.md](docs/architecture.md) for how the app is put together,
[docs/content-format.md](docs/content-format.md) for the documents it reads, and
[docs/guide.md](docs/guide.md) for how it is used at the table._

## What this is

A Blazor Server app for running a dungeon at the table: a DM screen (`/dm`), a player view for a
projector (`/player`) and a Room Editor (`/editor`). The app holds no adventure. It reads one from a
**content root**, a folder with `Maps/` and `data/`, and the only content in this repository is the
sample pack in `samples/demo`, a small original adventure that is also the test fixture.

## Real content stays out of this repository

A campaign run with this app is usually built from published books: maps, stat blocks, read-aloud
text and art that are someone else's copyright. None of it belongs here.

- **Tests never use real content.** Fixtures come from `samples/demo`, or are built in the test. The
  content tests (`DungeonTable.ContentTests`) can check any campaign through
  `DUNGEONTABLE_CONTENT_ROOT`, but only the sample pack runs in this repository's CI.
- **Examples in comments and docs use the sample pack's names** (Grask, Nib, the Bell-Warden, Wenna
  Brask, the Goblin Den), or no example at all.
- When a session has a real campaign open beside this repository, keep that content out of the diff:
  no names, prose, ids or file paths from it.

## Deployment and threat model

- **The app is meant to be served at a URL**, not only on the DM's own machine: the player view runs
  in the browser tab that drives the projector, and that tab is not sandboxed to a local network.
- **So the whole app sits behind one shared passphrase** (HTTP Basic auth, any user name; see
  `DungeonTable.Web/Auth/PassphraseAuthMiddleware.cs`), supplied as `Auth:Passphrase` (environment
  variable `Auth__Passphrase`), never hardcoded or committed. The app refuses to start without one, and
  a blank one counts as none. The point is not hiding secret doors from the players; it is that the
  content served under the URL is often copyrighted, and serving it publicly and discoverably is the
  real risk. Only `/healthz` (a liveness probe) is exempt.
- **Hidden information reaching the player view's DOM is not a security defect.** Past the passphrase
  only the people at the table have the URL, and the player view is shown on a projector, not browsed
  on players' own devices. Emitting the whole map and masking it for the projector is fine; weigh DOM
  leakage as cosmetic or tech debt at most. What the players actually **see** on the projector (a
  secret door drawn as a plain door, a revealed room not showing) must be right.
- The `/art` route serves raster images only, never the catalogue or an SVG: an SVG served
  same-origin is markup that can run script. Uploads are decoded and re-encoded to WebP, never stored
  as they arrived.

## House style

The build enforces most of this: warnings are errors.

- **No nullable annotations.** `<Nullable>disable</Nullable>`, and no `?` on reference types. Do not add
  `#nullable enable` to silence CS8632; remove the `?`. Use an empty string or an empty list for "none",
  or `Result<T>` where absence is an outcome.
- **Implicit usings are off.** System and SDK usings go in each project's `GlobalUsings.cs` or at the top
  of the file.
- **Every enum has an empty value** (`None`), first.
- **Expected failures are Qowaiv `Result` / `Result<T>`**, not exceptions: invalid input, not found, a
  refused upload. Use the non-generic `Result` (`Result.OK`, `Result.WithMessages(...)`) when nothing is
  returned, and check `IsValid` before reading `Result.Value`, which throws on an invalid result.
  Exceptions are for programmer errors and misconfiguration, such as a missing content root at
  startup, which should fail loudly and name the setting.
- **Content is live.** The documents are read into one `ContentSnapshot` that `LiveContent` swaps when a
  file changes on disk; the stores the pages inject are facades over it. Never keep a store's answer
  past a render without listening to `LiveContent.Changed` (see `DmWorkspace.RefreshAsync`), or a page
  shows old content after an edit.
- **The content stores are fail-soft**: a document that does not parse is skipped, and an entry with no
  id is dropped, so one typo never takes the table down. Each store records what it skipped in its
  `Problems`, which `ContentReport` logs at startup; the content tests report it in full. A `null`
  anywhere in a document reads as if the field were left out (`NullMeansLeftOut` and the lenient
  converters, where the JSON enters), so never check a content field for `null` downstream.
- **Layers:** `Core` (domain types, no dependencies) ← `Application` (ports: `I`-prefixed interfaces)
  ← `Infrastructure` (adapters, named for their technology: `FileSystem…`, `Marten…`) ← `Web` (the only
  composition root). Never reference upwards.
- **Composition:** register services inline in `Program.cs`, Singleton or Scoped only (never Transient).
  Register the concrete type, then the interface as a factory over it, so both resolve to one instance.
- **Configuration** is read with the colon-path indexer (`Configuration["Content:Root"]`), not
  `IOptions<T>`. A required setting is checked where it is read and fails naming the key.
- **Logging:** `ILogger<T>` for the containing class, structured templates with PascalCase
  placeholders, the exception as the first argument. `Core` does not log.
- **Documentation:** XML doc comments on public APIs; one public type per file, named after it;
  file-scoped namespaces.
- **Packages:** every version once in `Directory.Packages.props` (central package management), none in
  a `.csproj`. Lock files are committed; CI restores in locked mode. The one exception is the SDK's
  implicit `Microsoft.AspNetCore.App.Internal.Assets`, pinned in `DungeonTable.Web.csproj` so a newer
  SDK writes the same lock files (the comment there says why it cannot live in the props file).
- **Analyzers:** the .NET analyzers (Recommended), SonarAnalyzer and DotNetProjectFile.Analyzers. Tune a
  severity only in `.globalconfig`, with a `# Title` line and a `[Justification: ...]`.
- **Formatting:** four spaces, no tabs, no trailing whitespace, no blank line at the end of a file. The
  repository stores LF line endings.

## Traps

Each of these compiled, passed every test, and still broke something.

- **A Razor comment (`@* *@`) among an element's attributes** compiles and takes the whole DM screen's
  circuit down at render time. Put comments between elements.
- **A string component parameter written without `@`** is bound as the literal text: `Value="name"`
  passes the word "name".
- **`@{ }` inside a Razor `else` body is RZ1010.** Declare the variables bare there.
- **Blazor decides `preventDefault` when it renders**, not when the key arrives, so a key that is
  sometimes swallowed (Tab in the Battle tab's initiative column) needs JavaScript
  (`wwwroot/dt-battle.js`).
- **CSS specificity in `app.css`:** the shared button rules use `:not()`, which counts as a class. A
  component's own button rule has to match that specificity and come later, or it silently loses; the
  comment above the Battle tab's small controls explains it.
- **The static router never raises `NavigationLock.OnBeforeInternalNavigation`**, so the Room Editor's
  unsaved-changes guard intercepts the layout's links itself.
- **Credentials in the URL** (`http://dm:pass@host/dm`) authenticate the page but break the Blazor
  circuit: the page prerenders and then does nothing. When driving the app from a script or a test
  browser, sign in once with credentials, then load the plain URL.
- **`HtmlRenderer` renders statically**: nothing can be clicked, and only a panel's active tab renders.
  A part that has to be tested on its own becomes a component of its own (`LevelTable`, `DeckPanel`),
  or the panel takes the tab to open on (`RoomBriefingPanel.InitialTab`).
- **A duplicate `@key` passes a first render.** Blazor compares keys only when it diffs a list against
  the one it rendered before, so two siblings with one key throw on the next render, which a live tab
  does at its next change. A render test of keys must render twice (`RenderAgainHost` in the tests).
  Key authored lists with `RenderKeys`, which stays unique when the ids are not.
- **An exception thrown while the host starts** (before or after `builder.Build()`) surfaces from
  `WebApplicationFactory.CreateClient()` as itself, not wrapped.
- **NTFS lists a folder case-insensitively.** A test of ordinal file ordering passes on Windows by
  accident unless its names sort differently by case (`monsters-alpha`, `monsters-Zeta`).
- **Sonar rules that fail the build and surprise:** a comment line ending in `;` reads as commented-out
  code (S125); a local that shares a field's name (S1117); nested `if`s that could merge (S1066); a
  comparison it can prove constant (S3981); a ternary with equal branches (S3923); a nested ternary
  (S3358). CA1861 (a constant array argument) and CA1859 (a private method returning an interface it
  builds as an array) fail test code too, and a theory's parameters must be public types (CS0051).
- **`dotnet test --no-build` tests whatever was built last**, including a mutated build, or the old
  binaries when the last build failed.
- **A running app holds `bin/` open on Windows.** Build elsewhere with
  `dotnet build --artifacts-path artifacts`.

## Working method

- **Test new behaviour, and check the test can fail**: break the code it covers and watch it go red.
  This project mutation-checks its tests as a habit, because a test that cannot fail proves nothing.
- **Drive the app after a UI change.** Several bugs here passed every test and only showed in a
  browser. Run it on the sample pack:
  `dotnet user-secrets set "Auth:Passphrase" "<anything>" --project DungeonTable.Web`, then
  `dotnet run --project DungeonTable.Web`. In Development it reads `samples/demo` unless `Content:Root`
  says otherwise.
- **A change to the content format** changes the code, the sample pack, `docs/content-format.md` and
  the content rules together, so that the pack still shows every kind of document by example.
- **Where the reasoning behind a design matters, write it in `docs/architecture.md`**, not only in a
  comment or a commit message.
