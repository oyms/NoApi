using Skaar.NoApi.Model;

namespace Skaar.NoApi.Services;

/// <summary>
/// Responds to every request with the first matching route.
/// </summary>
internal sealed class RequestHandler(RouteTable routes, ILogger<RequestHandler> logger)
{
    public async Task Handle(HttpContext context)
    {
        var request = context.Request;
        var form = request.HasFormContentType ? await request.ReadFormAsync(context.RequestAborted) : null;
        // The fallback route matches all requests.
        var route = routes.Get().First(r => Matches(r, request, form));
        logger.LogInformation("{Method} {Path}{Query} -> {Route}",
            request.Method, request.Path, request.QueryString, route.Name ?? "(not named)");

        var response = context.Response;
        response.StatusCode = route.Response.StatusCode;
        response.ContentType = route.Response.ContentType;
        foreach (var (name, value) in route.Response.Headers) response.Headers[name] = value;
        try
        {
            await route.Response.WriteBody(response);
        }
        catch (IOException e) when (!response.HasStarted)
        {
            logger.LogError("Could not render response for {Route} ({Source}): {Message}",
                route.Name ?? "(not named)", route.Source, e.Message);
            response.Clear();
            response.StatusCode = StatusCodes.Status500InternalServerError;
            response.ContentType = "text/plain";
            await response.WriteAsync($"NoApi: {e.Message}\n", context.RequestAborted);
        }
    }

    private static bool Matches(Route route, HttpRequest request, IFormCollection? form) =>
        MatchesMethod(route.Method, request.Method) &&
        // The path is matched as it appears in a url, i.e. url encoded.
        route.Path.IsMatch(request.Path.ToUriComponent()) &&
        route.Parameters.All(p => ParameterValues(request, form, p.Name).Any(p.Value.IsMatch));

    private static bool MatchesMethod(Verb verb, string method) =>
        verb == Verb.Any ||
        Enum.TryParse<Verb>(method, ignoreCase: true, out var requested) && requested != Verb.Any && verb.HasFlag(requested);

    private static IEnumerable<string> ParameterValues(HttpRequest request, IFormCollection? form, string name)
    {
        IEnumerable<string?> values = request.Query[name];
        if (form is not null) values = values.Concat(form[name]);
        return values.OfType<string>();
    }
}
