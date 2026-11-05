namespace Skaar.NoApi.Services;

internal static class ResponseBody
{
    public static Func<HttpResponse, Task> Empty { get; } = _ => Task.CompletedTask;

    /// <summary>The file is read on each request, so changes to it are served without reloading the manifest.</summary>
    public static Func<HttpResponse, Task> FromFile(string path) =>
        response => response.SendFileAsync(path, response.HttpContext.RequestAborted);

    public static Func<HttpResponse, Task> FromText(Func<string> text) =>
        response => response.WriteAsync(text(), response.HttpContext.RequestAborted);
}
