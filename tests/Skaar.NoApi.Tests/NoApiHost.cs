using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Skaar.NoApi.Services;

namespace Skaar.NoApi.Tests;

/// <summary>
/// Runs NoApi in memory. The first argument is the manifest location, the rest is configuration,
/// e.g. <c>--NoApi:ManifestFileName=api.json</c>.
/// </summary>
internal sealed class NoApiHost : IAsyncDisposable
{
    private readonly Server server;

    private NoApiHost(Server server)
    {
        this.server = server;
        Client = server.App.GetTestClient();
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HttpClient Client { get; }

    public static async Task<NoApiHost> Start(params string[] args)
    {
        var server = Server.Create(args, builder => builder.WebHost.UseTestServer());
        await server.App.StartAsync(Ct);
        return new NoApiHost(server);
    }

    public Task<HttpResponseMessage> Send(HttpMethod method, string uri) =>
        Client.SendAsync(new HttpRequestMessage(method, uri), Ct);

    public Task<HttpResponseMessage> Get(string uri) => Client.GetAsync(uri, Ct);

    public Task<string> GetString(string uri) => Client.GetStringAsync(uri, Ct);

    public Task<HttpResponseMessage> Post(string uri, params (string Key, string Value)[] form) =>
        Client.PostAsync(uri, new FormUrlEncodedContent(form.Select(f => KeyValuePair.Create(f.Key, f.Value))), Ct);

    public async Task<JsonElement> GetJson(string uri) =>
        await (await Get(uri)).Content.ReadFromJsonAsync<JsonElement>(Ct);

    /// <summary>The names of the routes read from manifests (i.e. not the built-in routes).</summary>
    public async Task<string?[]> ManifestRouteNames() =>
    [
        .. (await GetJson(BuiltInRoutes.RoutesPath)).GetProperty("routes").EnumerateArray()
        .Where(r => r.GetProperty("source").ValueKind != JsonValueKind.Null)
        .Select(r => r.GetProperty("name").GetString())
    ];

    /// <summary>Retries until the condition is met, since file system events arrive asynchronously.</summary>
    public static async Task Eventually(Func<Task<bool>> condition, string because)
    {
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            if (DateTime.UtcNow > timeout) throw new TimeoutException($"Timed out waiting until {because}");
            await Task.Delay(100, Ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await server.App.StopAsync(Ct);
        await server.DisposeAsync();
    }
}
