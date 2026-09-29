using System.IO;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// Proves which folder the content tests check: the one <c>DUNGEONTABLE_CONTENT_ROOT</c> names, loudly
/// refused when there is none, and the sample pack when the variable is unset.
/// </summary>
public sealed class ContentRootResolveTests
{
    [Fact]
    public void The_folder_the_variable_names_is_the_content_root()
    {
        using var content = new TempContent();

        Assert.Equal(content.Root, ContentRoot.Resolve($"  {content.Root}  ", content.Start));
    }

    [Fact]
    public void A_variable_that_names_no_folder_stops_the_tests_and_says_so()
    {
        using var content = new TempContent();
        string missing = Path.Combine(content.Root, "no-such-campaign");

        DirectoryNotFoundException error = Assert.Throws<DirectoryNotFoundException>(() => ContentRoot.Resolve(missing, content.Start));

        Assert.Contains(ContentRoot.Variable, error.Message, StringComparison.Ordinal);
        Assert.Contains(missing, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_the_variable_the_sample_pack_above_the_tests_is_the_content_root()
    {
        using var content = new TempContent();
        content.Write("samples/demo/data/dossiers/level-1.json", "{}");
        string deep = Path.Combine(content.Root, "app", "bin", "Debug");
        Directory.CreateDirectory(deep);

        Assert.Equal(Path.Combine(content.Root, "samples", "demo"), ContentRoot.Resolve(string.Empty, deep));
    }

    [Fact]
    public void Without_the_variable_or_a_sample_pack_the_tests_stop_and_say_so()
    {
        using var content = new TempContent();

        DirectoryNotFoundException error = Assert.Throws<DirectoryNotFoundException>(() => ContentRoot.Resolve(null, content.Start));

        Assert.Contains("samples/demo", error.Message, StringComparison.Ordinal);
    }
}
