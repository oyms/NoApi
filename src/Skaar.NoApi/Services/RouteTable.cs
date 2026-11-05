using Skaar.NoApi.Manifests;
using Skaar.NoApi.Model;

namespace Skaar.NoApi.Services;

/// <summary>
/// The routes from all manifests, surrounded by the <see cref="BuiltInRoutes"/>.
/// The manifests are read on first use, and again after a manifest is added, changed or removed.
/// </summary>
internal sealed class RouteTable
{
    private readonly Lock gate = new();
    private readonly ManifestSource source;
    private readonly ManifestParser parser;
    private readonly ILogger<RouteTable> logger;
    private IReadOnlyList<Route>? routes;

    public RouteTable(ManifestSource source, ManifestParser parser, ILogger<RouteTable> logger)
    {
        this.source = source;
        this.parser = parser;
        this.logger = logger;
        source.Changed += OnManifestChanged;
    }

    public IReadOnlyList<Route> Get()
    {
        lock (gate)
        {
            return routes ??= Load();
        }
    }

    private IReadOnlyList<Route> Load()
    {
        var manifests = source.GetManifestFiles();
        var loaded = manifests.SelectMany(parser.Parse).ToList();
        logger.LogInformation("Read {Routes} route(s) from {Manifests} manifest(s) in {Location}",
            loaded.Count, manifests.Count, source.Description);
        foreach (var route in loaded)
        {
            logger.LogInformation("  {Method,-10} {Path,-30} {Name}",
                route.Method.ToString().ToUpperInvariant(), route.Path, route.Name);
        }

        return BuiltInRoutes.Around(loaded);
    }

    private void OnManifestChanged(object? sender, EventArgs e)
    {
        lock (gate)
        {
            if (routes is null) return;
            logger.LogInformation("Manifest change detected");
            routes = null;
        }
    }
}
