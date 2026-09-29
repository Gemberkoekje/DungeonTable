namespace DungeonTable.Core.Dossier;

/// <summary>
/// The campaign's rules reference: the conventions that hold on every floor (how doors, light or
/// resting work), shown on the Rules tab so they need not be repeated in every area.
/// </summary>
public sealed class ReferenceLibrary
{
    /// <summary>
    /// The heading the Rules tab shows above the sections, in the campaign's own words; none when blank.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The reference sections, each a titled block of rules prose.</summary>
    public IReadOnlyList<DossierBlock> Sections { get; init; } = Array.Empty<DossierBlock>();
}
