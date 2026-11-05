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
}
