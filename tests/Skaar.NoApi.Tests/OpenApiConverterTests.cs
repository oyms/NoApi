using System.Text.Json;
using Skaar.NoApi.Model;
using Skaar.NoApi.OpenApi;

namespace Skaar.NoApi.Tests;

public class OpenApiConverterTests
{
    private static Manifest Convert(string yaml, out IReadOnlyList<string> warnings)
    {
        var document = OpenApiLoader.Parse(yaml, out _);
        (var manifest, warnings) = OpenApiConverter.Convert(document, "spec.yaml");
        return manifest;
    }

    private static Manifest Convert(string yaml) => Convert(yaml, out _);

    /// <summary>The body of the GET /x operation, responding with the given media type.</summary>
    private static JsonElement? Body(string mediaYaml, string contentType = "application/json") =>
        Convert($$"""
            openapi: 3.0.3
            info: { title: Test, version: "1" }
            paths:
              /x:
                get:
                  responses:
                    "200":
                      description: OK
                      content:
                        {{contentType}}:
            {{Indent(mediaYaml, 14)}}
            """).Routes!.Single().Response!.Body;

    private static string Indent(string text, int spaces) =>
        string.Join("\n", text.Split('\n').Select(l => new string(' ', spaces) + l));

    [Fact]
    public void CreatesARoutePerOperation()
    {
        var manifest = Convert("""
            openapi: 3.0.3
            info: { title: Test, version: "1" }
            servers:
              - url: https://api.example.com/v1/
            paths:
              /users/{id}:
                get:
                  operationId: getUser
                  responses: { "200": { description: OK } }
                delete:
                  responses: { "204": { description: Deleted } }
              /users/me:
                get:
                  operationId: getMe
                  responses: { "200": { description: OK } }
            """);

        manifest.Source.ShouldBe("spec.yaml");
        manifest.Routes!.Select(r => (r.Name, r.Method, r.Path)).ShouldBe([
            ("getMe", "GET", "^/v1/users/me/?$"),
            ("getUser", "GET", "^/v1/users/[^/]+/?$"),
            ("DELETE /users/{id}", "DELETE", "^/v1/users/[^/]+/?$")
        ]);
    }

    [Theory]
    [InlineData(null, "^/things/?$")]
    [InlineData("/api/v3", "^/api/v3/things/?$")]
    [InlineData("{scheme}://example.com/{version}", "^/[^/]+/things/?$")]
    [InlineData("https://example.com", "^/things/?$")]
    public void UsesThePathOfTheFirstServer(string? server, string expected)
    {
        var servers = server is null ? "" : $"servers: [ {{ url: \"{server}\" }} ]";
        var manifest = Convert($$"""
            openapi: 3.0.3
            info: { title: Test, version: "1" }
            {{servers}}
            paths:
              /things:
                get: { responses: { "200": { description: OK } } }
            """);

        manifest.Routes!.Single().Path.ShouldBe(expected);
    }

    [Fact]
    public void ConvertsSwagger2()
    {
        var manifest = Convert("""
            swagger: "2.0"
            info: { title: Test, version: "1" }
            host: example.com
            basePath: /api
            paths:
              /things/{id}:
                get:
                  produces: [ application/json ]
                  responses:
                    "200":
                      description: OK
                      schema: { type: object, properties: { id: { type: integer } } }
            """);

        var route = manifest.Routes!.Single();
        route.Path.ShouldBe("^/api/things/[^/]+/?$");
        route.Response!.Body!.Value.GetProperty("id").GetInt32().ShouldBe(0);
    }

    [Theory]
    [InlineData("""{ "404": { description: x }, "201": { description: x }, "200": { description: x } }""", 200)]
    [InlineData("""{ "default": { description: x }, "202": { description: x } }""", 202)]
    [InlineData("""{ "default": { description: x }, "404": { description: x } }""", null)]
    [InlineData("""{ "404": { description: x } }""", 404)]
    [InlineData("""{ "5XX": { description: x } }""", 500)]
    public void UsesTheSuccessfulResponse(string responses, int? statusCode)
    {
        var manifest = Convert($$"""
            openapi: 3.0.3
            info: { title: Test, version: "1" }
            paths:
              /x:
                get: { responses: {{responses}} }
            """);

        manifest.Routes!.Single().Response!.StatusCode.ShouldBe(statusCode);
    }

