using System.Collections.Generic;

namespace DungeonTable.ContentTests;

/// <summary>How a content test fails: with every fault a rule found, each saying where it is.</summary>
internal static class Report
{
    /// <summary>Fails with every fault when there are any.</summary>
    /// <param name="faults">What a rule found.</param>
    /// <param name="what">What kind of fault these are, for the first line of the failure.</param>
    internal static void None(IReadOnlyList<string> faults, string what) =>
        Assert.True(faults.Count == 0, $"{what}:\n{string.Join("\n", faults)}");
}
