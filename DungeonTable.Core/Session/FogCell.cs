namespace DungeonTable.Core.Session;

/// <summary>
/// One painted grid cell in the stored fog mask. The live state keys cells by a
/// <c>(Col, Row)</c> tuple, which has no stable JSON shape, so the document spells the pair out.
/// </summary>
public sealed class FogCell
{
    /// <summary>Grid column.</summary>
    public int Col { get; set; }

    /// <summary>Grid row.</summary>
    public int Row { get; set; }
}
