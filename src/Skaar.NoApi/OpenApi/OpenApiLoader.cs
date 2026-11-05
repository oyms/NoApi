using System.Text.Json;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace Skaar.NoApi.OpenApi;

/// <summary>
/// Reads OpenAPI documents (OpenAPI 3.x or Swagger 2.0, json or yaml).
/// </summary>
internal static class OpenApiLoader
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// A yaml file is an OpenAPI document (manifests are json only),
    /// as is json with a root <c>openapi</c> or <c>swagger</c> property.
    /// </summary>
    public static bool IsOpenApi(string path, string content)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var json = JsonDocument.Parse(content, JsonOptions);
            return json.RootElement.ValueKind == JsonValueKind.Object &&
                   (json.RootElement.TryGetProperty("openapi", out _) || json.RootElement.TryGetProperty("swagger", out _));
        }
        catch (JsonException)
        {
            // Not json: let the manifest parser report it.
            return false;
        }
    }

    /// <param name="content">The document, as json or yaml.</param>
    /// <param name="diagnostics">Errors and warnings which did not prevent reading the document.</param>
    /// <exception cref="FormatException">The document could not be read.</exception>
    public static OpenApiDocument Parse(string content, out IReadOnlyList<string> diagnostics)
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();
        var format = content.TrimStart().StartsWith('{') ? OpenApiConstants.Json : OpenApiConstants.Yaml;
        var (document, diagnostic) = OpenApiDocument.Parse(content, format, settings);
        var messages = (diagnostic?.Errors ?? []).Select(e => e.ToString())
            .Concat((diagnostic?.Warnings ?? []).Select(w => w.ToString()))
            .ToList();
        if (document is null)
        {
            throw new FormatException($"Not a valid OpenAPI document. {string.Join(" ", messages)}".Trim());
        }

        diagnostics = messages;
        return document;
    }
}
