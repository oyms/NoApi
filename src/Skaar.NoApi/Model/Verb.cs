namespace Skaar.NoApi.Model;

/// <summary>
/// The http methods a route responds to. <see cref="Any"/> matches all methods.
/// </summary>
[Flags]
public enum Verb
{
    Any = 0,
    Get = 1,
    Post = 2,
    Put = 4,
    Patch = 8,
    Delete = 16,
    Head = 32,
    Options = 64
}
