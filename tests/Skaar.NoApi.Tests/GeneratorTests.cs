using System.Net;
using System.Text.Json;
using Skaar.NoApi.OpenApi;

namespace Skaar.NoApi.Tests;

public class GeneratorTests : IDisposable
{
    internal const string Spec = """
        openapi: 3.0.3
        info: { title: Pets, version: "1" }
        servers: [ { url: /api } ]
        paths:
          /pets/{id}:
            get:
              operationId: getPet
              responses:
                "200":
                  description: OK
                  content:
                    application/json:
                      example: { id: 1, name: Fido }
            delete:
              operationId: deletePet
              responses: { "204": { description: Deleted } }
          /health:
            get:
              operationId: health
              responses:
                "200": { description: OK, content: { text/plain: { example: healthy } } }
        """;

    private readonly TempFolder folder = new();
    private readonly StringWriter output = new();

    private string Output => Path.Combine(folder.Path, "out");

    [Fact]
    public async Task WritesAManifestWithResponseFiles()
    {
        var spec = folder.Write("pets.yaml", Spec);

        var manifestPath = await Generator.Run([spec, Output], output, cancellationToken: NoApiHost.Ct);

        manifestPath.ShouldBe(Path.Combine(Output, "routes.json"));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, NoApiHost.Ct));
        manifest.RootElement.GetProperty("source").GetString().ShouldBe(spec);
        var responses = manifest.RootElement.GetProperty("routes").EnumerateArray()
            .Select(r => r.GetProperty("response")).ToList();
        responses.Select(r => r.TryGetProperty("path", out var p) ? p.GetString() : null)
            .ShouldBe(["./responses/health.txt", "./responses/getPet.json", null]);
        (await File.ReadAllTextAsync(Path.Combine(Output, "responses", "health.txt"), NoApiHost.Ct)).ShouldBe("healthy");
        output.ToString().ShouldContain("Wrote 3 route(s)");
    }

    [Fact]
    public async Task WritesBodiesInlineWhenAsked()
    {
        var spec = folder.Write("pets.yaml", Spec);

        await Generator.Run([spec, Output, "--inline"], output, cancellationToken: NoApiHost.Ct);

        Directory.Exists(Path.Combine(Output, "responses")).ShouldBeFalse();
        (await File.ReadAllTextAsync(Path.Combine(Output, "routes.json"), NoApiHost.Ct)).ShouldContain("\"body\": \"healthy\"");
    }

    [Fact]
    public async Task ServesTheGeneratedManifest()
    {
        var spec = folder.Write("pets.yaml", Spec);
        await Generator.Run([spec, Output], output, cancellationToken: NoApiHost.Ct);
        await using var host = await NoApiHost.Start(Output);

        (await host.GetJson("/api/pets/7")).GetProperty("name").GetString().ShouldBe("Fido");
        (await host.GetString("/api/health")).ShouldBe("healthy");
        (await host.Send(HttpMethod.Delete, "/api/pets/7")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DownloadsADocumentFromAUrl()
    {
        using var client = new HttpClient(new StubHandler(Spec));

        await Generator.Run(["https://example.com/openapi.yaml", Output], output, client, NoApiHost.Ct);

        (await File.ReadAllTextAsync(Path.Combine(Output, "routes.json"), NoApiHost.Ct))
            .ShouldContain("\"source\": \"https://example.com/openapi.yaml\"");
    }

    [Fact]
    public async Task RefusesToOverwriteAManifest()
    {
        var spec = folder.Write("pets.yaml", Spec);
        folder.Write("out/routes.json", "{}");

        await Should.ThrowAsync<InvalidOperationException>(() => Generator.Run([spec, Output], output));
        await Generator.Run([spec, Output, "--force"], output, cancellationToken: NoApiHost.Ct);

        (await File.ReadAllTextAsync(Path.Combine(Output, "routes.json"), NoApiHost.Ct)).ShouldContain("getPet");
    }

    [Theory]
    [InlineData]
    [InlineData("a", "b", "c")]
    [InlineData("a", "--unknown")]
    public async Task RefusesInvalidArguments(params string[] args)
    {
        await Should.ThrowAsync<ArgumentException>(() => Generator.Run(args, output));
    }

    public void Dispose()
    {
        output.Dispose();
        folder.Dispose();
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
    }
}
