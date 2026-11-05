using System.Net;
using Skaar.NoApi.Services;

namespace Skaar.NoApi.Tests;

public class RoutingTests : IDisposable
{
    private readonly TempFolder folder = new();

    [Fact]
    public async Task ServesTheResponseFileOfAMatchingRoute()
    {
        folder.Write("responses/users.json", """[{"id":1}]""");
        var manifest = folder.WriteManifest("api.json",
            new { name = "Users", path = "^/api/users$", response = new { path = "responses/users.json" } });
        await using var host = await NoApiHost.Start(manifest);

        var response = await host.Get("/api/users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.Content.ReadAsStringAsync(NoApiHost.Ct)).ShouldBe("""[{"id":1}]""");
    }

    [Fact]
    public async Task RespondsWithAnEmptyDocumentWhenNoRouteMatches()
    {
        var manifest = folder.WriteManifest("api.json",
            new { path = "^/api/users$", response = new { path = "users.json" } });
        await using var host = await NoApiHost.Start(manifest);

        var response = await host.Get("/something/else");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(NoApiHost.Ct)).Trim().ShouldBe("{}");
    }

    [Fact]
    public async Task UsesTheFirstMatchingRoute()
    {
        folder.Write("first.txt", "first");
        folder.Write("second.txt", "second");
        var manifest = folder.WriteManifest("api.json",
            new { path = "^/specific$", response = new { path = "first.txt" } },
            new { path = "^/spec", response = new { path = "second.txt" } });
        await using var host = await NoApiHost.Start(manifest);

        (await host.GetString("/specific")).ShouldBe("first");
        (await host.GetString("/special")).ShouldBe("second");
    }

    [Theory]
    [InlineData("GET", "GET", true)]
    [InlineData("get", "GET", true)]
    [InlineData("GET", "POST", false)]
    [InlineData("GET, POST", "POST", true)]
    [InlineData("GET, POST", "PUT", false)]
    [InlineData("DELETE", "DELETE", true)]
    [InlineData("PATCH", "PATCH", true)]
    [InlineData("ANY", "PUT", true)]
    public async Task MatchesTheMethod(string routeMethod, string requestMethod, bool matches)
    {
        folder.Write("hit.txt", "hit");
        var manifest = folder.WriteManifest("api.json",
            new { method = routeMethod, path = "^/thing$", response = new { path = "hit.txt" } });
        await using var host = await NoApiHost.Start(manifest);

        var response = await host.Send(new HttpMethod(requestMethod), "/thing");

        (await response.Content.ReadAsStringAsync(NoApiHost.Ct) == "hit").ShouldBe(matches);
    }

    [Fact]
    public async Task MatchesQueryParameters()
    {
        folder.Write("hit.txt", "hit");
        var manifest = folder.WriteManifest("api.json",
            new
            {
                path = "^/search$",
                parameters = new object[] { new { parameter = "q", value = "^ola" }, new { parameter = "page" } },
                response = new { path = "hit.txt" }
            });
        await using var host = await NoApiHost.Start(manifest);

        (await host.GetString("/search?q=ola&page=1")).ShouldBe("hit");
        (await host.GetString("/search?q=kari&page=1")).ShouldNotBe("hit");
        (await host.GetString("/search?q=ola")).ShouldNotBe("hit");
    }

    [Fact]
    public async Task MatchesFormFields()
    {
        folder.Write("token.json", """{"access_token":"x"}""");
        var manifest = folder.WriteManifest("api.json",
            new
            {
                method = "POST",
                path = "^/token$",
                parameters = new object[] { new { parameter = "grant_type", value = "^client_credentials$" } },
                response = new { path = "token.json" }
            });
        await using var host = await NoApiHost.Start(manifest);

        var hit = await host.Post("/token", ("grant_type", "client_credentials"));
        var miss = await host.Post("/token", ("grant_type", "password"));

        (await hit.Content.ReadAsStringAsync(NoApiHost.Ct)).ShouldContain("access_token");
        (await miss.Content.ReadAsStringAsync(NoApiHost.Ct)).ShouldNotContain("access_token");
    }

    [Fact]
    public async Task MatchesTheUrlEncodedPath()
    {
        folder.Write("hit.txt", "hit");
        var manifest = folder.WriteManifest("api.json",
            new { path = "^/files/my%20file$", response = new { path = "hit.txt" } });
        await using var host = await NoApiHost.Start(manifest);

        (await host.GetString("/files/my%20file")).ShouldBe("hit");
    }

    [Fact]
    public async Task UsesStatusCodeContentTypeAndHeaders()
    {
        folder.Write("problem.json", """{"status":404}""");
        var manifest = folder.WriteManifest("api.json",
            new
            {
                path = "^/missing$",
                response = new
                {
                    path = "problem.json",
                    statusCode = 404,
                    contentType = "application/problem+json",
                    headers = new Dictionary<string, string> { ["X-Correlation-Id"] = "abc" }
                }
            });
        await using var host = await NoApiHost.Start(manifest);

        var response = await host.Get("/missing");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.GetValues("X-Correlation-Id").ShouldBe(["abc"]);
        (await response.Content.ReadAsStringAsync(NoApiHost.Ct)).ShouldBe("""{"status":404}""");
    }

    [Fact]
    public async Task RespondsWithAnEmptyBodyWithoutAResponseFile()
    {
        var manifest = folder.WriteManifest("api.json",
            new { method = "DELETE", path = "^/thing$", response = new { statusCode = 204 } });
        await using var host = await NoApiHost.Start(manifest);

        var response = await host.Send(HttpMethod.Delete, "/thing");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(NoApiHost.Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RespondsWithAnInlineBody()
    {
        var manifest = folder.WriteManifest("api.json",
            new { path = "^/json$", response = new { body = new { id = 1 } } },
            new { path = "^/text$", response = new { body = "plain text", contentType = "text/plain" } },
            new { name = "Both", path = "^/both$", response = new { path = "x.json", body = 1 } });
        await using var host = await NoApiHost.Start(manifest);

        (await host.GetJson("/json")).GetProperty("id").GetInt32().ShouldBe(1);
        (await host.GetString("/text")).ShouldBe("plain text");
        (await host.ManifestRouteNames()).ShouldNotContain("Both");
    }

    [Fact]
    public async Task ServesOpenApiDocumentsInTheFolder()
    {
        folder.Write("pets/openapi.yaml", GeneratorTests.Spec);
        folder.Write("pets/other.yaml", GeneratorTests.Spec);
        await using var host = await NoApiHost.Start(folder.Path);

        (await host.ManifestRouteNames()).ShouldBe(["health", "getPet", "deletePet"]);
        (await host.GetJson("/api/pets/7")).GetProperty("name").GetString().ShouldBe("Fido");
    }

    [Fact]
    public async Task ServesAnOpenApiDocumentGivenAsAFile()
    {
        var spec = folder.Write("pets.yaml", GeneratorTests.Spec);
        await using var host = await NoApiHost.Start(spec);

        (await host.GetString("/api/health")).ShouldBe("healthy");
    }

    [Fact]
    public async Task RespondsWithAnErrorWhenTheResponseFileIsMissing()
    {
        var manifest = folder.WriteManifest("api.json",
            new { path = "^/thing$", response = new { path = "missing.json", statusCode = 201 } });
        await using var host = await NoApiHost.Start(manifest);

        var response = await host.Get("/thing");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync(NoApiHost.Ct)).ShouldContain("missing.json");
    }

    [Fact]
    public async Task SkipsInvalidRoutes()
    {
        folder.Write("hit.txt", "hit");
        var manifest = folder.WriteManifest("api.json",
            new { name = "Bad regex", path = "^/(unclosed", response = new { path = "hit.txt" } },
            new { name = "Bad method", method = "FETCH", response = new { path = "hit.txt" } },
            new { name = "No response", path = "^/thing$" },
            new { name = "Good", path = "^/thing$", response = new { path = "hit.txt" } });
        await using var host = await NoApiHost.Start(manifest);

        (await host.ManifestRouteNames()).ShouldBe(["Good"]);
        (await host.GetString("/thing")).ShouldBe("hit");
    }

    [Fact]
    public async Task IgnoresAnInvalidManifest()
    {
        folder.Write("broken/routes.json", "{ not json");
        folder.Write("good/hit.txt", "hit");
        folder.WriteManifest("good/routes.json", new { name = "Good", path = "^/thing$", response = new { path = "hit.txt" } });
        await using var host = await NoApiHost.Start(folder.Path);

        (await host.ManifestRouteNames()).ShouldBe(["Good"]);
    }

    [Fact]
    public async Task ReadsManifestsRecursivelyFromAFolder()
    {
        folder.Write("a/a.txt", "a");
        folder.Write("b/c/c.txt", "c");
        folder.WriteManifest("a/routes.json", new { name = "A", path = "^/a$", response = new { path = "a.txt" } });
        folder.WriteManifest("b/c/routes.json", new { name = "C", path = "^/c$", response = new { path = "c.txt" } });
        folder.WriteManifest("b/other.json", new { name = "Not a manifest", response = new { path = "x" } });
        await using var host = await NoApiHost.Start(folder.Path);

        (await host.ManifestRouteNames()).ShouldBe(["A", "C"]);
        (await host.GetString("/a")).ShouldBe("a");
        (await host.GetString("/c")).ShouldBe("c");
    }

    [Fact]
    public async Task UsesTheConfiguredManifestFileName()
    {
        folder.WriteManifest("a/api.json", new { name = "A", response = new { } });
        folder.WriteManifest("a/routes.json", new { name = "Not a manifest", response = new { } });
        await using var host = await NoApiHost.Start(folder.Path, "--NoApi:ManifestFileName=api.json");

        (await host.ManifestRouteNames()).ShouldBe(["A"]);
    }

    [Fact]
    public async Task ListsTheRoutes()
    {
        var manifest = folder.WriteManifest("api.json",
            new { name = "Users", method = "GET, POST", path = "^/users$", response = new { statusCode = 202 } });
        await using var host = await NoApiHost.Start(manifest);

        var routes = (await host.GetJson(BuiltInRoutes.RoutesPath)).GetProperty("routes").EnumerateArray().ToList();

        routes.Select(r => r.GetProperty("name").GetString())
            .ShouldBe(["Route listing", "Manifest schema", "Users", "Fallback"]);
        var users = routes[2];
        users.GetProperty("method").GetString().ShouldBe("GET, POST");
        users.GetProperty("path").GetString().ShouldBe("^/users$");
        users.GetProperty("statusCode").GetInt32().ShouldBe(202);
        users.GetProperty("source").GetString().ShouldBe(manifest);
    }

    [Fact]
    public async Task ListsTheRoutesDespiteACatchAllRoute()
    {
        var manifest = folder.WriteManifest("api.json", new { name = "Everything", response = new { } });
        await using var host = await NoApiHost.Start(manifest);

        (await host.ManifestRouteNames()).ShouldBe(["Everything"]);
    }

    [Fact]
    public async Task ServesTheSchema()
    {
        await using var host = await NoApiHost.Start(folder.Path);

        var schema = await host.GetJson(BuiltInRoutes.SchemaPath);

        schema.GetProperty("title").GetString().ShouldBe("NoApi route manifest");
    }

    [Fact]
    public void RefusesAMissingManifestLocation()
    {
        Should.Throw<ArgumentException>(() => Server.Create([Path.Combine(folder.Path, "missing.json")]));
    }

    public void Dispose() => folder.Dispose();
}
