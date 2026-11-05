using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Skaar.NoApi.Model;
using Skaar.NoApi.OpenApi;

namespace Skaar.NoApi.Services;

/// <summary>
/// Reads the routes of a manifest file. Errors are logged, and invalid routes (or manifests) are skipped,
/// so a manifest being written, or a mistake in one route, does not take down the others.
/// </summary>
internal sealed class ManifestParser(ILogger<ManifestParser> logger)
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public IReadOnlyList<Route> Parse(string manifestPath)
    {
        Manifest? manifest;
        try
        {
            manifest = Read(manifestPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException
                                      or OpenApiException)
        {
            logger.LogError("Could not read manifest {Manifest}: {Message}", manifestPath, e.Message);
            return [];
        }

        var directory = Path.GetDirectoryName(manifestPath)!;
        var routes = new List<Route>();
        var definitions = manifest?.Routes ?? [];
        for (var i = 0; i < definitions.Count; i++)
        {
            try
            {
                routes.Add(CreateRoute(definitions[i], directory, manifestPath));
            }
            catch (Exception e) when (e is FormatException or ArgumentException)
            {
                logger.LogError("Skipping route {Route} in {Manifest}: {Message}",
                    definitions[i].Name ?? $"#{i + 1}", manifestPath, e.Message);
            }
        }

        return routes;
    }

    /// <summary>Reads a manifest, or converts an OpenAPI document to one.</summary>
    private Manifest? Read(string path)
    {
        var content = File.ReadAllText(path);
        if (!OpenApiLoader.IsOpenApi(path, content))
        {
            return JsonSerializer.Deserialize<Manifest>(content, ManifestJson.Options);
        }

        var document = OpenApiLoader.Parse(content, out var diagnostics);
        var (manifest, warnings) = OpenApiConverter.Convert(document, path);
        foreach (var message in diagnostics.Concat(warnings))
        {
            logger.LogWarning("{Manifest}: {Message}", path, message);
        }

        return manifest;
    }

    private static Route CreateRoute(RouteDefinition definition, string directory, string manifestPath)
    {
        var response = definition.Response ?? throw new FormatException("A route must have a response.");
        return new Route
        {
            Name = definition.Name,
            Source = manifestPath,
            Method = ParseMethod(definition.Method),
            Path = definition.Path is null ? Route.MatchAll : CreateRegex(definition.Path),
            Parameters =
            [
                .. (definition.Parameters ?? []).Select(p => new RouteParameter(
                    p.Parameter ?? throw new FormatException("A parameter must have a name."),
                    p.Value is null ? Route.MatchAll : CreateRegex(p.Value)))
            ],
            Response = new RouteResponse(CreateBody(response, directory))
            {
                StatusCode = response.StatusCode ?? StatusCodes.Status200OK,
                ContentType = response.ContentType ?? RouteResponse.DefaultContentType,
                Headers = response.Headers ?? []
            }
        };
    }

    private static Func<HttpResponse, Task> CreateBody(ResponseDefinition response, string directory)
    {
        switch (response)
        {
            case { Path: not null, Body: not null }:
                throw new FormatException("A response cannot have both a path and a body.");
            case { Path: { } path }:
                return ResponseBody.FromFile(Path.GetFullPath(path, directory));
            case { Body: { } body }:
                var text = ManifestJson.BodyText(body);
                return ResponseBody.FromText(() => text);
            default:
                return ResponseBody.Empty;
        }
    }

    private static Verb ParseMethod(string? method) =>
        method is null ? Verb.Any :
        Enum.TryParse<Verb>(method, ignoreCase: true, out var verb) ? verb :
        throw new FormatException($"Unknown method '{method}'.");

    private static Regex CreateRegex(string pattern) => new(pattern, RegexOptions.CultureInvariant, MatchTimeout);
}
