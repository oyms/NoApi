using System.Text.Json;
using System.Text.RegularExpressions;
using Skaar.NoApi.Model;

namespace Skaar.NoApi.Services;

/// <summary>
/// Routes surrounding the routes from the manifests: a listing of the routes and the manifest schema before them
/// (so a catch-all route does not hide them), and a fallback after them, responding with an empty json document.
/// </summary>
internal static class BuiltInRoutes
{
    public const string RoutesPath = "/__routes";
    public const string SchemaPath = "/__schema";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly Lazy<string> Schema = new(() =>
    {
        using var stream = typeof(BuiltInRoutes).Assembly.GetManifestResourceStream("schema.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static IReadOnlyList<Route> Around(IEnumerable<Route> routes)
    {
        List<Route> all = [];
        all.Add(new Route
        {
            Name = "Route listing",
            Method = Verb.Get,
            Path = Exactly(RoutesPath),
            Response = new RouteResponse(ResponseBody.FromText(() =>
                JsonSerializer.Serialize(new { Routes = all.Select(Describe) }, Options)))
        });
        all.Add(new Route
        {
            Name = "Manifest schema",
            Method = Verb.Get,
            Path = Exactly(SchemaPath),
            Response = new RouteResponse(ResponseBody.FromText(() => Schema.Value))
            {
                ContentType = "application/schema+json"
            }
        });
        all.AddRange(routes);
        all.Add(new Route
        {
            Name = "Fallback",
            Response = new RouteResponse(ResponseBody.FromText(() => "{}\n"))
        });
        return all;
    }

    private static Regex Exactly(string path) => new($"^{Regex.Escape(path)}$");

    private static object Describe(Route route) => new
    {
        route.Name,
        Method = route.Method.ToString().ToUpperInvariant(),
        Path = route.Path.ToString(),
        Parameters = route.Parameters.Select(p => new { Parameter = p.Name, Value = p.Value.ToString() }),
        route.Response.StatusCode,
        route.Response.ContentType,
        route.Source
    };
}
