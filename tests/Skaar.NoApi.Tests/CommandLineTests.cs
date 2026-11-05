using Skaar.NoApi.Manifests;

namespace Skaar.NoApi.Tests;

public class CommandLineTests
{
    [Theory]
    [InlineData(new string[0], null, new string[0])]
    [InlineData(new[] { "./routes.json" }, "./routes.json", new string[0])]
    [InlineData(new[] { "/abs/routes.json", "--urls=http://localhost:1" }, "/abs/routes.json", new[] { "--urls=http://localhost:1" })]
    [InlineData(new[] { "--urls", "http://localhost:1", "./mocks" }, "./mocks", new[] { "--urls", "http://localhost:1" })]
    [InlineData(new[] { "--urls=http://localhost:1", "--", "./mocks" }, "./mocks", new[] { "--urls=http://localhost:1" })]
    [InlineData(new[] { "NoApi:Url=http://localhost:1" }, null, new[] { "NoApi:Url=http://localhost:1" })]
    public void SeparatesTheManifestPathFromConfiguration(string[] args, string? path, string[] configuration)
    {
        var (manifestPath, configurationArgs) = CommandLine.Split(args);

        manifestPath.ShouldBe(path);
        configurationArgs.ShouldBe(configuration);
    }
}
