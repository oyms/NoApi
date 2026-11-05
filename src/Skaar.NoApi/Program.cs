using Skaar.NoApi.Services;

try
{
    await using var server = Server.Create(args);
    await server.Run();
}
catch (Exception e)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(e.Message);
    Console.ResetColor();
    return 1;
}

return 0;
