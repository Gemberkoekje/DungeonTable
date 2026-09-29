using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Infrastructure.Maps;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace DungeonTable.Tests.Maps;

/// <summary>
/// Boots the real web host once against a throwaway maps root that contains a single map
/// carrying a hidden trap feature, with the rest of its content from the sample pack. Shared across
/// the map-serving integration tests so the content is loaded only once.
/// </summary>
public sealed class MapServingFixture : IDisposable
{
    /// <summary>A distinctive marker used to detect whether hidden feature data leaks to a view.</summary>
    public const string TrapLabel = "SECRET_TRAP_MARKER";

    private readonly string tempMaps =
        Path.Combine(Path.GetTempPath(), "dt-serve-" + Guid.NewGuid().ToString("N"));

    private readonly WebApplicationFactory<Program> factory;

    /// <summary>The passphrase configured for the test host — see <see cref="Client"/>.</summary>
    public const string Passphrase = "test-passphrase";

    public MapServingFixture()
    {
        string adventure = Path.Combine(tempMaps, "adv");
        Directory.CreateDirectory(adventure);

        // A minimal RIFF/WEBP header is enough: static serving keys the content type off the
        // file extension, not the bytes.
        File.WriteAllBytes(
            Path.Combine(adventure, "trap-map.webp"),
            new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 });

        // A minimal Dungeon Scrawl source so the vector DM/player views have a map to render.
        File.WriteAllBytes(Path.Combine(adventure, "trap-map.ds"), MinimalDsArchive());

        var map = new MapDefinition
        {
            MapId = "trap-map",
            Adventure = "adv",
            LevelName = "Trap Test Level",
            Features = new[]
            {
                new FeatureMarker
                {
                    FeatureId = "f1",
                    Kind = FeatureKind.Trap,
                    Label = TrapLabel,
                    Position = new MapPoint(50, 50),
                },
            },
        };
        new FileSystemMapStore(tempMaps).SaveAsync(map, CancellationToken.None).GetAwaiter().GetResult();

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Maps:Root", tempMaps);
            builder.UseSetting("Auth:Passphrase", Passphrase);
        });

        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{Passphrase}")));
    }

    /// <summary>An HTTP client against the running test host, pre-authenticated with the shared passphrase.</summary>
    public HttpClient Client { get; }

    /// <summary>A client against the same host that never sends credentials, for auth-gate tests.</summary>
    public HttpClient CreateUnauthenticatedClient() => factory.CreateClient();

    public void Dispose()
    {
        Client.Dispose();
        factory.Dispose();
        if (Directory.Exists(tempMaps))
        {
            Directory.Delete(tempMaps, recursive: true);
        }
    }

    // A tiny valid .ds: one floor polygon, a wall style pass and a single door, enough for the
    // vector renderer to emit a map SVG with a door object.
    private static byte[] MinimalDsArchive()
    {
        const string map = """
            {
              "version": 1,
              "state": { "document": { "documentNodeId": "document", "nodes": {
                "document": { "type": "DOCUMENT", "id": "document", "selectedPage": "page" },
                "page": { "type": "PAGE", "id": "page", "children": ["tmpl", "imgs"], "grid": { "cellDiameter": 36 } },
                "tmpl": { "type": "TEMPLATE", "id": "tmpl", "parentId": "page", "children": ["geo"] },
                "geo": { "type": "GEOMETRY", "id": "geo", "parentId": "tmpl", "geometryId": "g1", "children": ["floor", "walls"] },
                "floor": { "type": "MULTIPOLYGON", "id": "floor", "parentId": "geo", "name": "Floor", "mask": false, "visible": true,
                  "fill": { "visible": true, "colour": { "colour": 15789280, "alpha": 1 } },
                  "stroke": { "visible": false, "width": 0, "colour": { "colour": 0, "alpha": 1 } } },
                "walls": { "type": "MULTIPOLYGON", "id": "walls", "parentId": "geo", "name": "Walls", "mask": false, "visible": true,
                  "fill": { "visible": false, "colour": { "colour": 16777215, "alpha": 1 } },
                  "stroke": { "visible": true, "width": 5, "colour": { "colour": 0, "alpha": 1 }, "roughOptions": "low" } },
                "imgs": { "type": "IMAGES", "id": "imgs", "parentId": "page", "children": ["door"] },
                "door": { "type": "DUNGEON_ASSET", "id": "door", "parentId": "imgs", "name": "Door", "transform": [1, 0, 0, 1, 0, 0], "children": ["doorgeo"] },
                "doorgeo": { "type": "GEOMETRY", "id": "doorgeo", "parentId": "door", "geometryId": "g2", "children": [] }
              } } },
              "data": { "geometry": {
                "g1": { "polygons": [[[[0, 0], [100, 0], [100, 100], [0, 100], [0, 0]]]], "polylines": [] },
                "g2": { "polygons": [[[[40, 0], [60, 0], [60, 20], [40, 20], [40, 0]]]], "polylines": [] }
              } }
            }
            """;

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("map");
            using Stream entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(map);
        }

        return buffer.ToArray();
    }
}
