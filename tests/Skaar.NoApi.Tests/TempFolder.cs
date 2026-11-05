using System.Text.Json;

namespace Skaar.NoApi.Tests;

/// <summary>
/// A temporary folder for manifests and response files, deleted on dispose.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "noapi-tests", Guid.NewGuid().ToString("N"))).FullName;
    }

    public string Path { get; }

    /// <summary>Writes a file, creating its folder if needed.</summary>
    /// <returns>The full path of the file.</returns>
    public string Write(string relativePath, string content)
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Writes a manifest with the given routes (serialized with camel case names).</summary>
    public string WriteManifest(string relativePath, params object[] routes) =>
        Write(relativePath, JsonSerializer.Serialize(new { routes }, JsonSerializerOptions.Web));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
