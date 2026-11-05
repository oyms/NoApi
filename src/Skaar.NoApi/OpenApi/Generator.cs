using System.Text.Json;
using System.Text.RegularExpressions;
using Skaar.NoApi.Model;

namespace Skaar.NoApi.OpenApi;

/// <summary>
/// <c>noapi generate &lt;file-or-url&gt; [output folder] [--inline] [--force]</c>:
/// writes a manifest (with the response bodies in separate files, unless <c>--inline</c>) from an OpenAPI document.
/// </summary>
public static partial class Generator
{
    public const string Command = "generate";
    public const string ManifestFileName = "routes.json";
    public const string ResponseFolder = "responses";

    private const string Usage = "Usage: noapi generate <file-or-url> [output folder] [--inline] [--force]";

    /// <param name="args">The arguments following <see cref="Command"/>.</param>
    /// <param name="output">Where progress is written.</param>
    /// <param name="httpClient">Downloads a document given as a url.</param>
    /// <param name="cancellationToken">Cancels reading and writing.</param>
    /// <returns>The path of the manifest written.</returns>
    public static async Task<string> Run(string[] args, TextWriter output, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        var options = args.Where(a => a.StartsWith("--")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = options.Except(["--inline", "--force"], StringComparer.OrdinalIgnoreCase).ToList();
        var positional = args.Where(a => !a.StartsWith("--")).ToList();
        if (unknown.Count > 0 || positional.Count is < 1 or > 2)
        {
            throw new ArgumentException(unknown.Count > 0 ? $"Unknown option {unknown[0]}. {Usage}" : Usage);
        }

        var input = positional[0];
        var folder = Path.GetFullPath(positional.ElementAtOrDefault(1) ?? ".");
        var manifestPath = Path.Combine(folder, ManifestFileName);
        if (File.Exists(manifestPath) && !options.Contains("--force"))
        {
            throw new InvalidOperationException($"{manifestPath} exists. Use --force to overwrite it.");
        }

        var isUrl = Uri.TryCreate(input, UriKind.Absolute, out var url) && url.Scheme is "http" or "https";
        var source = isUrl ? input : Path.GetFullPath(input);
        output.WriteLine($"Reading {source}");
        var content = isUrl
            ? await Download(url!, httpClient, cancellationToken)
            : await File.ReadAllTextAsync(source, cancellationToken);

        var document = OpenApiLoader.Parse(content, out var diagnostics);
        var (manifest, warnings) = OpenApiConverter.Convert(document, source);
        foreach (var message in diagnostics.Concat(warnings)) output.WriteLine($"Warning: {message}");

        Directory.CreateDirectory(folder);
        if (!options.Contains("--inline")) manifest = await WriteBodies(manifest, folder, cancellationToken);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, ManifestJson.Options),
            cancellationToken);
        output.WriteLine($"Wrote {manifest.Routes?.Count ?? 0} route(s) to {manifestPath}");
        return manifestPath;
    }

    private static async Task<string> Download(Uri url, HttpClient? httpClient, CancellationToken cancellationToken)
    {
        if (httpClient is not null) return await httpClient.GetStringAsync(url, cancellationToken);
        using var client = new HttpClient();
        return await client.GetStringAsync(url, cancellationToken);
    }

    /// <summary>Moves each inline body to a file in <see cref="ResponseFolder"/>, named after the route.</summary>
    private static async Task<Manifest> WriteBodies(Manifest manifest, string folder, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var routes = new List<RouteDefinition>();
        foreach (var route in manifest.Routes ?? [])
        {
            if (route.Response?.Body is not { } body)
            {
                routes.Add(route);
                continue;
            }

            var fileName = UniqueFileName(route.Name, Extension(route.Response.ContentType, body), names);
            var relativePath = $"{ResponseFolder}/{fileName}";
            Directory.CreateDirectory(Path.Combine(folder, ResponseFolder));
            await File.WriteAllTextAsync(Path.Combine(folder, ResponseFolder, fileName), ManifestJson.BodyText(body),
                cancellationToken);
            routes.Add(route with { Response = route.Response with { Path = $"./{relativePath}", Body = null } });
        }

        return manifest with { Routes = routes };
    }

    private static string UniqueFileName(string? routeName, string extension, HashSet<string> taken)
    {
        var stem = InvalidFileNameCharacters().Replace(routeName ?? "response", "-").Trim('-', '.');
        if (stem.Length == 0) stem = "response";
        var name = $"{stem}{extension}";
        for (var i = 2; !taken.Add(name); i++) name = $"{stem}-{i}{extension}";
        return name;
    }

    private static string Extension(string? contentType, JsonElement body) =>
        contentType switch
        {
            null when body.ValueKind != JsonValueKind.String => ".json",
            not null when contentType.Contains("json", StringComparison.OrdinalIgnoreCase) => ".json",
            not null when contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) => ".xml",
            not null when contentType.Contains("html", StringComparison.OrdinalIgnoreCase) => ".html",
            _ => ".txt"
        };

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex InvalidFileNameCharacters();
}
