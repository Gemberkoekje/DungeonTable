using DungeonTable.Application.Abstractions;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Infrastructure.Sessions;
using DungeonTable.Infrastructure.Stats;
using DungeonTable.Web.Auth;
using DungeonTable.Web.Components;
using DungeonTable.Web.Services;
using Marten;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Where the content is. Each of the six roots below is found by the first of: its own key
// (Maps:Root, Dossiers:Root, ...); its default place under Content:Root, one folder holding all the
// content in its usual layout (Maps/, data/); or walking up from ASP.NET's own content root, which is
// how a checkout finds the content sitting beside the app. Only the book index may be missing.
string contentRoot = builder.Configuration["Content:Root"] ?? string.Empty;
string startDirectory = builder.Environment.ContentRootPath;

// Locate the maps root once at startup (fail fast if it is missing).
string mapsRoot = MapFileLocator.Locate(startDirectory, builder.Configuration["Maps:Root"], contentRoot);
builder.Services.AddSingleton<FileSystemMapStore>(_ => new FileSystemMapStore(mapsRoot));
builder.Services.AddSingleton<IMapStore>(sp => sp.GetRequiredService<FileSystemMapStore>());

// Fully-vector maps: the Dungeon Scrawl reader parses a .ds source into a VectorMap, and the
// file-system store lists/loads them from the maps root.
builder.Services.AddSingleton<DungeonScrawlMapReader>();
builder.Services.AddSingleton<IVectorMapReader>(sp => sp.GetRequiredService<DungeonScrawlMapReader>());
builder.Services.AddSingleton<FileSystemVectorMapStore>(sp =>
    new FileSystemVectorMapStore(mapsRoot, sp.GetRequiredService<IVectorMapReader>()));
builder.Services.AddSingleton<IVectorMapStore>(sp => sp.GetRequiredService<FileSystemVectorMapStore>());

// The documents: the dossiers (data/dossiers), the stat blocks (data/statblocks) and the book index
// (data/book-index, optional: a campaign without one searches only its own content). The first two
// folders are required, and startup fails naming the setting when one is missing.
string dossiersRoot = DossierFileLocator.Locate(startDirectory, builder.Configuration["Dossiers:Root"], contentRoot);
string statsRoot = StatFileLocator.Locate(startDirectory, builder.Configuration["Stats:Root"], contentRoot);
string bookIndexRoot = BookIndexFileLocator.Locate(startDirectory, builder.Configuration["BookIndex:Root"], contentRoot);

// They are read into one snapshot, together with everything built from them: the link index, the
// projection behind briefings, cards and search, and the prose link resolver. LiveContent holds the
// snapshot and reads a new one when a document changes on disk (ContentWatcher, below); the stores the
// pages inject hand every call on to the current one, so an edit reaches every page without a restart.
builder.Services.AddSingleton<LiveContent>(_ => new LiveContent(dossiersRoot, statsRoot, bookIndexRoot));
builder.Services.AddSingleton<LiveDossierStore>(sp => new LiveDossierStore(sp.GetRequiredService<LiveContent>()));
builder.Services.AddSingleton<IDossierStore>(sp => sp.GetRequiredService<LiveDossierStore>());
builder.Services.AddSingleton<LiveStatLibrary>(sp => new LiveStatLibrary(sp.GetRequiredService<LiveContent>()));
builder.Services.AddSingleton<IStatLibrary>(sp => sp.GetRequiredService<LiveStatLibrary>());
builder.Services.AddSingleton<LiveBookIndexStore>(sp => new LiveBookIndexStore(sp.GetRequiredService<LiveContent>()));
builder.Services.AddSingleton<IBookIndexStore>(sp => sp.GetRequiredService<LiveBookIndexStore>());

// Briefings, reference cards and search, from the authored content (an area's creatures and exits, a
// level's entities, the people, the stat blocks) with the book index below it for what the books say.
builder.Services.AddSingleton<LiveContentProjection>(sp => new LiveContentProjection(sp.GetRequiredService<LiveContent>()));
builder.Services.AddSingleton<IContentProjection>(sp => sp.GetRequiredService<LiveContentProjection>());