    [Fact]
    public void SkipsUnsupportedMethods()
    {
        var manifest = Convert("""
            openapi: 3.0.3
            info: { title: Test, version: "1" }
            paths:
              /x:
                trace: { responses: { "200": { description: OK } } }
                get: { responses: { "200": { description: OK } } }
            """, out var warnings);

        manifest.Routes!.Select(r => r.Method).ShouldBe(["GET"]);
        warnings.ShouldHaveSingleItem().ShouldContain("TRACE");
    }

    [Fact]
    public void PrefersTheExampleOfTheMediaType()
    {
        var body = Body("""
            example: { source: media }
            examples: { first: { value: { source: examples } } }
            schema: { type: object, example: { source: schema } }
            """);

        body!.Value.GetProperty("source").GetString().ShouldBe("media");
    }

    [Fact]
    public void UsesTheFirstOfTheExamples()
    {
        var body = Body("""
            examples:
              first: { value: { source: first } }
              second: { value: { source: second } }
            schema: { type: object, example: { source: schema } }
            """);

        body!.Value.GetProperty("source").GetString().ShouldBe("first");
    }

    [Fact]
    public void BuildsAnExampleFromTheSchema()
    {
        var body = Body("""
            schema:
              type: object
              properties:
                id: { type: string, format: uuid }
                created: { type: string, format: date-time }
                email: { type: string, format: email }
                name: { type: string, example: Ola }
                status: { type: string, enum: [ active, inactive ] }
                count: { type: integer }
                price: { type: number }
                enabled: { type: boolean }
                size: { type: integer, default: 10 }
                tags: { type: array, items: { type: string } }
                parent: { type: object, nullable: true, properties: { id: { type: integer } } }
                password: { type: string, writeOnly: true }
                scores: { type: object, additionalProperties: { type: integer } }
            """);

        JsonSerializer.Serialize(body).ShouldBe(
            "{\"id\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"created\":\"2026-01-01T12:00:00Z\"," +
            "\"email\":\"user@example.com\",\"name\":\"Ola\",\"status\":\"active\",\"count\":0,\"price\":0," +
            "\"enabled\":true,\"size\":10,\"tags\":[\"string\"],\"parent\":{\"id\":0},\"scores\":{\"property\":0}}");
    }

    [Fact]
    public void MergesAllOf()
    {
        var body = Body("""
            schema:
              allOf:
                - { type: object, properties: { a: { type: integer } } }
                - { type: object, properties: { b: { type: boolean } } }
              properties:
                c: { type: string }
            """);

        JsonSerializer.Serialize(body).ShouldBe("""{"a":0,"b":true,"c":"string"}""");
    }

    [Fact]
    public void UsesTheFirstOfOneOf()
    {
        var body = Body("""
            schema:
              oneOf:
                - { type: string, format: date }
                - { type: integer }
            """);

        body!.Value.GetString().ShouldBe("2026-01-01");
    }

    [Fact]
    public void LimitsRecursiveSchemas()
    {
        var manifest = Convert("""
            openapi: 3.0.3
            info: { title: Test, version: "1" }
            paths:
              /tree:
                get:
                  responses:
                    "200":
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: "#/components/schemas/Node" }
            components:
              schemas:
                Node:
                  type: object
                  properties:
                    name: { type: string }
                    child: { $ref: "#/components/schemas/Node" }
            """);

        var body = manifest.Routes!.Single().Response!.Body!.Value;
        body.GetProperty("child").GetProperty("child").GetProperty("name").GetString().ShouldBe("string");
    }

    [Fact]
    public void UsesStringExamplesForOtherContentTypes()
    {
        Body("example: hello", "text/plain")!.Value.GetString().ShouldBe("hello");
        Body("schema: { type: object, properties: { a: { type: string } } }", "application/xml").ShouldBeNull();
    }

    [Fact]
    public void KeepsTheContentTypeUnlessJson()
    {
        var manifest = Convert("""
            openapi: 3.0.3
            info: { title: Test, version: "1" }
            paths:
              /json:
                get:
                  responses: { "200": { description: OK, content: { application/xml: { }, application/json: { } } } }
              /problem:
                get:
                  responses: { "400": { description: OK, content: { application/problem+json: { } } } }
              /text:
                get:
                  responses: { "200": { description: OK, content: { text/plain: { example: hi } } } }
            """);

        manifest.Routes!.Select(r => r.Response!.ContentType).ShouldBe([null, "application/problem+json", "text/plain"]);
    }

    [Fact]
    public void RefusesAnInvalidDocument()
    {
        Should.Throw<FormatException>(() => OpenApiLoader.Parse("not: [ an openapi document", out _));
    }
}
