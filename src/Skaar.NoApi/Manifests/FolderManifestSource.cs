namespace Skaar.NoApi.Manifests;

/// <summary>
/// All files with the manifest file name in a folder and its subfolders.
/// </summary>
internal sealed class FolderManifestSource : ManifestSource
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    private readonly string folderPath;
    private readonly string manifestFileName;
    private volatile IReadOnlyList<string> known = [];

    public FolderManifestSource(string folderPath, string manifestFileName)
        : base(folderPath, includeSubdirectories: true)
    {
        this.folderPath = folderPath;
        this.manifestFileName = manifestFileName;
    }

    public override string Description => Path.Combine(folderPath, "**", manifestFileName);

    public override IReadOnlyList<string> GetManifestFiles() =>
        known = [.. Directory.EnumerateFiles(folderPath, manifestFileName, Enumeration).Order(StringComparer.Ordinal)];

    protected override bool Concerns(string path, WatcherChangeTypes change) =>
        IsNamed(path, manifestFileName) || change switch
        {
            // A folder with manifests may be copied or moved in.
            WatcherChangeTypes.Created or WatcherChangeTypes.Renamed => Directory.Exists(path),
            // Or a folder with known manifests may be removed or moved out.
            WatcherChangeTypes.Deleted => known.Any(m => m.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.Ordinal)),
            _ => false
        };
}
