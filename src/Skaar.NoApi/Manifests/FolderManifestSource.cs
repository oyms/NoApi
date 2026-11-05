namespace Skaar.NoApi.Manifests;

/// <summary>
/// All files with any of the file names (manifests and OpenAPI documents) in a folder and its subfolders.
/// </summary>
internal sealed class FolderManifestSource : ManifestSource
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    private readonly string folderPath;
    private readonly IReadOnlyList<string> fileNames;
    private volatile IReadOnlyList<string> known = [];

    public FolderManifestSource(string folderPath, IReadOnlyList<string> fileNames)
        : base(folderPath, includeSubdirectories: true)
    {
        this.folderPath = folderPath;
        this.fileNames = fileNames;
    }

    public override string Description =>
        Path.Combine(folderPath, "**", fileNames.Count == 1 ? fileNames[0] : $"{{{string.Join(",", fileNames)}}}");

    public override IReadOnlyList<string> GetManifestFiles() =>
        known =
        [
            .. fileNames.SelectMany(name => Directory.EnumerateFiles(folderPath, name, Enumeration))
                .Distinct().Order(StringComparer.Ordinal)
        ];

    protected override bool Concerns(string path, WatcherChangeTypes change) =>
        IsNamed(path, fileNames) || change switch
        {
            // A folder with manifests may be copied or moved in.
            WatcherChangeTypes.Created or WatcherChangeTypes.Renamed => Directory.Exists(path),
            // Or a folder with known manifests may be removed or moved out.
            WatcherChangeTypes.Deleted => known.Any(m => m.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.Ordinal)),
            _ => false
        };
}
