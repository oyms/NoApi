using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Skaar.NoApi.Model;

namespace Skaar.NoApi.OpenApi;

/// <summary>
/// Converts an OpenAPI document to a manifest with a route per operation, responding with the operation's
/// successful response. The body is an example from the document, or built from the schema
/// (see <see cref="ExampleBuilder"/>).
/// </summary>
internal static partial class OpenApiConverter
{
    private const string Json = "application/json";

    public static (Manifest Manifest, IReadOnlyList<string> Warnings) Convert(OpenApiDocument document, string? source)
    {
        var warnings = new List<string>();
        var basePath = BasePath(document.Servers?.FirstOrDefault()?.Url);
        var routes =
            from path in (document.Paths ?? []).OrderBy(p => SegmentKinds(p.Key), StringComparer.Ordinal)
            from operation in path.Value.Operations ?? []
            let route = CreateRoute(basePath, path.Key, operation.Key.Method, operation.Value, warnings)
            where route is not null
            select route;
        return (new Manifest(ManifestJson.SchemaUrl, source, routes.ToList()), warnings);
    }

    private static RouteDefinition? CreateRoute(string basePath, string template, string method,
        OpenApiOperation operation, List<string> warnings)
    {
        var name = operation.OperationId ?? $"{method} {template}";
        if (!Enum.TryParse<Verb>(method, ignoreCase: true, out _))
        {
            warnings.Add($"Skipping {name}: the method {method} is not supported.");
            return null;
        }

        return new RouteDefinition(name, method, PathPattern(basePath, template), null,
            CreateResponse(operation.Responses));
    }

    private static ResponseDefinition CreateResponse(OpenApiResponses? responses)
    {
        if (responses is null || responses.Count == 0) return new ResponseDefinition(null, null, null, null, null);

        var (key, response) = responses
            .OrderBy(r => r.Key switch
            {
                ['2', _, _] when int.TryParse(r.Key, out var code) => code,
                "2XX" or "2xx" => 300,
                "default" => 400,
                _ => 1000
            })
            .First();
        var content = response.Content ?? new Dictionary<string, IOpenApiMediaType>();
        var media = content.FirstOrDefault(c => IsJson(c.Key));
        if (media.Key is null) media = content.FirstOrDefault();
        var contentType = media.Key is null || media.Key.Contains('*') ? null : media.Key;
        var body = media.Value is null ? null : Example(media.Value, IsJson(contentType ?? Json));
        return new ResponseDefinition(null, body, contentType == Json ? null : contentType, StatusCode(key), null);
    }

    private static JsonElement? Example(IOpenApiMediaType media, bool isJson)
    {
        var example = media.Example
                      ?? media.Examples?.Values.Select(e => e.Value).FirstOrDefault(v => v is not null)
                      ?? (isJson ? ExampleBuilder.Build(media.Schema) : null);
        // Other content than json can only be written as a string.
        if (example is null || !isJson && example.GetValueKind() != JsonValueKind.String) return null;
        return JsonSerializer.SerializeToElement(example);
    }

    private static int? StatusCode(string key) =>
        int.TryParse(key, out var code) ? code :
        key.Length == 3 && char.IsAsciiDigit(key[0]) && key[0] != '2' ? (key[0] - '0') * 100 :
        null; // 2XX and default: 200

    private static bool IsJson(string mediaType) =>
        mediaType.StartsWith(Json, StringComparison.OrdinalIgnoreCase) ||
        mediaType.Contains("+json", StringComparison.OrdinalIgnoreCase);

    /// <summary>The path of the first server url (which may be absolute, relative or contain variables).</summary>
    private static string BasePath(string? serverUrl)
    {
        if (string.IsNullOrEmpty(serverUrl)) return string.Empty;
        var path = Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http")
            ? uri.AbsolutePath
            : SchemeAndHost().Replace(serverUrl, string.Empty);
        return path.Split('?')[0].TrimEnd('/');
    }

    /// <summary>
    /// A pattern matching the path template, where each <c>{parameter}</c> matches a path segment
    /// (or part of one). The path is matched url encoded, so the literal parts are too.
    /// </summary>
    private static string PathPattern(string basePath, string template)
    {
        var path = (basePath + "/" + template.Trim('/')).TrimEnd('/');
        var pattern = string.Concat(Template().Split(path).Select((part, i) =>
            i % 2 == 0 ? Regex.Escape(Uri.EscapeDataString(part).Replace("%2F", "/")) : "[^/]+"));
        return $"^{pattern}/?$";
    }

    // Sorts literal segments before parameters, so /users/me is matched before /users/{id}.
    private static string SegmentKinds(string template) =>
        string.Concat(template.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Contains('{') ? 'P' : 'L'));

    // Captures the parameter name, so it is excluded by Regex.Split.
    [GeneratedRegex(@"\{([^}/]+)\}")]
    private static partial Regex Template();

    [GeneratedRegex(@"^([^:/]+:)?//[^/]*")]
    private static partial Regex SchemeAndHost();
}
