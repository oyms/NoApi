namespace Skaar.NoApi.Model;

// The manifest file format, as described in schema.json.

internal sealed record Manifest(IReadOnlyList<RouteDefinition>? Routes);

internal sealed record RouteDefinition(
    string? Name,
    string? Method,
    string? Path,
    IReadOnlyList<ParameterDefinition>? Parameters,
    ResponseDefinition? Response);

internal sealed record ParameterDefinition(string? Parameter, string? Value);

internal sealed record ResponseDefinition(
    string? Path,
    string? ContentType,
    int? StatusCode,
    Dictionary<string, string>? Headers);
