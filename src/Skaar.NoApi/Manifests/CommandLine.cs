namespace Skaar.NoApi.Manifests;

internal static class CommandLine
{
    /// <summary>
    /// Separates the manifest location (the first positional argument) from the configuration arguments.
    /// The path must be removed, since the configuration reads an argument starting with <c>/</c>
    /// (an absolute path on Linux and macOS) as a key, which swallows the next argument.
    /// </summary>
    public static (string? ManifestPath, string[] ConfigurationArgs) Split(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--")
            {
                return i + 1 < args.Length
                    ? (args[i + 1], [.. args[..i], .. args[(i + 2)..]])
                    : (null, args[..i]);
            }

            if (arg.StartsWith('-'))
            {
                // Skip the value of "--key value" style configuration arguments.
                if (!arg.Contains('=')) i++;
                continue;
            }

            if (!arg.Contains('=')) return (arg, [.. args[..i], .. args[(i + 1)..]]);
        }

        return (null, args);
    }
}
