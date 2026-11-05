namespace Skaar.NoApi.Config;

/// <summary>
/// The <see cref="SectionName"/> section of the configuration.
/// </summary>
public sealed class Settings
{
    public const string SectionName = "NoApi";

    /// <summary>
    /// The url to listen to, unless urls are set explicitly (e.g. with <c>--urls</c> or ASPNETCORE_URLS).
    /// </summary>
    public string Url { get; init; } = "https://localhost:9998";

    /// <summary>
    /// The name of the manifest files to look for when the manifest location is a folder.
    /// </summary>
    public string ManifestFileName { get; init; } = "routes.json";

    /// <summary>
    /// The names of OpenAPI documents to serve as manifests when the manifest location is a folder.
    /// </summary>
    public string[]? OpenApiFileNames { get; init; }

    /// <summary>All the file names to look for when the manifest location is a folder.</summary>
    public IReadOnlyList<string> FileNames => [ManifestFileName, .. OpenApiFileNames ?? DefaultOpenApiFileNames];

    private static readonly string[] DefaultOpenApiFileNames = ["openapi.json", "openapi.yaml", "openapi.yml"];
}
