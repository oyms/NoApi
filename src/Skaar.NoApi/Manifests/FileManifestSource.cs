namespace Skaar.NoApi.Manifests;

/// <summary>
/// A single manifest file. If the file is removed there are no manifests until it is created again.
/// </summary>
internal sealed class FileManifestSource(string filePath)
    : ManifestSource(Path.GetDirectoryName(filePath)!, includeSubdirectories: false)
{
    public override string Description => filePath;

    public override IReadOnlyList<string> GetManifestFiles() => File.Exists(filePath) ? [filePath] : [];

    protected override bool Concerns(string path, WatcherChangeTypes change) =>
        IsNamed(path, Path.GetFileName(filePath));
}
