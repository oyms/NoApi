namespace Skaar.NoApi.Tests;

public class FileWatchingTests : IDisposable
{
    private readonly TempFolder folder = new();

    [Fact]
    public async Task ReadsANewManifestInTheFolder()
    {
        folder.WriteManifest("a/routes.json", new { name = "A", response = new { } });
        await using var host = await NoApiHost.Start(folder.Path);
        (await host.ManifestRouteNames()).ShouldBe(["A"]);

        folder.WriteManifest("b/routes.json", new { name = "B", response = new { } });

        await NoApiHost.Eventually(async () => (await host.ManifestRouteNames()).Contains("B"), "the new manifest is read");
    }

    [Fact]
    public async Task ReadsAFolderWithManifestsMovedIn()
    {
        await using var host = await NoApiHost.Start(folder.Path);
        using var outside = new TempFolder();
        outside.WriteManifest("moved/routes.json", new { name = "Moved", response = new { } });

        Directory.Move(Path.Combine(outside.Path, "moved"), Path.Combine(folder.Path, "moved"));

        await NoApiHost.Eventually(async () => (await host.ManifestRouteNames()).Contains("Moved"),
            "the moved manifest is read");
    }

    [Fact]
    public async Task ForgetsARemovedManifest()
    {
        folder.WriteManifest("a/routes.json", new { name = "A", response = new { } });
        folder.WriteManifest("b/routes.json", new { name = "B", response = new { } });
        await using var host = await NoApiHost.Start(folder.Path);
        (await host.ManifestRouteNames()).ShouldBe(["A", "B"]);

        Directory.Delete(Path.Combine(folder.Path, "b"), recursive: true);

        await NoApiHost.Eventually(async () => (await host.ManifestRouteNames()).SequenceEqual(["A"]),
            "the removed manifest is forgotten");
    }

    [Fact]
    public async Task ReadsAChangedManifestFile()
    {
        var manifest = folder.WriteManifest("api.json", new { name = "Before", response = new { } });
        await using var host = await NoApiHost.Start(manifest);
        (await host.ManifestRouteNames()).ShouldBe(["Before"]);

        folder.WriteManifest("api.json", new { name = "After", response = new { } });

        await NoApiHost.Eventually(async () => (await host.ManifestRouteNames()).SequenceEqual(["After"]),
            "the changed manifest is read");
    }

    [Fact]
    public async Task ReadsAManifestFileReplacedByRename()
    {
        var manifest = folder.WriteManifest("api.json", new { name = "Before", response = new { } });
        await using var host = await NoApiHost.Start(manifest);
        (await host.ManifestRouteNames()).ShouldBe(["Before"]);

        // Like an editor saving atomically.
        var temp = folder.WriteManifest("api.json.tmp", new { name = "After", response = new { } });
        File.Move(temp, manifest, overwrite: true);

        await NoApiHost.Eventually(async () => (await host.ManifestRouteNames()).SequenceEqual(["After"]),
            "the replaced manifest is read");
    }

    [Fact]
    public async Task ServesChangesToAResponseFile()
    {
        folder.Write("hit.txt", "before");
        var manifest = folder.WriteManifest("api.json", new { path = "^/thing$", response = new { path = "hit.txt" } });
        await using var host = await NoApiHost.Start(manifest);
        (await host.GetString("/thing")).ShouldBe("before");

        folder.Write("hit.txt", "after");

        (await host.GetString("/thing")).ShouldBe("after");
    }

    public void Dispose() => folder.Dispose();
}
