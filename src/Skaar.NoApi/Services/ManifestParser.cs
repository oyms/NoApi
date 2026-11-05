using System.Text.Json;
using System.Text.RegularExpressions;
using Skaar.NoApi.Model;

namespace Skaar.NoApi.Services;

/// <summary>
/// Reads the routes of a manifest file. Errors are logged, and invalid routes (or manifests) are skipped,
/// so a manifest being written, or a mistake in one route, does not take down the others.
/// </summary>
internal sealed class ManifestParser(ILogger<ManifestParser> logger)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    public IReadOnlyList<Route> Parse(string manifestPath)
    {
        Manifest? manifest;
        try
        {
            using var stream = File.OpenRead(manifestPath);
            manifest = JsonSerializer.Deserialize<Manifest>(stream, Options);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
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
            Response = new RouteResponse(response.Path is null
                ? ResponseBody.Empty
                : ResponseBody.FromFile(Path.GetFullPath(response.Path, directory)))
            {
                StatusCode = response.StatusCode ?? StatusCodes.Status200OK,
                ContentType = response.ContentType ?? RouteResponse.DefaultContentType,
                Headers = response.Headers ?? []
            }
        };
    }

    private static Verb ParseMethod(string? method) =>
        method is null ? Verb.Any :
        Enum.TryParse<Verb>(method, ignoreCase: true, out var verb) ? verb :
        throw new FormatException($"Unknown method '{method}'.");

    private static Regex CreateRegex(string pattern) => new(pattern, RegexOptions.CultureInvariant, MatchTimeout);
}
