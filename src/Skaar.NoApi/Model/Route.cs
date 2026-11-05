using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Skaar.NoApi.Model;

/// <summary>
/// The requirements a request must meet, and the resulting response.
/// </summary>
[DebuggerDisplay("{Name ?? Path.ToString()}")]
public sealed record Route
{
    public static readonly Regex MatchAll = new(".*");

    public string? Name { get; init; }

    /// <summary>The manifest file defining the route. <c>null</c> for built-in routes.</summary>
    public string? Source { get; init; }

    public Verb Method { get; init; }

    /// <summary>A pattern the (url encoded) request path must match.</summary>
    public Regex Path { get; init; } = MatchAll;

    public IReadOnlyList<RouteParameter> Parameters { get; init; } = [];

    public required RouteResponse Response { get; init; }
}

/// <summary>
/// A query parameter or form field which must be present with a value matching <see cref="Value"/>.
/// </summary>
public sealed record RouteParameter(string Name, Regex Value);

public sealed record RouteResponse(Func<HttpResponse, Task> WriteBody)
{
    public const string DefaultContentType = "application/json";

    public int StatusCode { get; init; } = StatusCodes.Status200OK;
    public string ContentType { get; init; } = DefaultContentType;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = ReadOnlyDictionary<string, string>.Empty;
}