// The cross-reference resolver that turns dossier prose into clickable links: the links authors
// wrote ("[[bandit|human bandits]]", "[[area 6d]]"). Unmarked prose stays plain text.
builder.Services.AddSingleton<LiveCrossRefResolver>(sp => new LiveCrossRefResolver(sp.GetRequiredService<LiveContent>()));
builder.Services.AddSingleton<ICrossRefResolver>(sp => sp.GetRequiredService<LiveCrossRefResolver>());

// The pictures the DM can push onto the projector: the curated art committed under
// data/art plus the DM's own uploads beside it. Like the rosters this one is also written to, but
// unlike them it keeps an in-memory index, so the adapter refreshes that itself on every upload.
string artRoot = ArtFileLocator.Locate(startDirectory, builder.Configuration["Art:Root"], contentRoot);
builder.Services.AddSingleton<FileSystemArtCatalogue>(_ => new FileSystemArtCatalogue(artRoot));
builder.Services.AddSingleton<IArtCatalogue>(sp => sp.GetRequiredService<FileSystemArtCatalogue>());

// The saved party and ally rosters. Unlike the stores above these are also written to (from the
// in-app editors), so the adapters re-read on every Get rather than caching at startup.
//
// Only the CONCRETE file adapters are registered here. Which one answers IPartyRoster/IAllyRoster is
// decided with the session store below: with a database the rosters live in Postgres like the rest
// of the live table, and these two become the one-time seed for it. Without one they are the store,
// exactly as before.
string rostersRoot = RosterFileLocator.Locate(startDirectory, builder.Configuration["Rosters:Root"], contentRoot);
builder.Services.AddSingleton<FileSystemPartyRoster>(_ => new FileSystemPartyRoster(rostersRoot));
builder.Services.AddSingleton<FileSystemAllyRoster>(_ => new FileSystemAllyRoster(rostersRoot));

// Shared DM/player session state (one live table): the player projector's viewport + aspect.
builder.Services.AddSingleton<SessionState>();

// The one live fight. Singleton, not scoped: a browser refresh mid-combat must not lose it. It is
// never injected into the player view — the projector shows the map, the fight is on the table.
builder.Services.AddSingleton<BattleState>();

// How far the campaign's quests have got, and which cards each deck has dealt. Singleton beside
// the fight and for the same reason: a browser refresh must not lose a session's worth of ticks.
builder.Services.AddSingleton<CampaignState>();

// Works out an area's encounter from the creatures it lists; stateless, so a singleton.
builder.Services.AddSingleton<EncounterSource>();

// Finds what an area's prose says about one creature in the fight, for the combatant detail pane's
// encounter context. Stateless, so a singleton.
builder.Services.AddSingleton<EncounterContext>();

// Per-circuit DM state (the loaded map, its annotations, the shown briefing and the reference
// history), shared by the DM screen's Map / Info / Battle tabs. Scoped, so one DM browser gets one
// workspace and a second tab of the app does not inherit the first one's navigation.
builder.Services.AddScoped<DmWorkspace>();

