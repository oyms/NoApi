using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skaar.NoApi.Model;

// The manifest file format, as described in schema.json.

internal sealed record Manifest(
    [property: JsonPropertyName("$schema")] string? Schema,
    string? Source,
    IReadOnlyList<RouteDefinition>? Routes);

internal sealed record RouteDefinition(
    string? Name,
    string? Method,
    string? Path,
    IReadOnlyList<ParameterDefinition>? Parameters,
    ResponseDefinition? Response);

internal sealed record ParameterDefinition(string? Parameter, string? Value);

internal sealed record ResponseDefinition(
    string? Path,
    JsonElement? Body,
    string? ContentType,
    int? StatusCode,
    Dictionary<string, string>? Headers);

internal static class ManifestJson
{
    public const string SchemaUrl = "https://raw.githubusercontent.com/oyms/NoApi/main/src/Skaar.NoApi/schema.json";

    /// <summary>Reads manifests leniently, and writes them indented and without empty properties.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>The response content of an inline body: a string as it is, any other value as json.</summary>
    public static string BodyText(JsonElement body) =>
        body.ValueKind == JsonValueKind.String ? body.GetString()! : JsonSerializer.Serialize(body, Options);
}
