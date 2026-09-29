using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Tests.Maps;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The whole chain of live reload in the real app: a level file edited on disk is noticed by the
/// watcher, read into a new snapshot, and served by the next page, with no restart in between.
/// </summary>
public sealed class LiveReloadHostTests
{
    [Fact]
    public async Task A_level_file_edited_on_disk_shows_on_the_next_page_without_a_restart()
    {
        using var pack = new PackCopy();
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            pack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));
        Assert.Contains("Undercroft Landing", await client.GetStringAsync("/dm", CancellationToken.None), StringComparison.Ordinal);

        string level = Path.Combine(pack.Root, "data", "dossiers", "level-1.json");
        string text = await File.ReadAllTextAsync(level, CancellationToken.None);
        await File.WriteAllTextAsync(level, text.Replace("Undercroft Landing", "The Lantern Landing", StringComparison.Ordinal), CancellationToken.None);

        string page = string.Empty;
        DateTime until = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < until && !page.Contains("The Lantern Landing", StringComparison.Ordinal))
        {
            await Task.Delay(200, CancellationToken.None);
            page = await client.GetStringAsync("/dm", CancellationToken.None);
        }

        Assert.Contains("The Lantern Landing", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Watching_can_be_turned_off()
    {
        using var pack = new PackCopy();
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            pack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
            builder.UseSetting("Content:Watch", "false");
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));
        await client.GetStringAsync("/dm", CancellationToken.None);

        string level = Path.Combine(pack.Root, "data", "dossiers", "level-1.json");
        string text = await File.ReadAllTextAsync(level, CancellationToken.None);
        await File.WriteAllTextAsync(level, text.Replace("Undercroft Landing", "The Lantern Landing", StringComparison.Ordinal), CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.DoesNotContain("The Lantern Landing", await client.GetStringAsync("/dm", CancellationToken.None), StringComparison.Ordinal);
    }
}