// Persistence. The reveal state and the running fight are one document per table in Postgres, so
// a deploy, a crash or a laptop reboot mid-combat does not cost the evening.
//
// It is gated on the connection string being present, deliberately: local development and the test
// suite must not need a database, exactly as the map/dossier/stat stores are files rather than tables.
// That gate is not allowed to be silent, though — the ephemeral store reports itself as
// non-persistent, the warning below names the missing key, and the DM screen's status bar says "not
// saved" all session. A DM discovering after a restart that nothing was ever being saved is the one
// outcome worth being noisy about.
string sessionId = builder.Configuration["Session:Id"] ?? "table";
string postgres = builder.Configuration.GetConnectionString("Postgres") ?? string.Empty;
if (postgres.Length > 0)
{
    builder.Services.AddMarten(options => SessionDocuments.Configure(options, postgres));
    builder.Services.AddSingleton<MartenSessionStore>(sp =>
        new MartenSessionStore(sp.GetRequiredService<IDocumentStore>()));
    builder.Services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<MartenSessionStore>());

    // The rosters go with it. They are live table data edited in the app, and on the cluster the
    // container's file system is its own image layer - a party edited at the table was silently
    // thrown away by the next restart. Seeded once from the committed JSON; after that the document
    // wins and editing data/party.json in the repository no longer reaches a deployed table.
    builder.Services.AddSingleton<MartenPartyRoster>(sp => new MartenPartyRoster(
        sp.GetRequiredService<IDocumentStore>(),
        sp.GetRequiredService<FileSystemPartyRoster>(),
        sessionId));
    builder.Services.AddSingleton<IPartyRoster>(sp => sp.GetRequiredService<MartenPartyRoster>());
    builder.Services.AddSingleton<MartenAllyRoster>(sp => new MartenAllyRoster(
        sp.GetRequiredService<IDocumentStore>(),
        sp.GetRequiredService<FileSystemAllyRoster>(),
        sessionId));
    builder.Services.AddSingleton<IAllyRoster>(sp => sp.GetRequiredService<MartenAllyRoster>());
}
else
{
    builder.Services.AddSingleton<EphemeralSessionStore>();
    builder.Services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<EphemeralSessionStore>());

    // No database: the JSON files on disk are the rosters.
    builder.Services.AddSingleton<IPartyRoster>(sp => sp.GetRequiredService<FileSystemPartyRoster>());
    builder.Services.AddSingleton<IAllyRoster>(sp => sp.GetRequiredService<FileSystemAllyRoster>());
}

// The coordinator: restores the table before the first request (its Starting phase runs ahead of
// every other hosted service, Kestrel included) and writes changes back on a debounce. Registered
// twice on purpose so the DM screen can read its save status from the same instance the host runs.
builder.Services.AddSingleton<TablePersistence>(sp => new TablePersistence(
    sp.GetRequiredService<ISessionStore>(),
    sp.GetRequiredService<SessionState>(),
    sp.GetRequiredService<BattleState>(),
    sp.GetRequiredService<CampaignState>(),
    sessionId,
    sp.GetRequiredService<ILogger<TablePersistence>>()));
builder.Services.AddHostedService(sp => sp.GetRequiredService<TablePersistence>());

// What the content amounts to, and everything the stores skipped or dropped reading it (a document
// that does not parse, an entry with no id, a file under a name nothing reads), in the log as the app
// starts. The stores are fail-soft, so without this a typo just leaves a tab short, and the content
// tests that would say why need the .NET SDK. A file is named from the content root when it is set.
string reportRoot = contentRoot.Length > 0 ? System.IO.Path.GetFullPath(contentRoot, startDirectory) : string.Empty;
builder.Services.AddSingleton<ContentReport>(sp => new ContentReport(
    sp.GetRequiredService<LiveContent>(),
    sp.GetRequiredService<FileSystemArtCatalogue>(),
    sp.GetRequiredService<IMapStore>(),
    sp.GetRequiredService<IVectorMapStore>(),
    sp.GetRequiredService<FileSystemPartyRoster>(),
    sp.GetRequiredService<FileSystemAllyRoster>(),
    reportRoot,
    sp.GetRequiredService<ILogger<ContentReport>>()));
builder.Services.AddHostedService(sp => sp.GetRequiredService<ContentReport>());

// Live reload: a document, a map or the art catalogue changed on disk is read again, so an edit made by
// hand or by an LLM shows at the table without a restart. Content:Watch=false turns it off.
if (builder.Configuration.GetValue("Content:Watch", true))
{
    builder.Services.AddSingleton<ContentWatcher>(sp => new ContentWatcher(
        sp.GetRequiredService<LiveContent>(),
        sp.GetRequiredService<FileSystemArtCatalogue>(),
        sp.GetRequiredService<ContentReport>(),
        ContentWatcher.Folders(dossiersRoot, statsRoot, bookIndexRoot, mapsRoot, artRoot),
        TimeSpan.FromMilliseconds(400),
        sp.GetRequiredService<ILogger<ContentWatcher>>()));
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ContentWatcher>());
}

