namespace Skaar.NoApi.Manifests;

/// <summary>
/// Where the manifest files are found.
/// Raises <see cref="Changed"/> when a manifest file is added, changed, moved or removed.
/// </summary>
public abstract class ManifestSource : IDisposable
{
    private readonly FileSystemWatcher watcher;

    protected ManifestSource(string watchedFolder, bool includeSubdirectories)
    {
        watcher = new FileSystemWatcher(watchedFolder)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite |
                           NotifyFilters.Size
        };
        watcher.Created += OnFileSystemEvent;
        watcher.Changed += OnFileSystemEvent;
        watcher.Deleted += OnFileSystemEvent;
        watcher.Renamed += OnFileSystemEvent;
        // The event buffer overflowed, so changes may have been missed.
        watcher.Error += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Uses <paramref name="path"/> as a single manifest file if it is a file,
    /// or searches it recursively for files with any of the <paramref name="fileNames"/> if it is a folder.
    /// </summary>
    public static ManifestSource Create(string path, IReadOnlyList<string> fileNames)
    {
        path = Path.GetFullPath(path);
        if (Directory.Exists(path)) return new FolderManifestSource(path, fileNames);
        if (File.Exists(path)) return new FileManifestSource(path);
        throw new ArgumentException($"The manifest file or folder '{path}' does not exist.", nameof(path));
    }

    public event EventHandler? Changed;

    /// <summary>A description of the location, for display.</summary>
    public abstract string Description { get; }

    /// <summary>The full paths of the current manifest files, in the order they are read.</summary>
    public abstract IReadOnlyList<string> GetManifestFiles();

    /// <summary>Whether a change to <paramref name="path"/> may affect the set of manifests or their contents.</summary>
    protected abstract bool Concerns(string path, WatcherChangeTypes change);

    protected static bool IsNamed(string path, IEnumerable<string> fileNames) =>
        fileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    private void OnFileSystemEvent(object? sender, FileSystemEventArgs e)
    {
        var concerned = Concerns(e.FullPath, e.ChangeType) ||
                        e is RenamedEventArgs renamed && Concerns(renamed.OldFullPath, WatcherChangeTypes.Deleted);
        if (concerned) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
