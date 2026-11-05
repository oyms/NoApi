using Skaar.NoApi.Config;
using Skaar.NoApi.Manifests;

namespace Skaar.NoApi.Services;

public sealed class Server : IAsyncDisposable
{
    private readonly WebApplication app;

    private Server(WebApplication app) => this.app = app;

    /// <summary>
    /// The first positional argument is the manifest location (a file, or a folder searched recursively),
    /// defaulting to the working directory.
    /// Configuration is read from appsettings.json, environment variables and the other command line arguments.
    /// The server listens to <see cref="Settings.Url"/>, unless urls are set explicitly
    /// (e.g. with ASPNETCORE_URLS when running in a container).
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="configureBuilder">Additional configuration of the builder, e.g. a test server.</param>
    public static Server Create(string[] args, Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var (manifestPath, configurationArgs) = CommandLine.Split(args);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = configurationArgs,
            ContentRootPath = AppContext.BaseDirectory
        });
        var settings = builder.Configuration.GetSection(Settings.SectionName).Get<Settings>() ?? new Settings();
        if (string.IsNullOrWhiteSpace(builder.Configuration[WebHostDefaults.ServerUrlsKey]))
        {
            builder.WebHost.UseUrls(settings.Url);
        }

        var source = ManifestSource.Create(manifestPath ?? Environment.CurrentDirectory, settings.ManifestFileName);
        // Registered with a factory, so the container disposes it.
        builder.Services.AddSingleton(_ => source);
        builder.Services.AddSingleton<ManifestParser>();
        builder.Services.AddSingleton<RouteTable>();
        builder.Services.AddSingleton<RequestHandler>();
        configureBuilder?.Invoke(builder);

        var app = builder.Build();
        app.Run(app.Services.GetRequiredService<RequestHandler>().Handle);
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            // Read the manifests up front, to report errors and the routes at once.
            app.Services.GetRequiredService<RouteTable>().Get();
            app.Logger.LogInformation("NoApi listening to {Urls}. Routes: {Routes}",
                string.Join(", ", app.Urls), BuiltInRoutes.RoutesPath);
        });

        return new Server(app);
    }

    public Task Run() => app.RunAsync();

    internal WebApplication App => app;

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