// The app is served at a public URL and a campaign's maps and stat blocks are usually a publisher's
// copyrighted material, not just a spoiler risk — gate the whole app behind a single shared
// passphrase (HTTP Basic auth). Required in every environment; fail loudly if missing.
//
// Blank counts as missing. appsettings.json ships the slot as "", so a deployment that loses
// Auth__Passphrase reads an empty string here, not null — and the gate would then let in anyone
// who leaves the password field empty.
string passphrase = builder.Configuration["Auth:Passphrase"] ?? string.Empty;
if (string.IsNullOrWhiteSpace(passphrase))
{
    throw new InvalidOperationException(
        "Missing or blank required configuration \"Auth:Passphrase\" (env var Auth__Passphrase). " +
        "The app refuses to start without a shared passphrase, since it is served at a public URL.");
}

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

// An optional path prefix ("PathBase", e.g. "/dnd") for serving the app below a path on a shared
// host as well as at the root of its own. UsePathBase is a no-op for a request that does not start
// with the prefix, so both entry points work without a proxy-side rewrite. Unset, the app is served
// at the root only. An image that serves a campaign below a prefix sets it with ENV PathBase.
string pathBase = app.Configuration["PathBase"] ?? string.Empty;
if (pathBase.Length > 0)
{
    if (!pathBase.StartsWith('/'))
    {
        throw new InvalidOperationException(
            $"Configuration \"PathBase\" (env var PathBase) must start with '/', like \"/dnd\"; it is \"{pathBase}\".");
    }

    app.UsePathBase(pathBase);
}

app.UseHttpsRedirection();

// Gate everything below this line — static files, maps, and the Razor/SignalR app itself —
// behind the shared passphrase. Only /healthz (used by the cluster's liveness probe, which
// carries no credentials) is exempt.
app.UseMiddleware<PassphraseAuthMiddleware>(passphrase);

// The app's own scripts and styles are linked without a fingerprint, and with no Cache-Control a
// browser guesses how long its copy stays fresh, so after a deploy it could go on running the last
// version's JavaScript against this version's markup (a new Room Editor tool would be missing). With
// no-cache it keeps its copy but asks each time; an unchanged file costs a 304.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});

// Serve map images (only) under /maps. The restricted content-type provider makes the
// static-files middleware 404 anything that is not an image - crucially the sibling
// *.regions.json files, which describe hidden traps/secrets and must never reach a browser.
FileExtensionContentTypeProvider imageContentTypes = new();
imageContentTypes.Mappings.Clear();
imageContentTypes.Mappings[".webp"] = "image/webp";
imageContentTypes.Mappings[".png"] = "image/png";
imageContentTypes.Mappings[".jpg"] = "image/jpeg";
imageContentTypes.Mappings[".jpeg"] = "image/jpeg";
imageContentTypes.Mappings[".gif"] = "image/gif";
imageContentTypes.Mappings[".svg"] = "image/svg+xml";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mapsRoot),
    RequestPath = "/maps",
    ContentTypeProvider = imageContentTypes,
    ServeUnknownFileTypes = false,
});

// Serve the projector art (only) under /art, the same way and for the same reason - but with a
// STRICTER content-type set than /maps: no SVG. An authored map is a file this repo committed; an
// uploaded picture is not, and an SVG fetched at /art/x.svg is same-origin markup that can run
// script. Raster only here, whatever the upload path already refused.
//
// Both this route and /maps sit BELOW the passphrase middleware, so the art - usually copyrighted
// book art, exactly what the gate exists for - is behind it on arrival. Never move either above
// the UseMiddleware<PassphraseAuthMiddleware> call, and never add an exemption for /art.
FileExtensionContentTypeProvider artContentTypes = new();
artContentTypes.Mappings.Clear();
artContentTypes.Mappings[".webp"] = "image/webp";
artContentTypes.Mappings[".png"] = "image/png";
artContentTypes.Mappings[".jpg"] = "image/jpeg";
artContentTypes.Mappings[".jpeg"] = "image/jpeg";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(artRoot),
    RequestPath = "/art",
    ContentTypeProvider = artContentTypes,
    ServeUnknownFileTypes = false,
});

app.UseAntiforgery();

app.MapGet("/healthz", () => Results.Ok());

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
