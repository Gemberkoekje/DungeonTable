using System.Collections.Generic;
using System.IO;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Maps;

namespace DungeonTable.ContentTests;

/// <summary>
/// Every map's annotations: the file is where a save writes it, the map has a drawing, every region
/// can be clicked and opens an area, every area on a mapped floor can be clicked, and no region covers
/// another's middle.
/// </summary>
public sealed class MapTests
{
    public static TheoryData<string> Maps => ContentDocuments.Names(DocumentKind.MapRegions);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Maps))]
    public void Every_map_is_filed_under_its_own_id_and_adventure(string document)
    {
        MapDefinition map = ContentDocuments.Loaded<MapDefinition>(document);
        string underMaps = Path.GetRelativePath(ContentRoot.MapsFolder, ContentDocuments.Named(document).FullPath).Replace('\\', '/');

        Report.None(MapAudit.Misfiled(underMaps, map), $"Where {document} is filed");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Maps))]
    public void Every_map_can_be_opened(string document)
    {
        MapDefinition map = ContentDocuments.Loaded<MapDefinition>(document);
        IReadOnlySet<string> drawings = ContentDocuments.Of(DocumentKind.MapDrawing)
            .Select(drawing => Path.GetFileNameWithoutExtension(drawing.FullPath))
            .ToHashSet(StringComparer.Ordinal);

        Report.None(MapAudit.Unopenable(document, map, ContentDocuments.LoadedOf<MapDefinition>(DocumentKind.MapRegions), drawings), $"Why {document} cannot be opened");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Maps))]
    public void Every_region_can_be_clicked(string document)
    {
        Report.None(MapAudit.BadRegions(ContentDocuments.Loaded<MapDefinition>(document)), $"Regions in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Maps))]
    public void Every_region_opens_an_area_or_waits_for_its_floor(string document)
    {
        MapDefinition map = ContentDocuments.Loaded<MapDefinition>(document);

        Report.None(MapAudit.BadLinks(map, ContentRoot.Dossiers, CampaignAudit.FloorsWithAFile(ContentRoot.Dossiers)), $"Region links in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Maps))]
    public void Every_area_on_a_mapped_floor_can_be_clicked(string document)
    {
        MapDefinition map = ContentDocuments.Loaded<MapDefinition>(document);
        IEnumerable<MapDefinition> all = ContentDocuments.LoadedOf<MapDefinition>(DocumentKind.MapRegions).Select(loaded => loaded.Content);

        Report.None(MapAudit.Unclickable(map, all, ContentRoot.Dossiers), $"Areas on {document}'s floors that cannot be clicked");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Maps))]
    public void No_region_covers_another_regions_middle(string document)
    {
        Report.None(MapAudit.Covered(ContentDocuments.Loaded<MapDefinition>(document)), $"Regions in {document} that cover another");
    }
}
